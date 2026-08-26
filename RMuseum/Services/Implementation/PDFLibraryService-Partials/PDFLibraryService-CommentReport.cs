using Microsoft.EntityFrameworkCore;
using RMuseum.Models.Artifact;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Models.Notification;
using RSecurityBackend.Services;
using RSecurityBackend.Services.Implementation;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// submit a report against a page comment (spam, offensive, harassment, or something
        /// else) - any authenticated user other than the comment's own author (if you want your
        /// own comment gone, delete it directly - no report needed). Same notify-every-moderator
        /// pattern as SubmitPDFBookReportAsync - save first, then look up every user holding
        /// PDFPageCommentReportEntityShortName:ModerateOperationShortName and notify each; a
        /// failure in that step is swallowed rather than failing the submission itself.
        /// </summary>
        public async Task<RServiceResult<Guid>> SubmitPDFPageCommentReportAsync(Guid reporterId, Guid commentId, PDFPageCommentReportSubmitViewModel model)
        {
            try
            {
                var comment = await _context.PDFPageComments.AsNoTracking().Where(c => c.Id == commentId).SingleOrDefaultAsync();
                if (comment == null || comment.Status != PublishStatus.Published)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "این دیدگاه یافت نشد یا دیگر در دسترس نیست");
                }
                if (comment.UserId == reporterId)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "امکان گزارش دیدگاه خودتان وجود ندارد");
                }

                var alreadyReported = await _context.PDFPageCommentReports.AsNoTracking()
                    .AnyAsync(r => r.PDFPageCommentId == commentId && r.ReporterId == reporterId && r.Status == PDFPageCommentReportStatus.Open);
                if (alreadyReported)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "شما پیش‌تر این دیدگاه را گزارش کرده‌اید");
                }

                var report = new PDFPageCommentReport()
                {
                    Id = Guid.NewGuid(),
                    PDFPageCommentId = commentId,
                    ReporterId = reporterId,
                    Category = model.Category,
                    Description = model.Description,
                    Status = PDFPageCommentReportStatus.Open,
                    CreatedAt = DateTime.Now,
                };
                _context.PDFPageCommentReports.Add(report);
                await _context.SaveChangesAsync();

                var moderators = await _appUserService.GetUsersHavingPermission(RMuseumSecurableItem.PDFPageCommentReportEntityShortName, RMuseumSecurableItem.ModerateOperationShortName);
                if (string.IsNullOrEmpty(moderators.ExceptionString)) // if not, do nothing - see doc comment above
                {
                    foreach (var moderator in moderators.Result)
                    {
                        await _notificationService.PushNotification
                                        (
                                            (Guid)moderator.Id,
                                            "گزارش تازه دربارهٔ یک دیدگاه",
                                            $"کاربری گزارشی دربارهٔ یک دیدگاه ثبت کرده است. لطفاً بخش گزارش‌های دیدگاه‌ها را بررسی فرمایید.{Environment.NewLine}" +
                                            $"توجه فرمایید که اگر کاربر دیگری که دارای مجوز بررسی گزارش‌هاست پیش از شما به آن رسیدگی کرده باشد آن را در صف نخواهید دید.",
                                            NotificationType.ActionRequired
                                        );
                    }
                }

                return new RServiceResult<Guid>(report.Id);
            }
            catch (Exception exp)
            {
                return new RServiceResult<Guid>(Guid.Empty, exp.ToString());
            }
        }

        /// <summary>
        /// paginated list of still-open comment reports, for reviewers - includes enough of the
        /// reported comment's own context (text, book, page) to review without a follow-up call,
        /// and gracefully covers a report whose comment is already gone by the time it's looked
        /// at (moderated directly, deleted by its own author, or resolved by another reviewer
        /// who got to it first) via CommentStillExists, rather than erroring or showing blanks.
        /// </summary>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFPageCommentReportViewModel[] Items)>> GetOpenPDFPageCommentReportsAsync(PagingParameterModel paging)
        {
            try
            {
                var source = _context.PDFPageCommentReports.AsNoTracking()
                    .Include(r => r.PDFPageComment).ThenInclude(c => c.User)
                    .Include(r => r.PDFPageComment).ThenInclude(c => c.PDFPage)
                    .Include(r => r.Reporter)
                    .Where(r => r.Status == PDFPageCommentReportStatus.Open)
                    .OrderByDescending(r => r.CreatedAt);

                (PaginationMetadata PagingMeta, PDFPageCommentReport[] Items) paginatedResult =
                    await QueryablePaginator<PDFPageCommentReport>.Paginate(source, paging);

                var bookIds = paginatedResult.Items.Select(r => r.PDFPageComment.PDFPage.PDFBookId).Distinct().ToArray();
                var bookTitles = await _context.PDFBooks.AsNoTracking()
                    .Where(b => bookIds.Contains(b.Id))
                    .Select(b => new { b.Id, b.Title })
                    .ToDictionaryAsync(b => b.Id, b => b.Title);

                var items = paginatedResult.Items.Select(r =>
                {
                    var commentStillExists = r.PDFPageComment != null && r.PDFPageComment.Status == PublishStatus.Published;
                    var bookId = r.PDFPageComment.PDFPage.PDFBookId;
                    return new PDFPageCommentReportViewModel()
                    {
                        Id = r.Id,
                        PDFPageCommentId = r.PDFPageCommentId,
                        CommentStillExists = commentStillExists,
                        CommentText = commentStillExists ? r.PDFPageComment.Text : null,
                        CommentAuthorId = r.PDFPageComment.UserId,
                        CommentAuthorName = commentStillExists ? r.PDFPageComment.User.NickName : null,
                        PDFBookId = bookId,
                        BookTitle = bookTitles.TryGetValue(bookId, out var title) ? title : null,
                        PageNumber = r.PDFPageComment.PDFPage.PageNumber,
                        ReporterId = r.ReporterId,
                        ReporterName = r.Reporter.NickName,
                        Category = r.Category,
                        Description = r.Description,
                        Status = r.Status,
                        Approved = r.Approved,
                        CreatedAt = r.CreatedAt,
                        ReviewerResponse = r.ReviewerResponse,
                        ReviewedAt = r.ReviewedAt,
                    };
                }).ToArray();

                return new RServiceResult<(PaginationMetadata, PDFPageCommentReportViewModel[])>((paginatedResult.PagingMeta, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata, PDFPageCommentReportViewModel[])>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// resolve a comment report - approving it deletes the reported comment (soft-delete,
        /// same as DeletePDFPageCommentAsync) as part of closing the report; rejecting it leaves
        /// the comment untouched. Either way the reporter is notified with the outcome and the
        /// reviewer's optional note - a resolution nobody sees defeats the point of writing one,
        /// same reasoning as ClosePDFBookReportAsync. Safe to call even if the comment has
        /// already been deleted by some other means since the report was filed (moderated
        /// directly, deleted by its own author, or already resolved via a duplicate report) -
        /// approving in that case is a no-op on the comment itself, not an error.
        /// </summary>
        public async Task<RServiceResult<bool>> ResolvePDFPageCommentReportAsync(Guid reviewerId, Guid reportId, bool approved, string response)
        {
            try
            {
                var report = await _context.PDFPageCommentReports.Include(r => r.PDFPageComment).Where(r => r.Id == reportId).SingleOrDefaultAsync();
                if (report == null)
                {
                    return new RServiceResult<bool>(false, $"report {reportId} not found");
                }
                if (report.Status == PDFPageCommentReportStatus.Closed)
                {
                    return new RServiceResult<bool>(false, "این گزارش پیش‌تر بررسی شده است");
                }

                bool actuallyDeleted = false;
                if (approved && report.PDFPageComment != null && report.PDFPageComment.Status == PublishStatus.Published)
                {
                    report.PDFPageComment.Status = PublishStatus.Deleted;
                    _context.Update(report.PDFPageComment);
                    actuallyDeleted = true;
                }

                report.Status = PDFPageCommentReportStatus.Closed;
                report.Approved = approved;
                report.ReviewerId = reviewerId;
                report.ReviewerResponse = response;
                report.ReviewedAt = DateTime.Now;
                _context.Update(report);
                await _context.SaveChangesAsync();

                await _notificationService.PushNotification
                                (
                                    report.ReporterId,
                                    "نتیجهٔ بررسی گزارش شما",
                                    (approved
                                        ? "گزارش شما دربارهٔ یک دیدگاه بررسی و پذیرفته شد؛ دیدگاه حذف شد."
                                        : "گزارش شما دربارهٔ یک دیدگاه بررسی شد؛ دیدگاه حذف نشد.") +
                                    (string.IsNullOrWhiteSpace(response) ? "" : $"{Environment.NewLine}پاسخ بررسی‌کننده: {response}")
                                );

                // the comment's own author is told it was removed and why - deliberately at
                // category level, not the reporter's own free-text description or identity;
                // showing the raw complaint or who filed it risks retaliation against the
                // reporter, which is exactly what report anonymity is meant to prevent
                if (actuallyDeleted)
                {
                    await _notificationService.PushNotification
                                    (
                                        report.PDFPageComment.UserId,
                                        "حذف یکی از دیدگاه‌های شما",
                                        $"یکی از دیدگاه‌های شما به دلیل «{_CommentReportCategoryLabel(report.Category)}» و پس از بررسی حذف شد." +
                                        (string.IsNullOrWhiteSpace(response) ? "" : $"{Environment.NewLine}پاسخ بررسی‌کننده: {response}")
                                    );
                }

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// Persian label for a report category - same fixed set as the Flutter client's own
        /// kCommentReportCategories map, kept in sync manually since the category itself is a
        /// plain string on both ends rather than a shared enum (see
        /// PDFPageCommentReport.Category's own doc comment on why)
        /// </summary>
        private static string _CommentReportCategoryLabel(string category)
        {
            switch (category)
            {
                case "Spam": return "هرزنامه";
                case "Offensive": return "توهین‌آمیز";
                case "Harassment": return "آزار و اذیت";
                default: return "سایر";
            }
        }
    }
}
