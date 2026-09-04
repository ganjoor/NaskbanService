namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// sort modes for GetAllPDFBooksAsync - the main book browse/listing endpoint
    /// </summary>
    public enum PDFBookSortMode
    {
        /// <summary>
        /// newest first (the pre-existing, default behavior)
        /// </summary>
        Newest = 0,

        /// <summary>
        /// highest PageFetchCount first - see that field's own doc comment on PDFBook for what
        /// it measures and why
        /// </summary>
        MostPopular = 1,
    }
}
