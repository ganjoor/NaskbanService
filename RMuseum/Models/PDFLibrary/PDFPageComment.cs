using RMuseum.Models.Artifact;
using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a public comment on a PDF page - Phase 1 of the commenting system: plain page-level
    /// comments with threaded replies (a reply is just another PDFPageComment with InReplyToId
    /// set, matching the sibling Ganjoor project's GanjoorComment - no separate reply table).
    /// Phase 2 will add an optional highlighted-region reference: a client-cropped image of the
    /// area plus fractional (0.0-1.0) coordinates relative to PDFPage's own
    /// FullResolutionImageWidth/Height, so the highlight displays correctly regardless of which
    /// device rendered the page or at what zoom level - deliberately left off this entity for
    /// now rather than added as unused nullable columns ahead of need.
    /// </summary>
    public class PDFPageComment
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the page this comment is on
        /// </summary>
        public int PDFPageId { get; set; }

        /// <summary>
        /// the page this comment is on
        /// </summary>
        public virtual PDFPage PDFPage { get; set; }

        /// <summary>
        /// commenting user - always required, unlike GanjoorComment's UserId which is nullable
        /// to accommodate legacy anonymous imported comments this project has none of
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// commenting user
        /// </summary>
        public virtual RAppUser User { get; set; }

        /// <summary>
        /// plain text, not HTML - avoids sanitization/XSS concerns for a first version; unlike
        /// GanjoorComment.HtmlComment, nothing here renders as markup
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// when submitted
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// the comment this one replies to - null for a top-level comment
        /// </summary>
        public Guid? InReplyToId { get; set; }

        /// <summary>
        /// the comment this one replies to - null for a top-level comment
        /// </summary>
        public virtual PDFPageComment InReplyTo { get; set; }

        /// <summary>
        /// published immediately for Phase 1 - no moderation queue yet, but reusing the same
        /// PublishStatus enum already used across this project keeps the door open for one
        /// later without a schema change
        /// </summary>
        public PublishStatus Status { get; set; }
    }
}
