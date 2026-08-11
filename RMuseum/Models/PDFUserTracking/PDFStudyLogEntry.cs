using RMuseum.Models.PDFLibrary;
using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFUserTracking
{
    /// <summary>
    /// one entry in a user's study log - "at this moment, the person was on this page of this
    /// book", recorded when a reading session ends (client decides when, not on every page
    /// flip). Server-side counterpart of the Flutter client's local-only StudyLogEntry, kept in
    /// sync with it across the user's devices.
    ///
    /// This is the synced, user-owned replacement for the old PDFVisitRecord-derived
    /// GetUserLastActivityAsync "recently read" feature: PDFVisitRecord is anonymous-friendly,
    /// write-only server telemetry (search terms, every API hit) never meant to be read back as
    /// a device's own source of truth, so it can't represent an editable/deletable/offline
    /// "reading history" the way this table does. A book's current reading position ("resume
    /// where I left off") is deliberately NOT a separate synced table - it's just the latest
    /// (by Timestamp, not IsDeleted) PDFStudyLogEntry per PDFBookId for the user, computed the
    /// same way client-side and server-side.
    /// </summary>
    public class PDFStudyLogEntry
    {
        /// <summary>
        /// Id - client-generated, so a re-push of an already-synced entry (e.g. after a failed
        /// sync retry) is a harmless no-op upsert rather than a duplicate row
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
        /// the book this session was reading
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// the book this session was reading
        /// </summary>
        public PDFBook PDFBook { get; set; }

        /// <summary>
        /// the page the session left off on
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// when the session left off (client-supplied, preserved across syncs - this, not
        /// LastModified, is what "most recent reading position" is computed from)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// server-stamped, updated on every accepted change - the sync cursor field
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// soft-delete/tombstone flag - set when the person clears their study log/history;
        /// entries are otherwise append-only and never edited
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
