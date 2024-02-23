using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// PDF User Bookmark
    /// </summary>
    public class PDFUserBookmark
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// User Id
        /// </summary>
        public Guid RAppUserId { get; set; }

        /// <summary>
        /// User
        /// </summary>
        public RAppUser RAppUser { get; set; }

        /// <summary>
        /// PDF Book Id
        /// </summary>
        public int? PDFBookId { get; set; }

        /// <summary>
        /// PDF Book
        /// </summary>
        public virtual PDFBook PDFBook { get; set; }

        /// <summary>
        /// page id
        /// </summary>
        public int? PageId { get; set; }

        /// <summary>
        /// page
        /// </summary>
        public virtual PDFPage  Page { get; set; }

        /// <summary>
        /// note
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        /// DateTime
        /// </summary>
        public DateTime DateTime { get; set; }
    }
}
