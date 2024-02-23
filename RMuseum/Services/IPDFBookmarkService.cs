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
        /// <param name="pageId"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFUserBookmark>> SwitchBookmarkAsync(int pdfBookId, Guid userId, int? pageId);

        /// <summary>
        /// get user bookmarks
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <param name="pageId"></param>
        /// <param name="pdfBookId"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFUserBookmarkViewModel[] Bookmarks)>> GetBookmarksAsync(PagingParameterModel paging, Guid userId, int? pdfBookId, int? pageId);
    }
}
