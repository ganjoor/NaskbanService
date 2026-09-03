using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a registered user's report against a book review (spam, offensive, harassment, or
    /// something else) - mirrors PDFPageCommentReport exactly (same fields, same reasoning
    /// throughout - see that class's own doc comment). Resolving a report as approved deletes
    /// the reported review as part of closing the report - see
    /// ResolvePDFBookReviewReportAsync's own doc comment.
    /// </summary>
    public class PDFBookReviewReport
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the reported review
        /// </summary>
        public Guid PDFBookReviewId { get; set; }

        /// <summary>
        /// the reported review - callers must be ready for this navigation to point at a
        /// soft-deleted review by the time anyone reads the report back, since resolving a
        /// report as approved deletes the review while the report row itself is kept for
        /// history/audit
        /// </summary>
        public virtual PDFBookReview PDFBookReview { get; set; }

        /// <summary>
        /// reporting user
        /// </summary>
        public Guid ReporterId { get; set; }

        /// <summary>
        /// reporting user
        /// </summary>
        public virtual RAppUser Reporter { get; set; }

        /// <summary>
        /// one of a fixed set of category strings the client hardcodes and sends verbatim:
        /// "Spam", "Offensive", "Harassment", "Other" - same convention as
        /// PDFPageCommentReport.Category, a plain string rather than a C# enum so a new
        /// category can be added later without a migration.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// free text explanation provided by the reporter
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Open until a reviewer resolves it
        /// </summary>
        public PDFBookReviewReportStatus Status { get; set; }

        /// <summary>
        /// null while still Open; true if the reviewer approved the report (review deleted),
        /// false if rejected (review left alone)
        /// </summary>
        public bool? Approved { get; set; }

        /// <summary>
        /// when the report was submitted
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// user who resolved this report - null while still Open
        /// </summary>
        public Guid? ReviewerId { get; set; }

        /// <summary>
        /// user who resolved this report - null while still Open
        /// </summary>
        public virtual RAppUser Reviewer { get; set; }

        /// <summary>
        /// the reviewer's optional written note on the resolution - same reasoning as
        /// PDFPageCommentReport.ReviewerResponse: genuinely optional, since the outcome is
        /// usually self-explanatory and requiring a written justification for every obvious
        /// removal would just add friction
        /// </summary>
        public string ReviewerResponse { get; set; }

        /// <summary>
        /// when the report was resolved - null while still Open
        /// </summary>
        public DateTime? ReviewedAt { get; set; }
    }
}
