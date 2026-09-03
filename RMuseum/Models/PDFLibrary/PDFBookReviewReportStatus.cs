namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// PDFBookReviewReport status
    /// </summary>
    public enum PDFBookReviewReportStatus
    {
        /// <summary>
        /// submitted, not yet reviewed
        /// </summary>
        Open = 0,

        /// <summary>
        /// reviewed and resolved (see PDFBookReviewReport.Approved for the outcome)
        /// </summary>
        Closed = 1,
    }
}
