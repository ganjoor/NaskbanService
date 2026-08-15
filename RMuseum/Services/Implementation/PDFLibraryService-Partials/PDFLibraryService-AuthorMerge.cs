using Microsoft.EntityFrameworkCore;
using RMuseum.Models.PDFLibrary;
using RSecurityBackend.Models.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// merge two Author records by id - for an operator (with PDFBook delete permission,
        /// same policy as PDFBook merge) who spots two spellings of the same author, e.g. while
        /// browsing the authors list. Unlike PDFBook merge, this creates no redirect: nothing
        /// external references an author by a stable, shareable id/url the way a book's PDF file
        /// does, so there is nothing that would need one.
        /// </summary>
        /// <param name="survivorAuthorId">the Author id that stays</param>
        /// <param name="duplicateAuthorId">the Author id that gets merged away and removed</param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> MergeAuthorsByIdAsync(int survivorAuthorId, int duplicateAuthorId)
        {
            if (survivorAuthorId == duplicateAuthorId)
            {
                return new RServiceResult<bool>(false, "survivorAuthorId and duplicateAuthorId must be different");
            }

            try
            {
                var survivor = await _context.Authors.Where(a => a.Id == survivorAuthorId).SingleOrDefaultAsync();
                if (survivor == null)
                {
                    return new RServiceResult<bool>(false, $"survivor author {survivorAuthorId} not found");
                }

                var duplicate = await _context.Authors.Where(a => a.Id == duplicateAuthorId).SingleOrDefaultAsync();
                if (duplicate == null)
                {
                    return new RServiceResult<bool>(false, $"duplicate author {duplicateAuthorId} not found");
                }

                await _RepointAuthorContributionsAsync(survivor, duplicate);
                await _RepointBookAuthorRolesAsync(survivor, duplicate);
                await _RepointAuthorPinsAsync(survivorAuthorId, duplicateAuthorId);

                _context.Authors.Remove(duplicate);

                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// AuthorRole is reachable from two different owning collections - PDFBook.Contributers
        /// (handled here) and Book.Authors (the separate, higher-level Book entity - handled by
        /// _RepointBookAuthorRolesAsync below). Both need repointing before the duplicate Author
        /// row can be removed, or the DELETE fails on whichever FK path was missed - AuthorRole
        /// has no DbSet or navigation of its own back to either owner, so each owner's rows are
        /// loaded and walked directly rather than the role rows themselves. For each one: a
        /// duplicate contribution is dropped if the survivor already has an equivalent (same
        /// role) on that same book, otherwise repointed onto the survivor. The book's free-text
        /// AuthorsLine/TranslatorsLine also gets the duplicate's exact name swapped for the
        /// survivor's - those fields get re-parsed into AuthorRole rows again on this book's
        /// *next* edit (see EditPDFBookMasterRecordAsync's contributor-sync calls), and leaving
        /// the old spelling in the text would silently recreate the very duplicate this merge
        /// just removed, the next time anyone unrelated edits that book.
        /// </summary>
        private async Task _RepointAuthorContributionsAsync(Author survivor, Author duplicate)
        {
            var affectedBooks = await _context.PDFBooks
                .Include(b => b.Contributers).ThenInclude(c => c.Author)
                .Where(b => b.Contributers.Any(c => c.Author.Id == duplicate.Id))
                .ToListAsync();

            string survivorName = survivor.Name ?? "";

            foreach (var book in affectedBooks)
            {
                var duplicateContributions = book.Contributers.Where(c => c.Author.Id == duplicate.Id).ToList();
                foreach (var dc in duplicateContributions)
                {
                    bool survivorAlreadyHasThisRole = book.Contributers.Any(c => c.Author.Id == survivor.Id && c.Role == dc.Role);
                    if (survivorAlreadyHasThisRole)
                    {
                        // explicit delete, not book.Contributers.Remove(dc) - removing a
                        // dependent from a collection navigation only detaches it in memory,
                        // it does not reliably issue a DELETE unless the relationship happens
                        // to be configured to cascade-delete orphans, which is not something
                        // to assume for a shadow-FK-only entity like AuthorRole. This is what
                        // actually caused the merge to keep failing on the same FK constraint
                        // even after the duplicate contributions were supposedly "removed" -
                        // the rows were still sitting in the database, untouched, still
                        // pointing at the duplicate author.
                        book.Contributers.Remove(dc);
                        _context.Remove(dc);
                    }
                    else
                    {
                        dc.Author = survivor;
                    }
                }

                if (!string.IsNullOrEmpty(duplicate.Name))
                {
                    if (!string.IsNullOrEmpty(book.AuthorsLine))
                        book.AuthorsLine = book.AuthorsLine.Replace(duplicate.Name, survivorName);
                    if (!string.IsNullOrEmpty(book.TranslatorsLine))
                        book.TranslatorsLine = book.TranslatorsLine.Replace(duplicate.Name, survivorName);
                }
            }

            _context.PDFBooks.UpdateRange(affectedBooks);
        }

        /// <summary>
        /// same repoint-or-drop-on-collision logic as _RepointAuthorContributionsAsync above,
        /// but for the separate Book entity's own AuthorRole collection (Book.Authors) - the
        /// second, previously-missed path that caused MergeAuthorsByIdAsync's final Authors
        /// removal to fail with a REFERENCE constraint violation on FK_AuthorRole_Authors_AuthorId
        /// the first time this ran, since only PDFBook.Contributers was being repointed. Book has
        /// no AuthorsLine/TranslatorsLine-style free text to fix up (see Book.cs - just Name and
        /// Description), so there is no equivalent text-replacement step needed here.
        /// </summary>
        private async Task _RepointBookAuthorRolesAsync(Author survivor, Author duplicate)
        {
            var affectedBooks = await _context.Books
                .Include(b => b.Authors).ThenInclude(c => c.Author)
                .Where(b => b.Authors.Any(c => c.Author.Id == duplicate.Id))
                .ToListAsync();

            foreach (var book in affectedBooks)
            {
                var duplicateContributions = book.Authors.Where(c => c.Author.Id == duplicate.Id).ToList();
                foreach (var dc in duplicateContributions)
                {
                    bool survivorAlreadyHasThisRole = book.Authors.Any(c => c.Author.Id == survivor.Id && c.Role == dc.Role);
                    if (survivorAlreadyHasThisRole)
                    {
                        // see the matching comment in _RepointAuthorContributionsAsync above -
                        // an explicit delete is needed here for the same reason
                        book.Authors.Remove(dc);
                        _context.Remove(dc);
                    }
                    else
                    {
                        dc.Author = survivor;
                    }
                }
            }

            _context.Books.UpdateRange(affectedBooks);
        }

        /// <summary>
        /// every pin on the duplicate is tombstoned (IsDeleted = true), never hard-deleted - a
        /// hard delete would leave another device's already-pulled local pin dangling forever,
        /// since a future sync pull would never mention this row again to tell that device to
        /// remove it (see PDFPinnedAuthor's own doc comment on IsDeleted). If that same user
        /// doesn't already have the survivor pinned, a fresh pin on the survivor is created for
        /// them in the same pass - the person doing the merge is fixing which record represents
        /// this author, not asking anyone to re-decide whether they wanted that author pinned.
        /// </summary>
        private async Task _RepointAuthorPinsAsync(int survivorAuthorId, int duplicateAuthorId)
        {
            var duplicatePins = await _context.PDFPinnedAuthors
                .Where(p => p.AuthorId == duplicateAuthorId && !p.IsDeleted)
                .ToListAsync();

            foreach (var pin in duplicatePins)
            {
                bool userAlreadyHasSurvivorPin = await _context.PDFPinnedAuthors
                    .AnyAsync(p => p.RAppUserId == pin.RAppUserId && p.AuthorId == survivorAuthorId && !p.IsDeleted);

                pin.IsDeleted = true;
                pin.LastModified = DateTime.Now;

                if (!userAlreadyHasSurvivorPin)
                {
                    _context.PDFPinnedAuthors.Add(new PDFPinnedAuthor()
                    {
                        Id = Guid.NewGuid(),
                        RAppUserId = pin.RAppUserId,
                        AuthorId = survivorAuthorId,
                        PinnedAt = DateTime.Now,
                        LastModified = DateTime.Now,
                        IsDeleted = false
                    });
                }
            }

            _context.PDFPinnedAuthors.UpdateRange(duplicatePins);
        }
    }
}
