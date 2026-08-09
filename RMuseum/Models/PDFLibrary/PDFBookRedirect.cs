using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a redirect from a PDFBook id that no longer exists (merged away as a duplicate) to the
    /// survivor id that now holds its data - API calls for the old id transparently serve the
    /// survivor's data instead of a 404. Flattened, not chained: if the survivor itself later gets
    /// merged into a third book, every existing redirect pointing at it is updated to point
    /// straight at the new final survivor, so a lookup is always a single row read.
    /// </summary>
    public class PDFBookRedirect
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the merged-away PDFBook id that no longer has its own row
        /// </summary>
        public int OldPDFBookId { get; set; }

        /// <summary>
        /// the current living PDFBook id that now holds the data
        /// </summary>
        public int SurvivorPDFBookId { get; set; }

        /// <summary>
        /// when the merge happened
        /// </summary>
        public DateTime MergeDate { get; set; }

        /// <summary>
        /// user id of the operator who confirmed the merge
        /// </summary>
        public Guid MergedByUserId { get; set; }
    }
}
