namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// PDFBookReport status
    /// </summary>
    public enum PDFBookReportStatus
    {
        /// <summary>
        /// submitted, not yet reviewed
        /// </summary>
        Open = 0,

        /// <summary>
        /// reviewed and responded to
        /// </summary>
        Closed = 1,
    }
}
