using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// one bookmark's state, used both as a pull result item (server -> client) and a push
    /// input item (client -> server). Identity is the natural key (BookId, PageNumber) - no
    /// separate sync id is needed, matching PDFUserBookmark's own uniqueness rule.
    /// </summary>
    public class PDFBookmarkSyncItemViewModel
    {
        /// <summary>
        /// book id
        /// </summary>
        public int BookId { get; set; }

        /// <summary>
        /// 0 means the whole book, matching the existing bookmark API convention
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// note - meaningless when IsDeleted is true
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        /// client-supplied action time - used for last-write-wins conflict resolution on push,
        /// and reported back as-is on pull (see PDFUserBookmark.DateTime)
        /// </summary>
        public DateTime ClientModifiedAt { get; set; }

        /// <summary>
        /// true if this bookmark was removed
        /// </summary>
        public bool IsDeleted { get; set; }
    }

    /// <summary>
    /// one shelf's state, used both as a pull result item and a push input item. Id is the
    /// client-generated shelf id (see PDFShelf.Id).
    /// </summary>
    public class PDFShelfSyncItemViewModel
    {
        /// <summary>
        /// client-generated shelf id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// shelf name - meaningless when IsDeleted is true
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// when the shelf was first created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// client-supplied edit time - used for last-write-wins conflict resolution on push,
        /// and reported back as-is on pull
        /// </summary>
        public DateTime ClientModifiedAt { get; set; }

        /// <summary>
        /// true if this shelf was deleted
        /// </summary>
        public bool IsDeleted { get; set; }
    }

    /// <summary>
    /// one shelf-book membership's state, used both as a pull result item and a push input
    /// item. Identity is the natural key (ShelfId, BookId).
    /// </summary>
    public class PDFShelfBookSyncItemViewModel
    {
        /// <summary>
        /// the shelf - must already exist (or be included earlier in the same push batch, see
        /// PDFUserSyncService.ApplyShelfBookChangesAsync)
        /// </summary>
        public Guid ShelfId { get; set; }

        /// <summary>
        /// the book placed on/removed from the shelf
        /// </summary>
        public int BookId { get; set; }

        /// <summary>
        /// client-supplied action time - used for last-write-wins conflict resolution on push,
        /// and reported back as-is on pull
        /// </summary>
        public DateTime ClientModifiedAt { get; set; }

        /// <summary>
        /// true if the book was removed from this shelf
        /// </summary>
        public bool IsDeleted { get; set; }
    }

    /// <summary>
    /// one study log entry's state, used both as a pull result item and a push input item.
    /// Id is the client-generated entry id (see PDFStudyLogEntry.Id). Entries are append-only
    /// in practice - a push either creates a new entry or (re-push of the same Id, or a
    /// "clear history" action) sets IsDeleted; the fields otherwise never change.
    /// </summary>
    public class PDFStudyLogSyncItemViewModel
    {
        /// <summary>
        /// client-generated entry id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the book this session was reading
        /// </summary>
        public int BookId { get; set; }

        /// <summary>
        /// the page the session left off on - meaningless when IsDeleted is true
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// when the session left off
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// true if this entry was cleared from history
        /// </summary>
        public bool IsDeleted { get; set; }
    }

    /// <summary>
    /// a book's current reading position for the user - derived (not stored/synced directly)
    /// as the latest non-deleted PDFStudyLogEntry per PDFBookId. See PDFUserSyncService.
    /// GetReadingPositionsAsync.
    /// </summary>
    public class PDFReadingPositionViewModel
    {
        /// <summary>
        /// book id
        /// </summary>
        public int BookId { get; set; }

        /// <summary>
        /// last page read
        /// </summary>
        public int LastPageNumber { get; set; }

        /// <summary>
        /// when that page was last read
        /// </summary>
        public DateTime LastReadAt { get; set; }
    }
}
