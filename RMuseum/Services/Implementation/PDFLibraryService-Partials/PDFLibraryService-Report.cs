using Microsoft.EntityFrameworkCore;
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
        /// submit a report against a book (copyright violation, incorrect metadata, or
        /// something else) - any authenticated user. Modeled after the sibling Ganjoor
        /// project's GanjoorService.ReportComment: save first, then look up every user holding
        /// the PDFBookReportEntityShortName:ModerateOperationShortName permission and notify
        /// each of them. A failure in that lookup/notification step is swallowed rather than
        /// failing the whole submission - the report itself is already safely saved by then,
        /// and a missed notification isn't worth losing a user's report over (same reasoning
        /// the Ganjoor precedent uses: "if not, do nothing").
        /// </summary>
        public async Task<RServiceResult<Guid>> SubmitPDFBookReportAsync(Guid reporterId, int pdfBookId, PDFBookReportSubmitViewModel model)
        {
            try
            {
                var book = await _context.PDFBooks.AsNoTracking().Where(b => b.Id == pdfBookId).SingleOrDefaultAsync();
                if (book == null)
                {
                    return new RServiceResult<Guid>(Guid.Empty, $"PDFBook {pdfBookId} not found");
                }

                var report = new PDFBookReport()
                {
                    Id = Guid.NewGuid(),
                    PDFBookId = pdfBookId,
                    ReporterId = reporterId,
                    Category = model.Category,
                    Description = model.Description,
                    Status = PDFBookReportStatus.Open,
                    CreatedAt = DateTime.Now,
                };
                _context.PDFBookReports.Add(report);
                await _context.SaveChangesAsync();

                var moderators = await _appUserService.GetUsersHavingPermission(RMuseumSecurableItem.PDFBookReportEntityShortName, RMuseumSecurableItem.ModerateOperationShortName);
                if (string.IsNullOrEmpty(moderators.ExceptionString)) // if not, do nothing - see doc comment above
                {
                    foreach (var moderator in moderators.Result)
                    {
                        await _notificationService.PushNotification
                                        (
                                            (Guid)moderator.Id,
                                            "گزارش تازه دربارهٔ یک کتاب",
                                            $"کاربری گزارشی دربارهٔ کتاب «{book.Title}» ثبت کرده است. لطفاً بخش گزارش‌های کاربران را بررسی فرمایید.{Environment.NewLine}" +
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
        /// paginated list of still-open reports, for reviewers
        /// </summary>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBookReportViewModel[] Items)>> GetOpenPDFBookReportsAsync(PagingParameterModel paging)
        {
            try
            {
                var source =
                    _context.PDFBookReports
                    .Include(r => r.PDFBook)
                    .Include(r => r.Reporter)
                    .Where(r => r.Status == PDFBookReportStatus.Open)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new PDFBookReportViewModel()
                    {
                        Id = r.Id,
                        PDFBookId = r.PDFBookId,
                        PDFBookTitle = r.PDFBook.Title,
                        ReporterId = r.ReporterId,
                        ReporterName = r.Reporter.NickName,
                        Category = r.Category,
                        Description = r.Description,
                        Status = r.Status,
                        CreatedAt = r.CreatedAt,
                        ReviewerResponse = r.ReviewerResponse,
                        ReviewedAt = r.ReviewedAt,
                    });

                (PaginationMetadata PagingMeta, PDFBookReportViewModel[] Items) paginatedResult =
                    await QueryablePaginator<PDFBookReportViewModel>.Paginate(source, paging);

                return new RServiceResult<(PaginationMetadata, PDFBookReportViewModel[])>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata, PDFBookReportViewModel[])>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// respond to and close a report - notifies the original reporter with the response.
        /// A response nobody sees defeats the point of writing one, so unlike the sibling
        /// Ganjoor project's comment-report handling (which just deletes the report with no
        /// feedback to the reporter), this one always notifies.
        /// </summary>
        public async Task<RServiceResult<bool>> ClosePDFBookReportAsync(Guid reviewerId, Guid reportId, string response)
        {
            try
            {
                var report = await _context.PDFBookReports.Include(r => r.PDFBook).Where(r => r.Id == reportId).SingleOrDefaultAsync();
                if (report == null)
                {
                    return new RServiceResult<bool>(false, $"report {reportId} not found");
                }
                if (report.Status == PDFBookReportStatus.Closed)
                {
                    return new RServiceResult<bool>(false, "report is already closed");
                }

                report.Status = PDFBookReportStatus.Closed;
                report.ReviewerId = reviewerId;
                report.ReviewerResponse = response;
                report.ReviewedAt = DateTime.Now;
                _context.Update(report);
                await _context.SaveChangesAsync();

                await _notificationService.PushNotification
                                (
                                    report.ReporterId,
                                    "پاسخ به گزارش شما",
                                    $"گزارش شما دربارهٔ کتاب «{report.PDFBook?.Title}» بررسی و بسته شد.{Environment.NewLine}" +
                                    $"پاسخ: {response}"
                                );

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }
    }
}
