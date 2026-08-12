using RSecurityBackend.Models.Generic;
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
                // includes soft-deleted rows, not just !IsDeleted - reactivating a tombstone in
                // place preserves it as the same sync identity instead of a hard delete
                // followed by a fresh insert, which would otherwise look like two separate
                // changes to a device syncing later
                var existing = await _context.PDFUserBookmarks.Where(b => b.RAppUserId == userId && b.PDFBookId == pdfBookId && b.PageId == pageId).FirstOrDefaultAsync();
                if (existing != null && !existing.IsDeleted)
                {
                    existing.IsDeleted = true;
                    existing.DateTime = DateTime.Now;
                    existing.LastModified = DateTime.Now;
                    _context.Update(existing);
                    await _context.SaveChangesAsync();
                    return new RServiceResult<PDFUserBookmark>(existing);
                }
                if (existing != null)
                {
                    existing.IsDeleted = false;
                    existing.Note = note ?? "";
                    existing.DateTime = DateTime.Now;
                    existing.LastModified = DateTime.Now;
                    _context.Update(existing);
                    await _context.SaveChangesAsync();
                    return new RServiceResult<PDFUserBookmark>(existing);
                }
                PDFUserBookmark bookmark =
                    new PDFUserBookmark()
                    {
                        RAppUserId = userId,
                        PDFBookId = pdfBookId,
                        PageId = pageId,
                        DateTime = DateTime.Now,
                        LastModified = DateTime.Now,
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
                .Where(b => b.RAppUserId == userId && !b.IsDeleted && (pdfBookId == null || (b.PDFBookId == pdfBookId)) && ((pageId == 0 && b.PageId == null) || pageId == null || b.PageId == pageId))
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
                // soft-deleted (not RemoveRange'd) so a device syncing later learns these were
                // removed instead of silently seeing nothing
                var bookmarks = await _context.PDFUserBookmarks.Where(b => b.RAppUserId == userId && !b.IsDeleted).ToListAsync();
                var now = DateTime.Now;
                foreach (var bookmark in bookmarks)
                {
                    bookmark.IsDeleted = true;
                    bookmark.DateTime = now;
                    bookmark.LastModified = now;
                }
                _context.UpdateRange(bookmarks);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// bookmarks changed for this user since a given server time - a sync pull
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="since"></param>
        /// <param name="take"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(DateTime ServerTime, PDFBookmarkSyncItemViewModel[] Items)>> GetBookmarkSyncChangesAsync(Guid userId, DateTime since, int take = 500)
        {
            try
            {
                var rows = await _context.PDFUserBookmarks
                    .Include(b => b.Page)
                    .Where(b => b.RAppUserId == userId && b.LastModified >= since)
                    .OrderBy(b => b.LastModified)
                    .Take(take)
                    .ToListAsync();

                var items = rows.Select(b => new PDFBookmarkSyncItemViewModel()
                {
                    BookId = b.PDFBookId ?? 0,
                    PageNumber = b.Page == null ? 0 : b.Page.PageNumber,
                    Note = b.Note,
                    ClientModifiedAt = b.DateTime,
                    IsDeleted = b.IsDeleted
                }).ToArray();

                // capped -> resume from the last included row's own LastModified next time,
                // rather than "now" (which could skip rows written between this query and now)
                DateTime serverTime = rows.Count == take ? rows[rows.Count - 1].LastModified : DateTime.Now;

                return new RServiceResult<(DateTime, PDFBookmarkSyncItemViewModel[])>((serverTime, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(DateTime, PDFBookmarkSyncItemViewModel[])>((since, null), exp.ToString());
            }
        }

        /// <summary>
        /// applies a batch of client-side bookmark changes - a sync push
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> ApplyBookmarkSyncChangesAsync(Guid userId, PDFBookmarkSyncItemViewModel[] items)
        {
            try
            {
                if (ReadOnlyMode)
                    return new RServiceResult<bool>(false, "سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

                foreach (var item in items ?? Array.Empty<PDFBookmarkSyncItemViewModel>())
                {
                    int? pageId = item.PageNumber == 0
                        ? null
                        : (await _context.PDFPages.AsNoTracking().Where(p => p.PDFBookId == item.BookId && p.PageNumber == item.PageNumber).SingleOrDefaultAsync())?.Id;

                    var existing = await _context.PDFUserBookmarks.Where(b => b.RAppUserId == userId && b.PDFBookId == item.BookId && b.PageId == pageId).FirstOrDefaultAsync();

                    if (existing != null)
                    {
                        // stale push - the server already has a change newer than this one, so
                        // this item is simply not applied (not an error, no ack of its own needed:
                        // the client will see the server's newer state on its next pull)
                        if (item.ClientModifiedAt <= existing.DateTime)
                            continue;

                        existing.Note = item.Note ?? "";
                        existing.DateTime = item.ClientModifiedAt;
                        existing.IsDeleted = item.IsDeleted;
                        existing.LastModified = DateTime.Now;
                        _context.Update(existing);
                    }
                    else
                    {
                        if (item.IsDeleted)
                            continue; // nothing to delete, no tombstone needed for a row that never existed

                        _context.PDFUserBookmarks.Add(new PDFUserBookmark()
                        {
                            RAppUserId = userId,
                            PDFBookId = item.BookId,
                            PageId = pageId,
                            Note = item.Note ?? "",
                            DateTime = item.ClientModifiedAt,
                            LastModified = DateTime.Now,
                            IsDeleted = false
                        });
                    }
                }

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
