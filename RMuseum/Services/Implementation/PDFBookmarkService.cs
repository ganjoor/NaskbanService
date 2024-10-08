﻿using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services.Implementation;
using System.Linq;
using System.Threading.Tasks;
using System;
using RMuseum.DbContext;
using Microsoft.EntityFrameworkCore;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using Microsoft.Extensions.Configuration;
namespace RMuseum.Services.Implementation
{
    public class PDFBookmarkService : IPDFBookmarkService
    {
        /// <summary>
        /// Bookmark PDF Book (or one of its pages)
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="userId"></param>
        /// <param name="pageNumber"></param>
        /// <param name="note"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFUserBookmark>> SwitchBookmarkAsync(int pdfBookId, Guid userId, int? pageNumber, string note)
        {
            try
            {
                if (ReadOnlyMode)
                    return new RServiceResult<PDFUserBookmark>(null, "سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

                int? pageId = pageNumber == null ? null : (await _context.PDFPages.AsNoTracking().Where(p => p.PDFBookId == pdfBookId && p.PageNumber == pageNumber).SingleAsync()).Id;
                var alreadyBookmarked = await _context.PDFUserBookmarks.Where(b => b.RAppUserId == userId && b.PDFBookId == pdfBookId && b.PageId == pageId).FirstOrDefaultAsync();
                if (alreadyBookmarked != null)
                {
                    _context.Remove(alreadyBookmarked);
                    await _context.SaveChangesAsync();
                    return new RServiceResult<PDFUserBookmark>(alreadyBookmarked);
                }
                PDFUserBookmark bookmark =
                    new PDFUserBookmark()
                    {
                        RAppUserId = userId,
                        PDFBookId = pdfBookId,
                        PageId = pageId,
                        DateTime = DateTime.Now,
                        Note = note ?? ""
                    };
                _context.PDFUserBookmarks.Add(bookmark);
                await _context.SaveChangesAsync();
                return new RServiceResult<PDFUserBookmark>(bookmark);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFUserBookmark>(null, exp.ToString());
            }

        }
        /// <summary>
        /// get user bookmarks
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <param name="pageNo"></param>
        /// <param name="pdfBookId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFUserBookmarkViewModel[] Bookmarks)>> GetBookmarksAsync(PagingParameterModel paging, Guid userId, int? pdfBookId, int? pageNo)
        {
            try
            {
                int? pageId = pageNo == null || pageNo == 0 ? null : (await _context.PDFPages.AsNoTracking().Where(p => p.PDFBookId == pdfBookId && p.PageNumber == pageNo).SingleAsync()).Id;
                var source =
                _context.PDFUserBookmarks
                .Include(b => b.PDFBook)
                .Include(b => b.Page)
                .Where(b => b.RAppUserId == userId && (pdfBookId == null || (b.PDFBookId == pdfBookId)) && ((pageId == 0 && b.PageId == null) || pageId == null || b.PageId == pageId))
               .OrderByDescending(b => b.DateTime)
               .Select(b => new PDFUserBookmarkViewModel()
               {
                   Id = b.Id,
                   BookTitle = b.PDFBook.Title,
                   BookId = b.PDFBook.Id,
                   PageNumber = b.Page == null ? 0 : b.Page.PageNumber,
                   Note = b.Note,
                   DateTime = b.DateTime,
                   ExtenalImageUrl = b.Page == null ? b.PDFBook.ExtenalCoverImageUrl : b.Page.ExtenalThumbnailImageUrl
               })
               .AsQueryable();
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFUserBookmarkViewModel[] Bookmarks)>(await QueryablePaginator<PDFUserBookmarkViewModel>.Paginate(source, paging));

            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFUserBookmarkViewModel[] Bookmarks)>((null, null), exp.ToString());
            }
        }
        
        /// <summary>
        /// delete all user bookmarks
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteAllBookmarks(Guid userId)
        {
            try
            {
                var bookmarks = await _context.PDFUserBookmarks.Where(b => b.RAppUserId == userId).ToListAsync();
                _context.RemoveRange(bookmarks);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// readonly mode
        /// </summary>
        public bool ReadOnlyMode
        {
            get
            {
                try
                {
                    return bool.Parse(Configuration["ReadOnlyMode"]);
                }
                catch
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// Database Contetxt
        /// </summary>
        protected readonly RMuseumDbContext _context;

        /// <summary>
        /// Configuration
        /// </summary>
        protected IConfiguration Configuration { get; }
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="configuration"></param>
        public PDFBookmarkService(RMuseumDbContext context, IConfiguration configuration)
        {
            _context = context;
            Configuration = configuration;
        }
    }
}
