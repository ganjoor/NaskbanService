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
        /// client-supplied time of the bookmark action (create/toggle). Doubles as the
        /// last-write-wins conflict field on sync push - compared against the stored value to
        /// decide whether an incoming change is newer than what the server already has.
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// server-stamped at write time - the sync pull cursor field. Deliberately NOT used for
        /// conflict resolution (see DateTime) so that pull-since pagination stays
        /// correct/monotonic regardless of client clock skew.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// soft-delete/tombstone flag. Toggling a bookmark off used to hard-delete this row;
        /// it's now a tombstone instead, so another device syncing later can learn the bookmark
        /// was removed rather than seeing nothing and assuming it was never synced down yet.
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
