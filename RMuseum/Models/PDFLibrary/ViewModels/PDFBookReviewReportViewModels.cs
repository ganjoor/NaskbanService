using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// submitting a report against a review - mirrors PDFPageCommentReportSubmitViewModel
    /// </summary>
    public class PDFBookReviewReportSubmitViewModel
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
    /// a reviewer's resolution of a report - mirrors PDFPageCommentReportResolveViewModel
    /// </summary>
    public class PDFBookReviewReportResolveViewModel
    {
        /// <summary>
        /// true → the report was valid, the reported review is deleted as part of this call;
        /// false → rejected, the review is left alone
        /// </summary>
        public bool Approved { get; set; }

        /// <summary>
        /// optional written note on the resolution - see PDFBookReviewReport.ReviewerResponse's
        /// own doc comment on why this is optional
        /// </summary>
        public string Response { get; set; }
    }

    /// <summary>
    /// PDFBookReviewReport, for listing to reviewers - carries enough of the reported review's
    /// own context (text, rating, book) that the review screen doesn't need a follow-up call
    /// just to show what's actually being reported, matching PDFPageCommentReportViewModel's
    /// own convention.
    /// </summary>
    public class PDFBookReviewReportViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the reported review's id
        /// </summary>
        public Guid PDFBookReviewId { get; set; }

        /// <summary>
        /// false if the review has already been deleted by the time this report is being
        /// looked at (moderated directly, deleted by its own author, or already resolved by
        /// another reviewer who got to it first) - ReviewText/ReviewRating/ReviewAuthorName
        /// below are null in that case, and the review screen should say so rather than show
        /// blank text
        /// </summary>
        public bool ReviewStillExists { get; set; }

        /// <summary>
        /// the reported review's own text - null if ReviewStillExists is false
        /// </summary>
        public string ReviewText { get; set; }

        /// <summary>
        /// the reported review's own rating (1-5), if it had one - null if ReviewStillExists is
        /// false, or if the review genuinely had no rating (text-only)
        /// </summary>
        public int? ReviewRating { get; set; }

        /// <summary>
        /// the reported review's author's user id - unlike ReviewText/ReviewAuthorName, this is
        /// always populated regardless of ReviewStillExists: the author's own account isn't
        /// "deleted content" the way the review's text is, and a moderator reviewing a report
        /// may still want to act on the account even when this specific review is already gone
        /// </summary>
        public Guid ReviewAuthorId { get; set; }

        /// <summary>
        /// the reported review's author's display name - null if ReviewStillExists is false
        /// </summary>
        public string ReviewAuthorName { get; set; }

        /// <summary>
        /// the reviewed book's id
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// see PDFBookId
        /// </summary>
        public string BookTitle { get; set; }

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
        public PDFBookReviewReportStatus Status { get; set; }

        /// <summary>
        /// null while still Open; true if approved (review deleted), false if rejected
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
