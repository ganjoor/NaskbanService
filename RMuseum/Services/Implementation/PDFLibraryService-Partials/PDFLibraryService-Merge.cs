using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.PDFLibrary;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// execute a single confirmed duplicate-candidate merge (request-scoped entry point - uses
        /// this instance's own _context). See _MergePDFBookDuplicateAsync for what it actually does.
        /// </summary>
        /// <param name="candidateId"></param>
        /// <param name="reviewerUserId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> MergePDFBookDuplicateAsync(Guid candidateId, Guid reviewerUserId)
        {
            return await _MergePDFBookDuplicateAsync(_context, candidateId, reviewerUserId, null);
        }

        /// <summary>
        /// start merging EVERY Confirmed duplicate candidate in a single background job, not one
        /// at a time. The full list of Confirmed candidate ids is read ONCE up front (not
        /// re-queried per iteration with a growing exclusion list) - safe because nothing else
        /// modifies the Confirmed set while this runs, and repointing effects from earlier merges
        /// in the same batch (see _RepointOtherDuplicateCandidatesAsync) still apply correctly
        /// because each merge re-reads its own candidate row fresh by id regardless of what the
        /// outer list looked like when it was captured. A candidate whose merge fails is recorded
        /// in its ReviewNote and left as Confirmed (retried on a future run) rather than blocking
        /// the rest of the batch; a candidate that another merge earlier in this same run already
        /// repointed away or removed is silently skipped, not counted as a failure.
        /// </summary>
        public void StartMergingConfirmedPDFBookDuplicatesAsync()
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
                                   (
                                       async token =>
                                       {
                                           using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                           {
                                               LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                               var job = (await jobProgressServiceEF.NewJob("StartMergingConfirmedPDFBookDuplicatesAsync", "Query data")).Result;

                                               try
                                               {
                                                   await _MergeAllConfirmedPDFBookDuplicatesAsync(jobProgressServiceEF, job.Id);

                                                   await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);

                                                   // reclaim storage for everything just merged away; safe/cheap
                                                   // to call, and safe to call again later if this run doesn't finish
                                                   StartCleaningUpPendingPDFStorageAsync();
                                               }
                                               catch (Exception exp)
                                               {
                                                   await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                               }
                                           }
                                       }
                                   );
        }

        /// <summary>
        /// drives the batch: a fresh RMuseumDbContext per merge, disposed immediately after use,
        /// to keep EF Core's change tracker from growing unboundedly across a long run. The list
        /// of candidate ids to attempt is captured once at the start with a single lightweight
        /// query (per your point: nothing else touches the Confirmed set mid-run, so there is no
        /// need to keep re-querying it).
        /// </summary>
        private async Task _MergeAllConfirmedPDFBookDuplicatesAsync(LongRunningJobProgressServiceEF jobProgressServiceEF, Guid jobId)
        {
            List<Guid> candidateIds;
            using (var listContext = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
            {
                candidateIds =
                    await listContext.PDFBookDuplicateCandidates
                    .AsNoTracking()
                    .Where(c => c.Status == PDFBookDuplicateCandidateStatus.Confirmed)
                    .OrderBy(c => c.QueueTime)
                    .Select(c => c.Id)
                    .ToListAsync();
            }

            int total = candidateIds.Count;
            await jobProgressServiceEF.UpdateJob(jobId, 1, $"{total} confirmed candidates to merge");

            int merged = 0;
            int failed = 0;
            int alreadyResolved = 0;

            for (int i = 0; i < candidateIds.Count; i++)
            {
                Guid candidateId = candidateIds[i];
                int index = i + 1;

                async Task ReportStep(string step)
                {
                    int percent = total > 0 ? 1 + (int)(97.0 * i / total) : 50;
                    await jobProgressServiceEF.UpdateJob(jobId, Math.Min(percent, 98), $"[{index}/{total}] candidate {candidateId}: {step} (merged {merged}, failed {failed}, already resolved {alreadyResolved})");
                }

                using (var mergeContext = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                {
                    var res = await _MergePDFBookDuplicateAsync(mergeContext, candidateId, null, ReportStep);

                    if (res.Result)
                    {
                        merged++;
                    }
                    else if (res.ExceptionString == "duplicate candidate not found" || res.ExceptionString == "only a Confirmed candidate can be merged - review and confirm it first")
                    {
                        // same "already handled earlier in this batch" case, just caught a step
                        // later (candidate existed when we listed ids, changed status/vanished by
                        // the time we got to it)
                        alreadyResolved++;
                    }
                    else
                    {
                        failed++;

                        var stillThere = await mergeContext.PDFBookDuplicateCandidates.Where(c => c.Id == candidateId).SingleOrDefaultAsync();
                        if (stillThere != null)
                        {
                            stillThere.ReviewNote = $"merge failed on {DateTime.Now:yyyy-MM-dd HH:mm}: {res.ExceptionString}";
                            mergeContext.Update(stillThere);
                            await mergeContext.SaveChangesAsync();
                        }
                    }
                }
            }

            await jobProgressServiceEF.UpdateJob(jobId, 99, $"done: merged {merged}, failed {failed}, already resolved {alreadyResolved}");
        }

        /// <summary>
        /// execute a confirmed duplicate-candidate merge: fills gaps in the survivor's metadata
        /// from the duplicate, repoints every reference (bookmarks, Ganjoor/Pinterest links, visit
        /// records) from the duplicate to the survivor, creates a PDFBookRedirect so future API
        /// calls for the duplicate's id transparently serve the survivor, and removes the
        /// duplicate's own PDFBook row (reusing the same dependent-cleanup + storage-cleanup
        /// queuing that RemovePDFBookAsync uses). All in a single SaveChangesAsync, so a failure
        /// partway through leaves nothing half-applied. Takes `context` explicitly so it can run
        /// either request-scoped (single merge) or against a background job's own context (batch).
        /// `reviewerUserIdOverride`, if given, is recorded as who performed the merge (the
        /// interactive single-merge path: whoever is logged in and clicks merge right now); if
        /// null (the batch path - no interactive user), the candidate's own ReviewerId (whoever
        /// confirmed it) is used instead, read directly off the row already being loaded rather
        /// than requiring the caller to fetch it separately first.
        /// `reportStep`, if given, is called before each major phase so a batch run can surface
        /// exactly where a slow or stuck merge is spending its time - pass null to skip.
        /// </summary>
        /// <summary>
        /// manually merge two PDFBooks by id directly, without needing a pre-existing duplicate-
        /// candidate row - for an operator (with PDFBook delete permission) who spots a duplicate
        /// directly, e.g. while browsing, rather than through the automated detection queue. Runs
        /// the exact same merge mechanics as the candidate-driven flow (metadata fill, reference
        /// repointing, redirect creation, duplicate removal); it just has no candidate to check a
        /// Confirmed status against, since there isn't one.
        /// </summary>
        /// <param name="survivorPDFBookId">the PDFBook id that stays and receives the merged data</param>
        /// <param name="duplicatePDFBookId">the PDFBook id that gets merged away and removed</param>
        /// <param name="reviewerUserId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> MergePDFBooksByIdAsync(int survivorPDFBookId, int duplicatePDFBookId, Guid reviewerUserId)
        {
            if (survivorPDFBookId == duplicatePDFBookId)
            {
                return new RServiceResult<bool>(false, "survivorPDFBookId and duplicatePDFBookId must be different");
            }
            return await _MergePDFBooksAsync(_context, survivorPDFBookId, duplicatePDFBookId, reviewerUserId, Guid.Empty, null, null);
        }

        /// <summary>
        /// candidate-driven merge: resolves survivor/duplicate ids from a Confirmed
        /// PDFBookDuplicateCandidate row, then delegates the actual merge work to
        /// _MergePDFBooksAsync (shared with the manual-by-id path above), additionally marking the
        /// candidate itself as Merged in the same transaction.
        /// </summary>
        private async Task<RServiceResult<bool>> _MergePDFBookDuplicateAsync(RMuseumDbContext context, Guid candidateId, Guid? reviewerUserIdOverride, Func<string, Task> reportStep)
        {
            async Task Report(string step)
            {
                if (reportStep != null)
                    await reportStep(step);
            }

            await Report("loading candidate");
            var candidate = await context.PDFBookDuplicateCandidates.Where(c => c.Id == candidateId).SingleOrDefaultAsync();
            if (candidate == null)
            {
                return new RServiceResult<bool>(false, "duplicate candidate not found");
            }
            if (candidate.Status != PDFBookDuplicateCandidateStatus.Confirmed)
            {
                return new RServiceResult<bool>(false, "only a Confirmed candidate can be merged - review and confirm it first");
            }

            Guid reviewerUserId = reviewerUserIdOverride ?? candidate.ReviewerId ?? Guid.Empty;

            int survivorId = candidate.SurvivorPDFBookId;
            int duplicateId = candidate.PDFBookId1 == survivorId ? candidate.PDFBookId2 : candidate.PDFBookId1;
            if (survivorId != candidate.PDFBookId1 && survivorId != candidate.PDFBookId2)
            {
                return new RServiceResult<bool>(false, $"SurvivorPDFBookId must be either {candidate.PDFBookId1} or {candidate.PDFBookId2}");
            }

            return await _MergePDFBooksAsync(context, survivorId, duplicateId, reviewerUserId, candidateId, reportStep, ctx =>
            {
                candidate.Status = PDFBookDuplicateCandidateStatus.Merged;
                candidate.ReviewerId = reviewerUserId;
                candidate.ReviewDate = DateTime.Now;
                ctx.Update(candidate);
            });
        }

        /// <summary>
        /// the actual merge mechanics, shared by both entry points above: fills gaps in the
        /// survivor's metadata from the duplicate, repoints every reference (bookmarks,
        /// Ganjoor/Pinterest links, visit records) from the duplicate to the survivor, creates a
        /// PDFBookRedirect so future API calls for the duplicate's id transparently serve the
        /// survivor, repoints/cleans up any other pending duplicate-candidate rows that mentioned
        /// the duplicate, and removes the duplicate's own PDFBook row (reusing the same
        /// dependent-cleanup + storage-cleanup queuing that RemovePDFBookAsync uses). All in a
        /// single SaveChangesAsync, so a failure partway through leaves nothing half-applied.
        /// `excludeCandidateId` is the candidate row to exclude from the "repoint other pending
        /// candidates" step (pass Guid.Empty for the manual-by-id path, which has none of its own).
        /// `extraWorkBeforeSave`, if given, runs just before the final SaveChangesAsync so a caller
        /// can make additional changes (e.g. marking a candidate Merged) as part of the same
        /// transaction. `reportStep`, if given, is called before each major phase.
        /// </summary>
        private async Task<RServiceResult<bool>> _MergePDFBooksAsync(RMuseumDbContext context, int survivorId, int duplicateId, Guid reviewerUserId, Guid excludeCandidateId, Func<string, Task> reportStep, Action<RMuseumDbContext> extraWorkBeforeSave)
        {
            async Task Report(string step)
            {
                if (reportStep != null)
                    await reportStep(step);
            }

            try
            {
                await Report($"loading survivor {survivorId}");
                // AsSplitQuery: Pages is a to-many collection with its own to-many collection
                // (Tags) included underneath it - combining that with another Include in a single
                // (default) query multiplies each page row by its tag count, ballooning the result
                // set for any book with many pages. Splitting into separate queries per collection
                // avoids that Cartesian-join blowup.
                PDFBook survivor = await context.PDFBooks
                    .AsSplitQuery()
                    .Include(b => b.Tags)
                    .Include(b => b.Contributers).ThenInclude(c => c.Author)
                    .Include(b => b.Pages)
                    .Where(b => b.Id == survivorId)
                    .SingleOrDefaultAsync();

                await Report($"loading duplicate {duplicateId} (including its pages)");
                PDFBook duplicate = await context.PDFBooks
                    .AsSplitQuery()
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

                await Report("filling metadata gaps, merging tags and contributers");
                _FillPDFBookMetadataGaps(survivor, duplicate);
                _MergeTags(survivor, duplicate);
                _MergeContributers(survivor, duplicate);

                await Report($"repointing bookmarks/Ganjoor+Pinterest links/visit records ({duplicate.Pages.Count} pages)");
                await _RepointPDFBookReferencesAsync(context, survivor, duplicate);

                await Report("updating redirect chain");
                // flatten any existing redirects that pointed at the duplicate onto the survivor,
                // so a lookup is always a single row read no matter how many merges deep it is
                var chainedRedirects = await context.PDFBookRedirects.Where(r => r.SurvivorPDFBookId == duplicateId).ToArrayAsync();
                foreach (var r in chainedRedirects)
                {
                    r.SurvivorPDFBookId = survivorId;
                }
                context.PDFBookRedirects.UpdateRange(chainedRedirects);

                context.PDFBookRedirects.Add
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

                await Report("repointing other pending duplicate candidates");
                await _RepointOtherDuplicateCandidatesAsync(context, excludeCandidateId, duplicateId, survivorId);

                extraWorkBeforeSave?.Invoke(context);

                await Report("queuing duplicate's PDFBook row for removal");
                await _QueuePDFBookRemovalAsync(context, duplicate);

                await Report("saving changes");
                await context.SaveChangesAsync();
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
        private static async Task _RepointPDFBookReferencesAsync(RMuseumDbContext context, PDFBook survivor, PDFBook duplicate)
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
                await context.PDFUserBookmarks
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
            context.PDFUserBookmarks.UpdateRange(bookmarks);

            var ganjoorLinks = await context.PDFGanjoorLinks.Where(l => l.PDFBookId == duplicate.Id).ToArrayAsync();
            foreach (var l in ganjoorLinks)
            {
                l.PDFBookId = survivor.Id;
            }
            context.PDFGanjoorLinks.UpdateRange(ganjoorLinks);

            var pinterestLinks = await context.PinterestLinks.Where(l => l.PDFBookId == duplicate.Id).ToArrayAsync();
            foreach (var l in pinterestLinks)
            {
                l.PDFBookId = survivor.Id;
            }
            context.PinterestLinks.UpdateRange(pinterestLinks);

            var visits = await context.PDFVisitRecords.Where(v => v.PDFBookId == duplicate.Id).ToArrayAsync();
            foreach (var v in visits)
            {
                v.PDFBookId = survivor.Id;
            }
            context.PDFVisitRecords.UpdateRange(visits);
        }

        /// <summary>
        /// other pending duplicate-candidate rows that mention the just-merged-away duplicate get
        /// repointed onto the survivor instead of left dangling: if that would make a row refer to
        /// the survivor on both sides, it's removed (no longer meaningful); if an equivalent
        /// survivor-vs-other-side row already exists, the repointed one is removed to avoid a
        /// duplicate pair. This is also what lets a batch run correctly cluster three-or-more-way
        /// duplicates: merging (A,B) repoints a pending (B,C) row onto (A,C), still Confirmed, so
        /// the batch loop picks it up next and folds C into A too.
        /// </summary>
        private static async Task _RepointOtherDuplicateCandidatesAsync(RMuseumDbContext context, Guid excludeCandidateId, int duplicateId, int survivorId)
        {
            var otherCandidates =
                await context.PDFBookDuplicateCandidates
                .Where(c => c.Id != excludeCandidateId && (c.PDFBookId1 == duplicateId || c.PDFBookId2 == duplicateId))
                .ToArrayAsync();

            foreach (var oc in otherCandidates)
            {
                int otherSide = oc.PDFBookId1 == duplicateId ? oc.PDFBookId2 : oc.PDFBookId1;
                if (otherSide == survivorId)
                {
                    context.PDFBookDuplicateCandidates.Remove(oc);
                    continue;
                }

                int lower = Math.Min(survivorId, otherSide);
                int higher = Math.Max(survivorId, otherSide);

                bool alreadyExists =
                    await context.PDFBookDuplicateCandidates
                    .AnyAsync(c => c.Id != oc.Id && c.PDFBookId1 == lower && c.PDFBookId2 == higher);

                if (alreadyExists)
                {
                    context.PDFBookDuplicateCandidates.Remove(oc);
                }
                else
                {
                    oc.PDFBookId1 = lower;
                    oc.PDFBookId2 = higher;
                    oc.SurvivorPDFBookId = lower; // reset to the new pair's default; reviewer can still change it
                    context.PDFBookDuplicateCandidates.Update(oc);
                }
            }
        }
    }
}
