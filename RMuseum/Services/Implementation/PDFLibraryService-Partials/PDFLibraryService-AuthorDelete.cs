using Microsoft.EntityFrameworkCore;
using RMuseum.Models.PDFLibrary;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// Same direct-by-AuthorId query as _RepointAuthorRoleRowsAsync (see that method's own
        /// doc comment for why AuthorRole is queried directly rather than through
        /// PDFBook.Contributers/Book.Authors - the same reasoning and the same risk of a missed
        /// path applies here). Every matching role is simply deleted, not repointed - there is no
        /// survivor for a plain delete - and no PDFBook/Book text is touched. Used by
        /// DeleteAuthorAsync (in PDFLibraryService.cs, the pre-existing delete-author method -
        /// this used to be its own separate DeleteAuthorByIdAsync/author/{authorId} route until
        /// that turned out to duplicate and route-conflict with the already-existing
        /// DeleteAuthorAsync/author/{id}; consolidated into that one instead of running two).
        /// </summary>
        private async Task _RemoveAuthorRoleRowsAsync(int authorId)
        {
            var roles = await _context.Set<AuthorRole>()
                .Where(r => r.Author.Id == authorId)
                .ToListAsync();

            foreach (var role in roles)
            {
                _context.Remove(role);
            }
        }

        /// <summary>
        /// every pin on this author is tombstoned (IsDeleted = true), never hard-deleted - same
        /// reasoning as _RepointAuthorPinsAsync's own doc comment: a hard delete would leave
        /// another device's already-pulled local pin dangling forever, since a future sync pull
        /// would never mention this row again to tell that device to remove it. Unlike merge,
        /// there is no survivor to carry the pin forward onto - the author is just gone. Used by
        /// DeleteAuthorAsync in PDFLibraryService.cs.
        /// </summary>
        private async Task _RemoveAuthorPinsAsync(int authorId)
        {
            var pins = await _context.PDFPinnedAuthors
                .Where(p => p.AuthorId == authorId && !p.IsDeleted)
                .ToListAsync();

            foreach (var pin in pins)
            {
                pin.IsDeleted = true;
                pin.LastModified = DateTime.Now;
            }

            _context.PDFPinnedAuthors.UpdateRange(pins);
        }
    }
}
