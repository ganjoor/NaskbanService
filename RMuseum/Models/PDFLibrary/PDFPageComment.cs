using RMuseum.Models.Artifact;
using RSecurityBackend.Models.Auth.Db;
using RSecurityBackend.Models.Image;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a public comment on a PDF page - Phase 1: plain page-level comments with threaded
    /// replies (a reply is just another PDFPageComment with InReplyToId set, matching the
    /// sibling Ganjoor project's GanjoorComment - no separate reply table). Phase 2 (below):
    /// an optional highlighted-region reference - a client-cropped image of the area (pages
    /// are rendered entirely client-side, via pdfx/pdf.js, so there is no server-side page
    /// render to crop from) plus fractional (0.0-1.0) coordinates relative to PDFPage's own
    /// FullResolutionImageWidth/Height, so the highlight displays correctly regardless of
    /// which device rendered the page or at what zoom level.
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
        /// when the text was last edited by its own author - null if never edited. Shown to
        /// other readers as a plain "edited" indicator (not a full revision history) so a
        /// thread doesn't go silently confusing when someone replied to wording that's since
        /// changed.
        /// </summary>
        public DateTime? EditedAt { get; set; }

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

        /// <summary>
        /// Phase 2: the client-cropped snippet of the highlighted region, if this comment
        /// references one - null for a plain page-level comment
        /// </summary>
        public Guid? ImageId { get; set; }

        /// <summary>
        /// Phase 2: the client-cropped snippet of the highlighted region, if this comment
        /// references one - null for a plain page-level comment
        /// </summary>
        public virtual RImage Image { get; set; }

        /// <summary>
        /// Phase 2: fractional (0.0-1.0) X of the highlighted rectangle's top-left corner,
        /// relative to PDFPage.FullResolutionImageWidth - present only alongside the other
        /// three Highlight* fields and ImageId, all four or none
        /// </summary>
        public double? HighlightX { get; set; }

        /// <summary>
        /// Phase 2: fractional (0.0-1.0) Y of the highlighted rectangle's top-left corner,
        /// relative to PDFPage.FullResolutionImageHeight
        /// </summary>
        public double? HighlightY { get; set; }

        /// <summary>
        /// Phase 2: fractional (0.0-1.0) width of the highlighted rectangle, relative to
        /// PDFPage.FullResolutionImageWidth
        /// </summary>
        public double? HighlightWidth { get; set; }

        /// <summary>
        /// Phase 2: fractional (0.0-1.0) height of the highlighted rectangle, relative to
        /// PDFPage.FullResolutionImageHeight
        /// </summary>
        public double? HighlightHeight { get; set; }
    }
}
