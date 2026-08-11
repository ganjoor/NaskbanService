using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a user-created, named collection of whole books ("قفسه") - server-side counterpart of
    /// the Flutter client's local-only BookShelf, kept in sync with it. Id is client-generated
    /// (not server ValueGeneratedOnAdd) so a newly created shelf can be referenced by
    /// PDFShelfBook rows pushed in the same sync batch, before any round trip confirms a
    /// server-assigned id.
    /// </summary>
    public class PDFShelf
    {
        /// <summary>
        /// Id - client-generated
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
        /// shelf name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// when this shelf was first created (client-supplied, preserved across syncs)
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// client-supplied time of the most recent edit (rename/delete) - compared against the
        /// stored value on push to resolve last-write-wins conflicts between devices. Distinct
        /// from LastModified: this one carries the client's own clock/intent, LastModified
        /// carries the server's, so a push can tell "is this change newer than what I have"
        /// independent of pull-cursor bookkeeping.
        /// </summary>
        public DateTime ClientModifiedAt { get; set; }

        /// <summary>
        /// server-stamped at write time - the sync pull cursor field. Deliberately NOT used for
        /// conflict resolution (see ClientModifiedAt) so that pull-since pagination stays
        /// correct/monotonic regardless of client clock skew.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// soft-delete/tombstone flag - deletions are never hard-deleted immediately so that a
        /// pull from another device can learn about them; see PDFStorageCleanup-style periodic
        /// purge for old tombstones
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
