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
        /// submit a report against a book review (spam, offensive, harassment, or something
        /// else) - any authenticated user other than the review's own author (if you want your
        /// own review gone, delete it directly - no report needed). Mirrors
        /// SubmitPDFPageCommentReportAsync exactly, including the notify-every-moderator
        /// pattern - save first, then look up every user holding
        /// PDFBookReviewReportEntityShortName:ModerateOperationShortName and notify each; a
        /// failure in that step is swallowed rather than failing the submission itself.
        /// </summary>
        public async Task<RServiceResult<Guid>> SubmitPDFBookReviewReportAsync(Guid reporterId, Guid reviewId, PDFBookReviewReportSubmitViewModel model)
        {
            try
            {
                var review = await _context.PDFBookReviews.AsNoTracking().Where(r => r.Id == reviewId).SingleOrDefaultAsync();
                if (review == null || review.Status != PublishStatus.Published)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "این نقد یافت نشد یا دیگر در دسترس نیست");
                }
                if (review.UserId == reporterId)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "امکان گزارش نقد خودتان وجود ندارد");
                }

                var alreadyReported = await _context.PDFBookReviewReports.AsNoTracking()
                    .AnyAsync(r => r.PDFBookReviewId == reviewId && r.ReporterId == reporterId && r.Status == PDFBookReviewReportStatus.Open);
                if (alreadyReported)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "شما پیش‌تر این نقد را گزارش کرده‌اید");
                }

                var report = new PDFBookReviewReport()
                {
                    Id = Guid.NewGuid(),
                    PDFBookReviewId = reviewId,
                    ReporterId = reporterId,
                    Category = model.Category,
                    Description = model.Description,
                    Status = PDFBookReviewReportStatus.Open,
                    CreatedAt = DateTime.Now,
                };
                _context.PDFBookReviewReports.Add(report);
                await _context.SaveChangesAsync();

                var moderators = await _appUserService.GetUsersHavingPermission(RMuseumSecurableItem.PDFBookReviewReportEntityShortName, RMuseumSecurableItem.ModerateOperationShortName);
                if (string.IsNullOrEmpty(moderators.ExceptionString)) // if not, do nothing - see doc comment above
                {
                    foreach (var moderator in moderators.Result)
                    {
                        await _notificationService.PushNotification
                                        (
                                            (Guid)moderator.Id,
                                            "گزارش تازه دربارهٔ یک نقد",
                                            $"کاربری گزارشی دربارهٔ یک نقد ثبت کرده است. لطفاً بخش گزارش‌های نقدها را بررسی فرمایید.{Environment.NewLine}" +
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
        /// paginated list of still-open review reports, for reviewers - includes enough of the
        /// reported review's own context (text, rating, book) to review without a follow-up
        /// call, and gracefully covers a report whose review is already gone by the time it's
        /// looked at via ReviewStillExists, rather than erroring or showing blanks. Simpler
        /// than GetOpenPDFPageCommentReportsAsync's own book-title lookup: PDFBookReview has a
        /// direct PDFBookId/PDFBook, no PDFPage indirection, so a single Include chain reaches
        /// the title without a separate batched dictionary lookup.
        /// </summary>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBookReviewReportViewModel[] Items)>> GetOpenPDFBookReviewReportsAsync(PagingParameterModel paging)
        {
            try
            {
                var source = _context.PDFBookReviewReports.AsNoTracking()
                    .Include(r => r.PDFBookReview).ThenInclude(rv => rv.User)
                    .Include(r => r.PDFBookReview).ThenInclude(rv => rv.PDFBook)
                    .Include(r => r.Reporter)
                    .Where(r => r.Status == PDFBookReviewReportStatus.Open)
                    .OrderByDescending(r => r.CreatedAt);

                (PaginationMetadata PagingMeta, PDFBookReviewReport[] Items) paginatedResult =
                    await QueryablePaginator<PDFBookReviewReport>.Paginate(source, paging);

                var items = paginatedResult.Items.Select(r =>
                {
                    var reviewStillExists = r.PDFBookReview != null && r.PDFBookReview.Status == PublishStatus.Published;
                    return new PDFBookReviewReportViewModel()
                    {
                        Id = r.Id,
                        PDFBookReviewId = r.PDFBookReviewId,
                        ReviewStillExists = reviewStillExists,
                        ReviewText = reviewStillExists ? r.PDFBookReview.Text : null,
                        ReviewRating = reviewStillExists ? r.PDFBookReview.Rating : null,
                        ReviewAuthorId = r.PDFBookReview.UserId,
                        ReviewAuthorName = reviewStillExists ? r.PDFBookReview.User.NickName : null,
                        PDFBookId = r.PDFBookReview.PDFBookId,
                        BookTitle = r.PDFBookReview.PDFBook.Title,
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

                return new RServiceResult<(PaginationMetadata, PDFBookReviewReportViewModel[])>((paginatedResult.PagingMeta, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata, PDFBookReviewReportViewModel[])>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// resolve a review report - approving it deletes the reported review (soft-delete,
        /// same as DeletePDFBookReviewAsync) as part of closing the report; rejecting it leaves
        /// the review untouched. Either way the reporter is notified with the outcome and the
        /// reviewer's optional note, and (unlike the comment-report equivalent) the book's own
        /// AverageRating/RatingCount are recomputed if the deleted review had a rating - see
        /// DeletePDFBookReviewAsync's own doc comment for why that recompute has to happen
        /// wherever a rated review can be removed, not just from that one direct-delete path.
        /// Safe to call even if the review has already been deleted by some other means since
        /// the report was filed - approving in that case is a no-op on the review itself, not
        /// an error.
        /// </summary>
        public async Task<RServiceResult<bool>> ResolvePDFBookReviewReportAsync(Guid reviewerId, Guid reportId, bool approved, string response)
        {
            try
            {
                var report = await _context.PDFBookReviewReports.Include(r => r.PDFBookReview).Where(r => r.Id == reportId).SingleOrDefaultAsync();
                if (report == null)
                {
                    return new RServiceResult<bool>(false, $"report {reportId} not found");
                }
                if (report.Status == PDFBookReviewReportStatus.Closed)
                {
                    return new RServiceResult<bool>(false, "این گزارش پیش‌تر بررسی شده است");
                }

                bool actuallyDeleted = false;
                int? bookIdToRecompute = null;
                if (approved && report.PDFBookReview != null && report.PDFBookReview.Status == PublishStatus.Published)
                {
                    report.PDFBookReview.Status = PublishStatus.Deleted;
                    _context.Update(report.PDFBookReview);
                    actuallyDeleted = true;
                    if (report.PDFBookReview.Rating != null)
                    {
                        bookIdToRecompute = report.PDFBookReview.PDFBookId;
                    }
                }

                report.Status = PDFBookReviewReportStatus.Closed;
                report.Approved = approved;
                report.ReviewerId = reviewerId;
                report.ReviewerResponse = response;
                report.ReviewedAt = DateTime.Now;
                _context.Update(report);
                await _context.SaveChangesAsync();

                if (bookIdToRecompute != null)
                {
                    await _RecomputeBookRatingAsync(bookIdToRecompute.Value);
                }

                await _notificationService.PushNotification
                                (
                                    report.ReporterId,
                                    "نتیجهٔ بررسی گزارش شما",
                                    (approved
                                        ? "گزارش شما دربارهٔ یک نقد بررسی و پذیرفته شد؛ نقد حذف شد."
                                        : "گزارش شما دربارهٔ یک نقد بررسی شد؛ نقد حذف نشد.") +
                                    (string.IsNullOrWhiteSpace(response) ? "" : $"{Environment.NewLine}پاسخ بررسی‌کننده: {response}")
                                );

                // the review's own author is told it was removed and why - deliberately at
                // category level, not the reporter's own free-text description or identity;
                // showing the raw complaint or who filed it risks retaliation against the
                // reporter, which is exactly what report anonymity is meant to prevent (same
                // reasoning as ResolvePDFPageCommentReportAsync's own author notification)
                if (actuallyDeleted)
                {
                    await _notificationService.PushNotification
                                    (
                                        report.PDFBookReview.UserId,
                                        "حذف یکی از نقدهای شما",
                                        $"یکی از نقدهای شما به دلیل «{_ReviewReportCategoryLabel(report.Category)}» و پس از بررسی حذف شد." +
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
        /// Persian label for a report category - same fixed set as _CommentReportCategoryLabel,
        /// kept as a separate method (rather than reusing that one) only because the two report
        /// systems are otherwise fully independent - see PDFBookReviewReportEntityShortName's
        /// own doc comment on that split.
        /// </summary>
        private static string _ReviewReportCategoryLabel(string category)
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
