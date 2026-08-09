using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.PDFLibrary;
using RSecurityBackend.Models.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// execute a confirmed duplicate-candidate merge: fills gaps in the survivor's metadata
        /// from the duplicate, repoints every reference (bookmarks, Ganjoor/Pinterest links, visit
        /// records) from the duplicate to the survivor, creates a PDFBookRedirect so future API
        /// calls for the duplicate's id transparently serve the survivor, and removes the
        /// duplicate's own PDFBook row (reusing the same dependent-cleanup + storage-cleanup
        /// queuing that RemovePDFBookAsync uses). All in a single SaveChangesAsync, so a failure
        /// partway through leaves nothing half-applied.
        /// </summary>
        /// <param name="candidateId"></param>
        /// <param name="reviewerUserId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> MergePDFBookDuplicateAsync(Guid candidateId, Guid reviewerUserId)
        {
            try
            {
                var candidate = await _context.PDFBookDuplicateCandidates.Where(c => c.Id == candidateId).SingleOrDefaultAsync();
                if (candidate == null)
                {
                    return new RServiceResult<bool>(false, "duplicate candidate not found");
                }
                if (candidate.Status != PDFBookDuplicateCandidateStatus.Confirmed)
                {
                    return new RServiceResult<bool>(false, "only a Confirmed candidate can be merged - review and confirm it first");
                }

                int survivorId = candidate.SurvivorPDFBookId;
                int duplicateId = candidate.PDFBookId1 == survivorId ? candidate.PDFBookId2 : candidate.PDFBookId1;
                if (survivorId != candidate.PDFBookId1 && survivorId != candidate.PDFBookId2)
                {
                    return new RServiceResult<bool>(false, $"SurvivorPDFBookId must be either {candidate.PDFBookId1} or {candidate.PDFBookId2}");
                }

                PDFBook survivor = await _context.PDFBooks
                    .Include(b => b.Tags)
                    .Include(b => b.Contributers).ThenInclude(c => c.Author)
                    .Include(b => b.Pages)
                    .Where(b => b.Id == survivorId)
                    .SingleOrDefaultAsync();
                PDFBook duplicate = await _context.PDFBooks
                    .Include(b => b.Tags)
                    .Include(b => b.Contributers).ThenInclude(c => c.Author)
                    .Include(b => b.Pages).ThenInclude(p => p.ThumbnailImage)
                    .Include(b => b.Pages).ThenInclude(p => p.Tags)
                    .Where(b => b.Id == duplicateId)
                    .SingleOrDefaultAsync();

                if (survivor == null)
                {
                    return new RServiceResult<bool>(false, $"survivor PDFBook {survivorId} not found");
                }
                if (duplicate == null)
                {
                    return new RServiceResult<bool>(false, $"duplicate PDFBook {duplicateId} not found");
                }

                _FillPDFBookMetadataGaps(survivor, duplicate);
                _MergeTags(survivor, duplicate);
                _MergeContributers(survivor, duplicate);

                await _RepointPDFBookReferencesAsync(survivor, duplicate);

                // flatten any existing redirects that pointed at the duplicate onto the survivor,
                // so a lookup is always a single row read no matter how many merges deep it is
                var chainedRedirects = await _context.PDFBookRedirects.Where(r => r.SurvivorPDFBookId == duplicateId).ToArrayAsync();
                foreach (var r in chainedRedirects)
                {
                    r.SurvivorPDFBookId = survivorId;
                }
                _context.PDFBookRedirects.UpdateRange(chainedRedirects);

                _context.PDFBookRedirects.Add
                (
                    new PDFBookRedirect()
                    {
                        Id = Guid.NewGuid(),
                        OldPDFBookId = duplicateId,
                        SurvivorPDFBookId = survivorId,
                        MergeDate = DateTime.Now,
                        MergedByUserId = reviewerUserId
                    }
                );

                await _RepointOtherDuplicateCandidatesAsync(candidateId, duplicateId, survivorId);

                candidate.Status = PDFBookDuplicateCandidateStatus.Merged;
                candidate.ReviewerId = reviewerUserId;
                candidate.ReviewDate = DateTime.Now;
                _context.Update(candidate);

                await _QueuePDFBookRemovalAsync(duplicate);

                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// fill blank/null metadata fields on the survivor from the duplicate. Never overwrites a
        /// field the survivor already has a value for. Fields describing the physical file itself
        /// (PageCount, FileMD5CheckSum, StorageFolderName, PDFFile, OCR state, BookText) are left
        /// untouched - they must stay accurate to whichever file the survivor is actually serving.
        /// </summary>
        private static void _FillPDFBookMetadataGaps(PDFBook survivor, PDFBook duplicate)
        {
            if (string.IsNullOrWhiteSpace(survivor.SubTitle)) survivor.SubTitle = duplicate.SubTitle;
            if (string.IsNullOrWhiteSpace(survivor.AuthorsLine)) survivor.AuthorsLine = duplicate.AuthorsLine;
            if (string.IsNullOrWhiteSpace(survivor.ISBN)) survivor.ISBN = duplicate.ISBN;
            if (string.IsNullOrWhiteSpace(survivor.Description)) survivor.Description = duplicate.Description;
            if (string.IsNullOrWhiteSpace(survivor.Language)) survivor.Language = duplicate.Language;
            if (string.IsNullOrWhiteSpace(survivor.TranslatorsLine)) survivor.TranslatorsLine = duplicate.TranslatorsLine;
            if (string.IsNullOrWhiteSpace(survivor.TitleInOriginalLanguage)) survivor.TitleInOriginalLanguage = duplicate.TitleInOriginalLanguage;
            if (string.IsNullOrWhiteSpace(survivor.PublisherLine)) survivor.PublisherLine = duplicate.PublisherLine;
            if (string.IsNullOrWhiteSpace(survivor.PublishingDate)) survivor.PublishingDate = duplicate.PublishingDate;
            if (string.IsNullOrWhiteSpace(survivor.PublishingLocation)) survivor.PublishingLocation = duplicate.PublishingLocation;
            if (!survivor.PublishingNumber.HasValue) survivor.PublishingNumber = duplicate.PublishingNumber;
            if (!survivor.ClaimedPageCount.HasValue) survivor.ClaimedPageCount = duplicate.ClaimedPageCount;
            if (string.IsNullOrWhiteSpace(survivor.OriginalSourceName)) survivor.OriginalSourceName = duplicate.OriginalSourceName;
            if (string.IsNullOrWhiteSpace(survivor.OriginalSourceUrl)) survivor.OriginalSourceUrl = duplicate.OriginalSourceUrl;
        }

        /// <summary>
        /// move any of the duplicate's tags the survivor doesn't already have an equivalent of
        /// (same RTagId + FriendlyUrl) onto the survivor; the rest stay on the duplicate and are
        /// discarded along with it by _QueuePDFBookRemovalAsync
        /// </summary>
        private static void _MergeTags(PDFBook survivor, PDFBook duplicate)
        {
            foreach (var tag in duplicate.Tags.ToList())
            {
                bool alreadyHasEquivalent = survivor.Tags.Any(t => t.RTagId == tag.RTagId && t.FriendlyUrl == tag.FriendlyUrl);
                if (!alreadyHasEquivalent)
                {
                    duplicate.Tags.Remove(tag);
                    survivor.Tags.Add(tag);
                }
            }
        }

        /// <summary>
        /// move any of the duplicate's author/contributor links the survivor doesn't already have
        /// an equivalent of (same author + role) onto the survivor
        /// </summary>
        private static void _MergeContributers(PDFBook survivor, PDFBook duplicate)
        {
            foreach (var contributer in duplicate.Contributers.ToList())
            {
                bool alreadyHasEquivalent = survivor.Contributers.Any(c => c.Author?.Id == contributer.Author?.Id && c.Role == contributer.Role);
                if (!alreadyHasEquivalent)
                {
                    duplicate.Contributers.Remove(contributer);
                    survivor.Contributers.Add(contributer);
                }
            }
        }

        /// <summary>
        /// repoint every table that references the duplicate PDFBook onto the survivor.
        /// PDFUserBookmark's PageId gets remapped to the survivor's page with the same page
        /// number (nearest fallback, since the two books are only guaranteed close in page count,
        /// not identical - see the page-count disqualifier tolerance in duplicate detection).
        /// PDFGanjoorLink stores a plain PageNumber (not a page id FK) so it needs no remapping,
        /// just a repointed PDFBookId. AIQueue/OCRQueue are per-file processing-queue artifacts
        /// tied to the duplicate's specific PDFFile, not user-facing data, so they're simply
        /// dropped for the duplicate (via _QueuePDFBookRemovalAsync) rather than repointed.
        /// </summary>
        private async Task _RepointPDFBookReferencesAsync(PDFBook survivor, PDFBook duplicate)
        {
            var survivorPagesByNumber = new Dictionary<int, int>(); // PageNumber -> PDFPage.Id
            foreach (var p in survivor.Pages)
            {
                if (!survivorPagesByNumber.ContainsKey(p.PageNumber))
                    survivorPagesByNumber[p.PageNumber] = p.Id;
            }

            int? ClosestSurvivorPageId(int pageNumber)
            {
                if (survivor.Pages.Count == 0)
                    return null;
                if (survivorPagesByNumber.TryGetValue(pageNumber, out int exactId))
                    return exactId;
                return survivor.Pages.OrderBy(p => Math.Abs(p.PageNumber - pageNumber)).First().Id;
            }

            var duplicatePagesById = duplicate.Pages.ToDictionary(p => p.Id, p => p);

            var duplicatePageIds = duplicate.Pages.Select(p => p.Id).ToArray();
            var bookmarks =
                await _context.PDFUserBookmarks
                .Where(bm => bm.PDFBookId == duplicate.Id || (bm.PageId != null && duplicatePageIds.Contains(bm.PageId.Value)))
                .ToArrayAsync();
            foreach (var bm in bookmarks)
            {
                if (bm.PageId.HasValue)
                {
                    // normally always one of the duplicate's own pages (the WHERE above already
                    // filtered to this book), but fall back to clearing rather than leaving a
                    // stale PageId if that invariant is ever violated - a dangling PageId would
                    // otherwise block deletion of the duplicate's PDFPage rows
                    bm.PageId = duplicatePagesById.TryGetValue(bm.PageId.Value, out var duplicatePage)
                        ? ClosestSurvivorPageId(duplicatePage.PageNumber)
                        : null;
                }
                bm.PDFBookId = survivor.Id;
            }
            _context.PDFUserBookmarks.UpdateRange(bookmarks);

            var ganjoorLinks = await _context.PDFGanjoorLinks.Where(l => l.PDFBookId == duplicate.Id).ToArrayAsync();
            foreach (var l in ganjoorLinks)
            {
                l.PDFBookId = survivor.Id;
            }
            _context.PDFGanjoorLinks.UpdateRange(ganjoorLinks);

            var pinterestLinks = await _context.PinterestLinks.Where(l => l.PDFBookId == duplicate.Id).ToArrayAsync();
            foreach (var l in pinterestLinks)
            {
                l.PDFBookId = survivor.Id;
            }
            _context.PinterestLinks.UpdateRange(pinterestLinks);

            var visits = await _context.PDFVisitRecords.Where(v => v.PDFBookId == duplicate.Id).ToArrayAsync();
            foreach (var v in visits)
            {
                v.PDFBookId = survivor.Id;
            }
            _context.PDFVisitRecords.UpdateRange(visits);
        }

        /// <summary>
        /// other pending duplicate-candidate rows that mention the just-merged-away duplicate get
        /// repointed onto the survivor instead of left dangling: if that would make a row refer to
        /// the survivor on both sides, it's removed (no longer meaningful); if an equivalent
        /// survivor-vs-other-side row already exists, the repointed one is removed to avoid a
        /// duplicate pair.
        /// </summary>
        private async Task _RepointOtherDuplicateCandidatesAsync(Guid excludeCandidateId, int duplicateId, int survivorId)
        {
            var otherCandidates =
                await _context.PDFBookDuplicateCandidates
                .Where(c => c.Id != excludeCandidateId && (c.PDFBookId1 == duplicateId || c.PDFBookId2 == duplicateId))
                .ToArrayAsync();

            foreach (var oc in otherCandidates)
            {
                int otherSide = oc.PDFBookId1 == duplicateId ? oc.PDFBookId2 : oc.PDFBookId1;
                if (otherSide == survivorId)
                {
                    _context.PDFBookDuplicateCandidates.Remove(oc);
                    continue;
                }

                int lower = Math.Min(survivorId, otherSide);
                int higher = Math.Max(survivorId, otherSide);

                bool alreadyExists =
                    await _context.PDFBookDuplicateCandidates
                    .AnyAsync(c => c.Id != oc.Id && c.PDFBookId1 == lower && c.PDFBookId2 == higher);

                if (alreadyExists)
                {
                    _context.PDFBookDuplicateCandidates.Remove(oc);
                }
                else
                {
                    oc.PDFBookId1 = lower;
                    oc.PDFBookId2 = higher;
                    oc.SurvivorPDFBookId = lower; // reset to the new pair's default; reviewer can still change it
                    _context.PDFBookDuplicateCandidates.Update(oc);
                }
            }
        }
    }
}
