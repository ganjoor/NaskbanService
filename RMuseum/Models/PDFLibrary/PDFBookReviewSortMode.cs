namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// sort modes for listing book reviews
    /// </summary>
    public enum PDFBookReviewSortMode
    {
        /// <summary>
        /// newest first
        /// </summary>
        Newest = 0,

        /// <summary>
        /// highest Rating first (reviews with no rating sort last); ties broken by newest
        /// </summary>
        HighestRated = 1,

        /// <summary>
        /// highest (LikeCount - DislikeCount) first; ties broken by newest
        /// </summary>
        MostLiked = 2,
    }
}
