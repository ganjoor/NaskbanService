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
        /// delete an Author record by id - for a generic/placeholder credit (e.g. "جمعی از
        /// نویسندگان") that shouldn't exist as a browsable/searchable author entry, but whose
        /// exact wording is still worth keeping on the books it's credited on. Same permission
        /// policy as PDFBook delete/PDFBook merge/Author merge.
        ///
        /// Deliberately does NOT touch any PDFBook's AuthorsLine/TranslatorsLine - this is the
        /// one thing that makes this different from a merge with nowhere to repoint to. A
        /// generic placeholder name still has real value as descriptive text on the book itself
        /// ("نویسنده: جمعی از نویسندگان" is meaningful to a reader) even though it has none as
        /// a structured, searchable Author entity (nobody is usefully served by an "author" whose
        /// entire browsable identity is "a group of writers"). Merge doesn't have this exception
        /// because merge always has a specific, named survivor to substitute in; delete doesn't -
        /// there's nothing meaningful to replace the text with, so it's left as-is.
        /// </summary>
        /// <param name="authorId">the Author id to delete</param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteAuthorByIdAsync(int authorId)
        {
            try
            {
                var author = await _context.Authors.Where(a => a.Id == authorId).SingleOrDefaultAsync();
                if (author == null)
                {
                    return new RServiceResult<bool>(false, $"author {authorId} not found");
                }

                await _RemoveAuthorRoleRowsAsync(authorId);
                await _RemoveAuthorPinsAsync(authorId);

                _context.Authors.Remove(author);

                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// Same direct-by-AuthorId query as _RepointAuthorRoleRowsAsync (see that method's own
        /// doc comment for why AuthorRole is queried directly rather than through
        /// PDFBook.Contributers/Book.Authors - the same reasoning and the same risk of a missed
        /// path applies here). Every matching role is simply deleted, not repointed - there is no
        /// survivor for a plain delete - and no PDFBook/Book text is touched, per this file's own
        /// top-level doc comment on why that's the deliberate difference from merge.
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
        /// there is no survivor to carry the pin forward onto - the author is just gone.
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
