using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RMuseum.DbContext;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RMuseum.Models.PDFUserTracking;
using RSecurityBackend.Models.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// see IPDFUserSyncService
    /// </summary>
    public class PDFUserSyncService : IPDFUserSyncService
    {
        public async Task<RServiceResult<(DateTime ServerTime, PDFShelfSyncItemViewModel[] Items)>> GetShelfChangesAsync(Guid userId, DateTime since, int take = 500)
        {
            try
            {
                var rows = await _context.PDFShelves
                    .Where(s => s.RAppUserId == userId && s.LastModified >= since)
                    .OrderBy(s => s.LastModified)
                    .Take(take)
                    .ToListAsync();

                var items = rows.Select(s => new PDFShelfSyncItemViewModel()
                {
                    Id = s.Id,
                    Name = s.Name,
                    CreatedAt = s.CreatedAt,
                    ClientModifiedAt = s.ClientModifiedAt,
                    IsDeleted = s.IsDeleted
                }).ToArray();

                DateTime serverTime = rows.Count == take ? rows[rows.Count - 1].LastModified : DateTime.Now;
                return new RServiceResult<(DateTime, PDFShelfSyncItemViewModel[])>((serverTime, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(DateTime, PDFShelfSyncItemViewModel[])>((since, null), exp.ToString());
            }
        }

        /// <summary>
        /// The convenience "default" shelf both clients auto-create on first use - kept in
        /// sync with the Flutter client's own ShelfService.uncategorizedShelfName. See the
        /// duplicate-shelf guard in ApplyShelfChangesAsync below for why this constant exists
        /// server-side at all.
        /// </summary>
        private const string DefaultShelfName = "پیش‌فرض";

        public async Task<RServiceResult<bool>> ApplyShelfChangesAsync(Guid userId, PDFShelfSyncItemViewModel[] items)
        {
            try
            {
                foreach (var item in items ?? Array.Empty<PDFShelfSyncItemViewModel>())
                {
                    var existing = await _context.PDFShelves.Where(s => s.RAppUserId == userId && s.Id == item.Id).FirstOrDefaultAsync();

                    if (existing != null)
                    {
                        if (item.ClientModifiedAt <= existing.ClientModifiedAt)
                            continue; // stale push, server already has a newer state

                        existing.Name = item.Name;
                        existing.ClientModifiedAt = item.ClientModifiedAt;
                        existing.IsDeleted = item.IsDeleted;
                        existing.LastModified = DateTime.Now;
                        _context.Update(existing);
                    }
                    else
                    {
                        if (item.IsDeleted)
                            continue; // nothing to delete, no tombstone needed for a shelf that never existed

                        // Backstop against duplicate "پیش‌فرض" shelves - the actual fix is
                        // client-side (ShelfService._runFirstRunSetupIfNeeded checks the
                        // server before creating one locally), but this catches whatever that
                        // client-side fix can't: older app versions still in the wild, and the
                        // "used the app anonymously, then logged into an account that already
                        // has a default shelf from another device" case, where the client's
                        // own pre-check has no way to know about the other device's shelf yet.
                        // Deliberately just skips the row rather than fully remapping the
                        // client's local id to the existing shelf's id (which would need a
                        // response shape this method doesn't have) - any shelf-books in this
                        // same push that reference the skipped id won't apply, but that's a
                        // narrow, recoverable edge case (the next sync pulls the real shelf
                        // down, and the person can re-add anything that didn't make it) next
                        // to the alternative this guard exists to prevent: permanent, growing
                        // duplicate shelves.
                        if (item.Name == DefaultShelfName)
                        {
                            var alreadyHasDefault = await _context.PDFShelves
                                .AnyAsync(s => s.RAppUserId == userId && s.Name == DefaultShelfName && !s.IsDeleted);
                            if (alreadyHasDefault)
                                continue;
                        }

                        _context.PDFShelves.Add(new PDFShelf()
                        {
                            Id = item.Id,
                            RAppUserId = userId,
                            Name = item.Name,
                            CreatedAt = item.CreatedAt,
                            ClientModifiedAt = item.ClientModifiedAt,
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

        public async Task<RServiceResult<(DateTime ServerTime, PDFShelfBookSyncItemViewModel[] Items)>> GetShelfBookChangesAsync(Guid userId, DateTime since, int take = 500)
        {
            try
            {
                var rows = await _context.PDFShelfBooks
                    .Include(sb => sb.PDFShelf)
                    .Include(sb => sb.PDFBook)
                    .Where(sb => sb.PDFShelf.RAppUserId == userId && sb.LastModified >= since)
                    .OrderBy(sb => sb.LastModified)
                    .Take(take)
                    .ToListAsync();

                var items = rows.Select(sb => new PDFShelfBookSyncItemViewModel()
                {
                    ShelfId = sb.PDFShelfId,
                    BookId = sb.PDFBookId,
                    BookTitle = sb.PDFBook?.Title,
                    ThumbnailUrl = sb.PDFBook?.ExtenalCoverImageUrl,
                    ClientModifiedAt = sb.AddedAt,
                    IsDeleted = sb.IsDeleted
                }).ToArray();

                DateTime serverTime = rows.Count == take ? rows[rows.Count - 1].LastModified : DateTime.Now;
                return new RServiceResult<(DateTime, PDFShelfBookSyncItemViewModel[])>((serverTime, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(DateTime, PDFShelfBookSyncItemViewModel[])>((since, null), exp.ToString());
            }
        }

        public async Task<RServiceResult<bool>> ApplyShelfBookChangesAsync(Guid userId, PDFShelfBookSyncItemViewModel[] items)
        {
            try
            {
                // shelf ids owned by this user, loaded once - membership rows are only ever
                // applied against a shelf the server already knows about (see interface doc)
                var ownedShelfIds = (await _context.PDFShelves.Where(s => s.RAppUserId == userId).Select(s => s.Id).ToListAsync()).ToHashSet();

                foreach (var item in items ?? Array.Empty<PDFShelfBookSyncItemViewModel>())
                {
                    if (!ownedShelfIds.Contains(item.ShelfId))
                        continue;

                    var existing = await _context.PDFShelfBooks.Where(sb => sb.PDFShelfId == item.ShelfId && sb.PDFBookId == item.BookId).FirstOrDefaultAsync();

                    if (existing != null)
                    {
                        if (item.ClientModifiedAt <= existing.AddedAt)
                            continue;

                        existing.AddedAt = item.ClientModifiedAt;
                        existing.IsDeleted = item.IsDeleted;
                        existing.LastModified = DateTime.Now;
                        _context.Update(existing);
                    }
                    else
                    {
                        if (item.IsDeleted)
                            continue;

                        _context.PDFShelfBooks.Add(new PDFShelfBook()
                        {
                            Id = Guid.NewGuid(),
                            PDFShelfId = item.ShelfId,
                            PDFBookId = item.BookId,
                            AddedAt = item.ClientModifiedAt,
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

        public async Task<RServiceResult<(DateTime ServerTime, PDFStudyLogSyncItemViewModel[] Items)>> GetStudyLogChangesAsync(Guid userId, DateTime since, int take = 500)
        {
            try
            {
                var rows = await _context.PDFStudyLogEntries
                    .Include(e => e.PDFBook)
                    .Where(e => e.RAppUserId == userId && e.LastModified >= since)
                    .OrderBy(e => e.LastModified)
                    .Take(take)
                    .ToListAsync();

                var items = rows.Select(e => new PDFStudyLogSyncItemViewModel()
                {
                    Id = e.Id,
                    BookId = e.PDFBookId,
                    BookTitle = e.PDFBook?.Title,
                    PageNumber = e.PageNumber,
                    Timestamp = e.Timestamp,
                    IsDeleted = e.IsDeleted
                }).ToArray();

                DateTime serverTime = rows.Count == take ? rows[rows.Count - 1].LastModified : DateTime.Now;
                return new RServiceResult<(DateTime, PDFStudyLogSyncItemViewModel[])>((serverTime, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(DateTime, PDFStudyLogSyncItemViewModel[])>((since, null), exp.ToString());
            }
        }

        public async Task<RServiceResult<bool>> ApplyStudyLogChangesAsync(Guid userId, PDFStudyLogSyncItemViewModel[] items)
        {
            try
            {
                foreach (var item in items ?? Array.Empty<PDFStudyLogSyncItemViewModel>())
                {
                    // append-only content - identified purely by the client-generated Id, so
                    // this is always an idempotent upsert, never a content comparison
                    var existing = await _context.PDFStudyLogEntries.Where(e => e.RAppUserId == userId && e.Id == item.Id).FirstOrDefaultAsync();

                    if (existing != null)
                    {
                        if (existing.IsDeleted == item.IsDeleted)
                            continue; // already applied, nothing changed

                        existing.IsDeleted = item.IsDeleted;
                        existing.LastModified = DateTime.Now;
                        _context.Update(existing);
                    }
                    else
                    {
                        if (item.IsDeleted)
                            continue; // nothing to delete, no tombstone needed for an entry that never existed

                        _context.PDFStudyLogEntries.Add(new PDFStudyLogEntry()
                        {
                            Id = item.Id,
                            RAppUserId = userId,
                            PDFBookId = item.BookId,
                            PageNumber = item.PageNumber,
                            Timestamp = item.Timestamp,
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

        public async Task<RServiceResult<PDFReadingPositionViewModel[]>> GetReadingPositionsAsync(Guid userId)
        {
            try
            {
                // GroupBy + an ordered First() per group is a known EF Core translation risk
                // (see GetAuthorsWithBookCountAsync's own history of this) - a plain GROUP BY
                // for the max timestamp, joined back to fetch that row's PageNumber, is the
                // well-supported equivalent.
                var maxByBook = _context.PDFStudyLogEntries
                    .Where(e => e.RAppUserId == userId && !e.IsDeleted)
                    .GroupBy(e => e.PDFBookId)
                    .Select(g => new { BookId = g.Key, LastReadAt = g.Max(e => e.Timestamp) });

                var entries = _context.PDFStudyLogEntries.Where(e => e.RAppUserId == userId && !e.IsDeleted);

                var joined = await (
                    from m in maxByBook
                    join e in entries
                        on new { m.BookId, m.LastReadAt } equals new { BookId = e.PDFBookId, LastReadAt = e.Timestamp }
                    select new PDFReadingPositionViewModel()
                    {
                        BookId = m.BookId,
                        LastPageNumber = e.PageNumber,
                        LastReadAt = m.LastReadAt
                    })
                    .ToListAsync();

                // two entries for the same book at the exact same Timestamp (rare - e.g. two
                // devices syncing the same instant) would otherwise join to more than one row
                var positions = joined
                    .GroupBy(p => p.BookId)
                    .Select(g => g.First())
                    .OrderByDescending(p => p.LastReadAt)
                    .ToList();

                // enriched with BookTitle/ThumbnailUrl the same way GetUserLastActivityAsync
                // does - a per-position lookup rather than a single bigger join, since this
                // list is at most one row per book the user has ever read, not a candidate for
                // the same N+1 concern a per-page-view query would be
                List<PDFReadingPositionViewModel> result = new List<PDFReadingPositionViewModel>();
                foreach (var position in positions)
                {
                    var pdf = await _context.PDFBooks.AsNoTracking().Where(p => p.Id == position.BookId).FirstOrDefaultAsync();
                    if (pdf == null)
                        continue;

                    var page = await _context.PDFPages.AsNoTracking().Where(p => p.PDFBookId == position.BookId && p.PageNumber == position.LastPageNumber).FirstOrDefaultAsync();

                    result.Add(new PDFReadingPositionViewModel()
                    {
                        BookId = position.BookId,
                        LastPageNumber = position.LastPageNumber,
                        LastReadAt = position.LastReadAt,
                        BookTitle = pdf.Title,
                        ThumbnailUrl = page != null ? page.ExtenalThumbnailImageUrl : pdf.ExtenalCoverImageUrl
                    });
                }

                return new RServiceResult<PDFReadingPositionViewModel[]>(result.ToArray());
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFReadingPositionViewModel[]>(null, exp.ToString());
            }
        }

        public async Task<RServiceResult<bool>> RecordStudyLogEntryAsync(Guid userId, int pdfBookId, int pageNumber)
        {
            try
            {
                _context.PDFStudyLogEntries.Add(new PDFStudyLogEntry()
                {
                    Id = Guid.NewGuid(),
                    RAppUserId = userId,
                    PDFBookId = pdfBookId,
                    PageNumber = pageNumber,
                    Timestamp = DateTime.Now,
                    LastModified = DateTime.Now,
                    IsDeleted = false
                });
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        public async Task<RServiceResult<(DateTime ServerTime, PDFPinnedAuthorSyncItemViewModel[] Items)>> GetPinnedAuthorChangesAsync(Guid userId, DateTime since, int take = 500)
        {
            try
            {
                var rows = await _context.PDFPinnedAuthors
                    .Include(p => p.Author)
                    .Where(p => p.RAppUserId == userId && p.LastModified >= since)
                    .OrderBy(p => p.LastModified)
                    .Take(take)
                    .ToListAsync();

                var items = rows.Select(p => new PDFPinnedAuthorSyncItemViewModel()
                {
                    AuthorId = p.AuthorId,
                    AuthorName = p.Author?.Name,
                    ClientModifiedAt = p.PinnedAt,
                    IsDeleted = p.IsDeleted
                }).ToArray();

                DateTime serverTime = rows.Count == take ? rows[rows.Count - 1].LastModified : DateTime.Now;
                return new RServiceResult<(DateTime, PDFPinnedAuthorSyncItemViewModel[])>((serverTime, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(DateTime, PDFPinnedAuthorSyncItemViewModel[])>((since, null), exp.ToString());
            }
        }

        public async Task<RServiceResult<bool>> ApplyPinnedAuthorChangesAsync(Guid userId, PDFPinnedAuthorSyncItemViewModel[] items)
        {
            try
            {
                foreach (var item in items ?? Array.Empty<PDFPinnedAuthorSyncItemViewModel>())
                {
                    var existing = await _context.PDFPinnedAuthors.Where(p => p.RAppUserId == userId && p.AuthorId == item.AuthorId).FirstOrDefaultAsync();

                    if (existing != null)
                    {
                        if (item.ClientModifiedAt <= existing.PinnedAt)
                            continue; // stale push, server already has a newer state

                        existing.PinnedAt = item.ClientModifiedAt;
                        existing.IsDeleted = item.IsDeleted;
                        existing.LastModified = DateTime.Now;
                        _context.Update(existing);
                    }
                    else
                    {
                        if (item.IsDeleted)
                            continue; // nothing to delete, no tombstone needed for a row that never existed

                        _context.PDFPinnedAuthors.Add(new PDFPinnedAuthor()
                        {
                            Id = Guid.NewGuid(),
                            RAppUserId = userId,
                            AuthorId = item.AuthorId,
                            PinnedAt = item.ClientModifiedAt,
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
        /// Database Context
        /// </summary>
        protected readonly RMuseumDbContext _context;

        /// <summary>
        /// Configuration
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <summary>
        /// constructor
        /// </summary>
        public PDFUserSyncService(RMuseumDbContext context, IConfiguration configuration)
        {
            _context = context;
            Configuration = configuration;
        }
    }
}
