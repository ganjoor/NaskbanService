using System;

namespace RMuseum.Models.PDFUserTracking.ViewModels
{
    /// <summary>
    /// PDF Visit View Model
    /// </summary>
    public class PDFVisistViewModel
    {
        /// <summary>
        /// date time
        /// </summary>
        public DateTime DateTime { get; set; }
        /// <summary>
        /// pdf book id
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// page number
        /// </summary>
        public int? PageNumber { get; set; }

        /// <summary>
        /// external image url
        /// </summary>
        public string ExternalImageUrl { get; set; }
    }
}
