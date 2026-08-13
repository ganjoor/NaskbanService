using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a user-pinned author - lets someone jump straight back to an author's books without
    /// searching for them again every time. Server-side counterpart of the Flutter client's
    /// local-only PinnedAuthor, kept in sync with it. Own Id (rather than a composite key on
    /// (RAppUserId, AuthorId)) matches the existing PDFUserBookmark convention: a natural key
    /// exists and is enforced at the application level in the sync service, but EF still wants
    /// its own single-column primary key.
    /// </summary>
    public class PDFPinnedAuthor
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// owning user
        /// </summary>
        public Guid RAppUserId { get; set; }

        /// <summary>
        /// owning user
        /// </summary>
        public RAppUser RAppUser { get; set; }

        /// <summary>
        /// the pinned author
        /// </summary>
        public int AuthorId { get; set; }

        /// <summary>
        /// the pinned author
        /// </summary>
        public Author Author { get; set; }

        /// <summary>
        /// client-supplied time of the pin action - doubles as the last-write-wins conflict
        /// field on sync push, same role as PDFUserBookmark.DateTime/PDFShelf.ClientModifiedAt
        /// </summary>
        public DateTime PinnedAt { get; set; }

        /// <summary>
        /// server-stamped at write time - the sync pull cursor field, same role as
        /// PDFUserBookmark.LastModified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// soft-delete/tombstone flag - unpinning is never a hard delete immediately, so that a
        /// pull from another device can learn about it (see PDFUserBookmark.IsDeleted)
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
