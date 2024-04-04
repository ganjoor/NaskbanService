namespace RMuseum.Models.PDFLibrary.ViewModels
{
    public class GanjoorPoemMatchViewModel
    {
        /// <summary>
        /// cat id
        /// </summary>
        public int GanjoorCatId { get; set; }

        /// <summary>
        /// start from this poem
        /// </summary>
        public int GanjoorPoemId { get; set; } = 0;

        /// <summary>
        /// book id
        /// </summary>
        public int BookId { get; set; }

        /// <summary>
        /// page number
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// threshold
        /// </summary>
        public int Threshold { get; set; }
    }
}
