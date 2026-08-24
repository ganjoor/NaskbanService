using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a registered user's report against a page comment (spam, offensive, harassment, or
    /// something else) - modeled after PDFBookReport, which is itself modeled after the
    /// sibling Ganjoor project's GanjoorCommentAbuseReport but richer (a written resolution
    /// the reporter is notified of, rather than the report just silently disappearing).
    /// Resolving a report as approved deletes the reported comment as part of closing the
    /// report - see ResolvePDFPageCommentReportAsync's own doc comment.
    /// </summary>
    public class PDFPageCommentReport
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the reported comment
        /// </summary>
        public Guid PDFPageCommentId { get; set; }

        /// <summary>
        /// the reported comment - callers must be ready for this navigation to point at a
        /// soft-deleted comment by the time anyone reads the report back, since resolving a
        /// report as approved deletes the comment while the report row itself is kept for
        /// history/audit
        /// </summary>
        public virtual PDFPageComment PDFPageComment { get; set; }

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
        /// "Spam", "Offensive", "Harassment", "Other" - a plain string rather than a C# enum,
        /// matching PDFBookReport.Category's own convention, so a new category can be added
        /// later without a migration.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// free text explanation provided by the reporter
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Open until a reviewer resolves it
        /// </summary>
        public PDFPageCommentReportStatus Status { get; set; }

        /// <summary>
        /// null while still Open; true if the reviewer approved the report (comment deleted),
        /// false if rejected (comment left alone)
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
        /// the reviewer's optional written note on the resolution - unlike
        /// PDFBookReport.ReviewerResponse, this is genuinely optional: the outcome (deleted or
        /// not) is usually self-explanatory for a comment report, and requiring a written
        /// justification for every obvious spam removal would just add friction
        /// </summary>
        public string ReviewerResponse { get; set; }

        /// <summary>
        /// when the report was resolved - null while still Open
        /// </summary>
        public DateTime? ReviewedAt { get; set; }
    }
}
