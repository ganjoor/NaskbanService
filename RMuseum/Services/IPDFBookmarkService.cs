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
    }
}
