namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// PDFPageCommentReport status
    /// </summary>
    public enum PDFPageCommentReportStatus
    {
        /// <summary>
        /// submitted, not yet reviewed
        /// </summary>
        Open = 0,

        /// <summary>
        /// reviewed and resolved (see PDFPageCommentReport.Approved for the outcome)
        /// </summary>
        Closed = 1,
    }
}
