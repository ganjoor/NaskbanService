using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// submitting a report against a book
    /// </summary>
    public class PDFBookReportSubmitViewModel
    {
        /// <summary>
        /// one of a fixed set the client hardcodes: "Copyright", "IncorrectMetadata", "Other"
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// explanatory text provided by the reporter
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// a reviewer's response, closing a report
    /// </summary>
    public class PDFBookReportCloseViewModel
    {
        /// <summary>
        /// the reviewer's written response to the reporter
        /// </summary>
        public string Response { get; set; }
    }

    /// <summary>
    /// PDFBookReport, for listing to reviewers
    /// </summary>
    public class PDFBookReportViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// reported book
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// reported book's title, so the list doesn't need a follow-up call just to show it
        /// </summary>
        public string PDFBookTitle { get; set; }

        /// <summary>
        /// reporting user's id
        /// </summary>
        public Guid ReporterId { get; set; }

        /// <summary>
        /// reporting user's display name
        /// </summary>
        public string ReporterName { get; set; }

        /// <summary>
        /// "Copyright", "IncorrectMetadata", or "Other"
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// explanatory text provided by the reporter
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Open or Closed
        /// </summary>
        public PDFBookReportStatus Status { get; set; }

        /// <summary>
        /// when submitted
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// the reviewer's written response - null while still Open
        /// </summary>
        public string ReviewerResponse { get; set; }

        /// <summary>
        /// when closed - null while still Open
        /// </summary>
        public DateTime? ReviewedAt { get; set; }
    }
}
