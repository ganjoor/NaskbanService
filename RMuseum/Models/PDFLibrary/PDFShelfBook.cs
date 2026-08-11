using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// one book's membership in one PDFShelf - a book can be on any number of shelves. Natural
    /// key for sync purposes is (PDFShelfId, PDFBookId); Id exists only because EF Core wants a
    /// primary key and the client's local ShelfBook has no id of its own to reuse.
    /// </summary>
    public class PDFShelfBook
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// owning shelf
        /// </summary>
        public Guid PDFShelfId { get; set; }

        /// <summary>
        /// owning shelf
        /// </summary>
        public PDFShelf PDFShelf { get; set; }

        /// <summary>
        /// the book placed on the shelf
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// the book placed on the shelf
        /// </summary>
        public PDFBook PDFBook { get; set; }

        /// <summary>
        /// client-supplied time this book was added to this shelf. Doubles as the
        /// last-write-wins conflict field on sync push, same role as PDFUserBookmark.DateTime.
        /// </summary>
        public DateTime AddedAt { get; set; }

        /// <summary>
        /// server-stamped at write time - the sync pull cursor field, same role as
        /// PDFShelf.LastModified.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// soft-delete/tombstone flag - see PDFShelf.IsDeleted
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
