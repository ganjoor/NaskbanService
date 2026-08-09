namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// PDFBook duplicate candidate review status
    /// </summary>
    public enum PDFBookDuplicateCandidateStatus
    {
        /// <summary>
        /// detected but not reviewed by a human operator yet
        /// </summary>
        New = 0,

        /// <summary>
        /// reviewed and confirmed as a real duplicate (ready to be merged)
        /// </summary>
        Confirmed = 1,

        /// <summary>
        /// reviewed and rejected (not actually a duplicate)
        /// </summary>
        Rejected = 2,

        /// <summary>
        /// already merged
        /// </summary>
        Merged = 3
    }
}
