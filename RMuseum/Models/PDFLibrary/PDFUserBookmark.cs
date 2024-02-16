using RMuseum.Models.Artifact;
using RMuseum.Models.Bookmark;
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
        /// private
        /// </summary>
        public bool IsPrivate { get; set; }

        /// <summary>
        /// Type
        /// </summary>
        public RBookmarkType RBookmarkType { get; set; }

        /// <summary>
        /// PDF Book Id
        /// </summary>
        public int? PDFBookId { get; set; }

        /// <summary>
        /// PDF Book
        /// </summary>
        public virtual PDFBook PDFBook { get; set; }

        /// <summary>
        /// PDF Page Id
        /// </summary>
        public int? PDFPageId { get; set; }

        /// <summary>
        /// pdf page
        /// </summary>
        public virtual PDFPage PDFPage { get; set; }

        /// <summary>
        /// note
        /// </summary>
        public string HtmlContent { get; set; }

        /// <summary>
        /// DateTime
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Is Updated by User
        /// </summary>
        public bool Modified { get; set; }

        /// <summary>
        /// Last Modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Reference Note Id
        /// </summary>
        public Guid? ReferenceNoteId { get; set; }

        /// <summary>
        /// In Reply to Other Note
        /// </summary>
        public virtual PDFUserBookmark ReferenceNote { get; set; }

        /// <summary>
        /// Status
        /// </summary>
        public PublishStatus Status { get; set; }
    }
}
