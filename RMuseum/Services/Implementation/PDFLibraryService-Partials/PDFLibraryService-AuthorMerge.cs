using Microsoft.EntityFrameworkCore;
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

                await _RepointAuthorRoleRowsAsync(survivor, duplicate);
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
        /// Queries AuthorRole directly by AuthorId rather than reaching it through a parent's
        /// collection navigation (PDFBook.Contributers or Book.Authors, the two earlier
        /// attempts at this method). AuthorRole's own PDFBookId/BookId are both shadow
        /// properties - not even visible on the C# class, only in the database schema - and
        /// both are nullable, so a role row missing whichever one a given collection-navigation
        /// query relies on (or, worse, a row with neither set at all - a genuinely orphaned
        /// row) is invisible to that query and silently left behind, still pointing at the
        /// duplicate, blocking the final Authors.Remove below with the exact same FK violation
        /// every time - which is what happened twice even after fixing the two collection-based
        /// paths one at a time. Querying the role rows directly closes that off regardless of
        /// which parent (or no parent) a given row turns out to belong to.
        ///
        /// For each duplicate role: dropped (explicit _context.Remove, not detached from a
        /// collection - see the note further down on why that distinction matters) if the
        /// survivor already has an equivalent role on the same owner (matched by whichever of
        /// PDFBookId/BookId that role actually has), otherwise repointed onto the survivor.
        /// Every affected PDFBook's free-text AuthorsLine/TranslatorsLine also gets the
        /// duplicate's exact name swapped for the survivor's - those fields get re-parsed into
        /// AuthorRole rows again on that book's *next* edit (see
        /// EditPDFBookMasterRecordAsync's contributor-sync calls), and leaving the old spelling
        /// in the text would silently recreate the very duplicate this merge just removed.
        /// </summary>
        private async Task _RepointAuthorRoleRowsAsync(Author survivor, Author duplicate)
        {
            var duplicateRoles = await _context.Set<AuthorRole>()
                .Where(r => r.Author.Id == duplicate.Id)
                .ToListAsync();

            if (duplicateRoles.Count == 0)
                return;

            var survivorRoles = await _context.Set<AuthorRole>()
                .Where(r => r.Author.Id == survivor.Id)
                .ToListAsync();

            // EF.Property<T>() only works inside a live LINQ-to-Entities expression being
            // translated to SQL - both role lists are already materialized (post-ToListAsync,
            // plain LINQ-to-Objects from here on), so shadow property values have to come from
            // the change tracker's own Entry(...).Property(...) API instead, which works on any
            // already-tracked entity regardless of query context.
            int? PdfBookIdOf(AuthorRole r) => (int?)_context.Entry(r).Property("PDFBookId").CurrentValue;
            int? BookIdOf(AuthorRole r) => (int?)_context.Entry(r).Property("BookId").CurrentValue;

            var survivorRoleKeys = new HashSet<(int? pdfBookId, int? bookId, string role)>(
                survivorRoles.Select(r => (PdfBookIdOf(r), BookIdOf(r), r.Role)));

            var affectedPdfBookIds = new HashSet<int>();

            foreach (var role in duplicateRoles)
            {
                int? pdfBookId = PdfBookIdOf(role);
                int? bookId = BookIdOf(role);

                bool survivorAlreadyHasThisRole = survivorRoleKeys.Contains((pdfBookId, bookId, role.Role));
                if (survivorAlreadyHasThisRole)
                {
                    // explicit delete on the tracked entity itself, not
                    // book.Contributers.Remove(role)/book.Authors.Remove(role) - removing a
                    // dependent from a collection navigation only detaches it in memory, it
                    // does not reliably issue a DELETE unless the relationship happens to be
                    // configured to cascade-delete orphans, which is not something to assume
                    // for a shadow-FK-only entity like this one. This is what actually caused
                    // the very first fix attempt to keep failing on the same FK constraint even
                    // though duplicate contributions were supposedly already "removed" - the
                    // rows were still sitting in the database, untouched.
                    _context.Remove(role);
                }
                else
                {
                    role.Author = survivor;
                }

                if (pdfBookId.HasValue)
                    affectedPdfBookIds.Add(pdfBookId.Value);
            }

            if (affectedPdfBookIds.Count > 0 && !string.IsNullOrEmpty(duplicate.Name))
            {
                string survivorName = survivor.Name ?? "";
                var affectedPdfBooks = await _context.PDFBooks
                    .Where(b => affectedPdfBookIds.Contains(b.Id))
                    .ToListAsync();

                foreach (var book in affectedPdfBooks)
                {
                    if (!string.IsNullOrEmpty(book.AuthorsLine))
                        book.AuthorsLine = book.AuthorsLine.Replace(duplicate.Name, survivorName);
                    if (!string.IsNullOrEmpty(book.TranslatorsLine))
                        book.TranslatorsLine = book.TranslatorsLine.Replace(duplicate.Name, survivorName);
                }

                _context.PDFBooks.UpdateRange(affectedPdfBooks);
            }
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
