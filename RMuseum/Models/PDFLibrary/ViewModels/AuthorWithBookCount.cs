namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// a minimal Author projection plus the number of distinct books they're credited on
    /// (optionally filtered to a single role, e.g. "مترجم"/"نویسنده"/"مصحح") - counted at the Book
    /// level (the entity that groups different PDFBook scans/editions of the same work together),
    /// not the PDFBook level, so multiple scans of the same work don't inflate the count.
    /// Deliberately only Id/Name/BookCount - every other Author field (NameInOriginalLanguage,
    /// Bio, ImageId, ExtenalImageUrl, LastModified) is unused by this listing and, in this
    /// dataset, always null anyway - leaving them out of the projection means the underlying
    /// query only ever selects Id and Name from the Authors table.
    /// </summary>
    public class AuthorWithBookCount
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// number of distinct books this author is credited on (in the given role, if a role
        /// filter was applied - across all roles otherwise)
        /// </summary>
        public int BookCount { get; set; }
    }
}
