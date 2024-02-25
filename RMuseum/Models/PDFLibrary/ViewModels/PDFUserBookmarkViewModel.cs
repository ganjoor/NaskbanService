using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    public class PDFUserBookmarkViewModel
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// book title
        /// </summary>
        public  string BookTitle { get; set; }

        /// <summary>
        /// book id
        /// </summary>
        public int BookId { get; set; }

        /// <summary>
        /// PDF Page Id
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// note
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        /// external image url
        /// </summary>
        public string ExtenalImageUrl { get; set; }

        /// <summary>
        /// DateTime
        /// </summary>
        public DateTime DateTime { get; set; }
    }
}
