using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// an Author plus the number of distinct books they're credited on (optionally filtered to a
    /// single role, e.g. "مترجم"/"نویسنده"/"مصحح") - counted at the Book level (the entity that
    /// groups different PDFBook scans/editions of the same work together), not the PDFBook level,
    /// so multiple scans of the same work don't inflate the count
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
        /// Name in original language
        /// </summary>
        public string NameInOriginalLanguage { get; set; }

        /// <summary>
        /// bio
        /// </summary>
        public string Bio { get; set; }

        /// <summary>
        /// Cover Image Id
        /// </summary>
        public Guid? ImageId { get; set; }

        /// <summary>
        /// external image url
        /// </summary>
        public string ExtenalImageUrl { get; set; }

        /// <summary>
        /// Last Modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// number of distinct books this author is credited on (in the given role, if a role
        /// filter was applied - across all roles otherwise)
        /// </summary>
        public int BookCount { get; set; }
    }
}
