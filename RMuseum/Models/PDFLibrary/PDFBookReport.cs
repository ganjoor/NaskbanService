using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a registered user's report against a book (copyright violation, incorrect metadata,
    /// or something else) - modeled after GanjoorCommentAbuseReport from the sibling Ganjoor
    /// project, but richer: that one is simply deleted once handled, with no response back to
    /// the reporter. This one is Closed with a written ReviewerResponse instead, and the
    /// reporter is notified when that happens - a response nobody sees defeats the point of
    /// writing one.
    /// </summary>
    public class PDFBookReport
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the reported book
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// the reported book
        /// </summary>
        public virtual PDFBook PDFBook { get; set; }

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
        /// "Copyright", "IncorrectMetadata", "Other" - a plain string rather than a C# enum,
        /// matching GanjoorCommentAbuseReport.ReasonCode's own convention, so a new category can
        /// be added later without a migration.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// free text explanation provided by the reporter
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Open until a reviewer closes it
        /// </summary>
        public PDFBookReportStatus Status { get; set; }

        /// <summary>
        /// when the report was submitted
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// user who closed/responded to this report - null while still Open
        /// </summary>
        public Guid? ReviewerId { get; set; }

        /// <summary>
        /// user who closed/responded to this report - null while still Open
        /// </summary>
        public virtual RAppUser Reviewer { get; set; }

        /// <summary>
        /// the reviewer's written response to the reporter - null while still Open
        /// </summary>
        public string ReviewerResponse { get; set; }

        /// <summary>
        /// when the report was closed - null while still Open
        /// </summary>
        public DateTime? ReviewedAt { get; set; }
    }
}
