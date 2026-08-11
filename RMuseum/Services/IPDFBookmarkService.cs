using RMuseum.Models.PDFLibrary;
using RSecurityBackend.Models.Generic;
using System.Threading.Tasks;
using System;
using RMuseum.Models.PDFLibrary.ViewModels;

namespace RMuseum.Services
{
    public interface IPDFBookmarkService
    {
        /// <summary>
        /// Bookmark PDF Book (or one of its pages)
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="userId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="note"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFUserBookmark>> SwitchBookmarkAsync(int pdfBookId, Guid userId, int? pageNumber, string note);

        /// <summary>
        /// get user bookmarks
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <param name="pageNo"></param>
        /// <param name="pdfBookId"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFUserBookmarkViewModel[] Bookmarks)>> GetBookmarksAsync(PagingParameterModel paging, Guid userId, int? pdfBookId, int? pageNo);

        /// <summary>
        /// delete all user bookmarks
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeleteAllBookmarks(Guid userId);

        /// <summary>
        /// bookmarks changed (created/edited/toggled off) for this user since a given server
        /// time - a sync pull. Includes tombstones (IsDeleted rows) so another device can learn
        /// about deletions. If the result is capped (see take), ServerTime is the LastModified
        /// of the last included row rather than "now", so calling again with that as `since`
        /// continues where this call left off instead of skipping anything.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="since"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        Task<RServiceResult<(DateTime ServerTime, PDFBookmarkSyncItemViewModel[] Items)>> GetBookmarkSyncChangesAsync(Guid userId, DateTime since, int take = 500);

        /// <summary>
        /// applies a batch of client-side bookmark changes (a sync push) - last-write-wins per
        /// item by comparing ClientModifiedAt against the stored bookmark's own DateTime; a
        /// push older than what the server already has is silently ignored (not an error), so
        /// the caller doesn't need to pre-filter its own stale items.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> ApplyBookmarkSyncChangesAsync(Guid userId, PDFBookmarkSyncItemViewModel[] items);
    }
}
