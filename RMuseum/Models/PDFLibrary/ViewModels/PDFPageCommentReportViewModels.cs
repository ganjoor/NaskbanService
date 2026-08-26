using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// submitting a report against a comment
    /// </summary>
    public class PDFPageCommentReportSubmitViewModel
    {
        /// <summary>
        /// one of a fixed set the client hardcodes: "Spam", "Offensive", "Harassment", "Other"
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// explanatory text provided by the reporter
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// a reviewer's resolution of a report
    /// </summary>
    public class PDFPageCommentReportResolveViewModel
    {
        /// <summary>
        /// true → the report was valid, the reported comment is deleted as part of this call;
        /// false → rejected, the comment is left alone
        /// </summary>
        public bool Approved { get; set; }

        /// <summary>
        /// optional written note on the resolution - see PDFPageCommentReport.ReviewerResponse's
        /// own doc comment on why this is optional, unlike the equivalent field on PDFBookReport
        /// </summary>
        public string Response { get; set; }
    }

    /// <summary>
    /// PDFPageCommentReport, for listing to reviewers - carries enough of the reported
    /// comment's own context (text, book, page) that the review screen doesn't need a
    /// follow-up call just to show what's actually being reported, matching
    /// PDFBookReportViewModel's own PDFBookTitle convention.
    /// </summary>
    public class PDFPageCommentReportViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the reported comment's id
        /// </summary>
        public Guid PDFPageCommentId { get; set; }

        /// <summary>
        /// false if the comment has already been deleted by the time this report is being
        /// looked at (moderated directly, deleted by its own author, or already resolved by
        /// another reviewer who got to it first) - CommentText/CommentAuthorName below are
        /// null in that case, and the review screen should say so rather than show blank text
        /// </summary>
        public bool CommentStillExists { get; set; }

        /// <summary>
        /// the reported comment's own text - null if CommentStillExists is false
        /// </summary>
        public string CommentText { get; set; }

        /// <summary>
        /// the reported comment's author's user id - unlike CommentText/CommentAuthorName,
        /// this is always populated regardless of CommentStillExists: the author's own account
        /// isn't "deleted content" the way the comment's text is, and a moderator reviewing a
        /// report may still want to act on the account (view their other comments, kick them
        /// out) even when this specific comment is already gone
        /// </summary>
        public Guid CommentAuthorId { get; set; }

        /// <summary>
        /// the reported comment's author's display name - null if CommentStillExists is false
        /// </summary>
        public string CommentAuthorName { get; set; }

        /// <summary>
        /// the book the reported comment's page belongs to
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// see PDFBookId
        /// </summary>
        public string BookTitle { get; set; }

        /// <summary>
        /// the page number the reported comment is on
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// reporting user's id
        /// </summary>
        public Guid ReporterId { get; set; }

        /// <summary>
        /// reporting user's display name
        /// </summary>
        public string ReporterName { get; set; }

        /// <summary>
        /// "Spam", "Offensive", "Harassment", or "Other"
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// explanatory text provided by the reporter
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Open or Closed
        /// </summary>
        public PDFPageCommentReportStatus Status { get; set; }

        /// <summary>
        /// null while still Open; true if approved (comment deleted), false if rejected
        /// </summary>
        public bool? Approved { get; set; }

        /// <summary>
        /// when submitted
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// the reviewer's optional written note - null while still Open, or if the reviewer
        /// left it blank
        /// </summary>
        public string ReviewerResponse { get; set; }

        /// <summary>
        /// when resolved - null while still Open
        /// </summary>
        public DateTime? ReviewedAt { get; set; }
    }
}
