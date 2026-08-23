using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// submitting a page comment - Phase 1 fields (Text/InReplyToId) plus Phase 2's optional
    /// highlighted region. Bound from multipart form fields, not JSON, since Phase 2 also
    /// needs to accept an image file alongside these - see
    /// PDFLibraryController.SubmitPDFPageCommentAsync for how each field is read from
    /// Request.Form (matching the sibling Ganjoor project's own mixed file+field upload
    /// convention, e.g. GanjoorController's Request.Form["bookName"] pattern).
    /// </summary>
    public class PDFPageCommentPostViewModel
    {
        /// <summary>
        /// comment text - plain text, not HTML
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// send null for a top-level comment, or another comment's id to reply to it
        /// </summary>
        public Guid? InReplyToId { get; set; }

        /// <summary>
        /// Phase 2: fractional (0.0-1.0) coordinates of the highlighted rectangle, relative to
        /// PDFPage.FullResolutionImageWidth/Height. All four must be present together, or all
        /// four absent - send none of them for a plain page-level comment.
        /// </summary>
        public double? HighlightX { get; set; }

        /// <summary>
        /// see HighlightX
        /// </summary>
        public double? HighlightY { get; set; }

        /// <summary>
        /// see HighlightX
        /// </summary>
        public double? HighlightWidth { get; set; }

        /// <summary>
        /// see HighlightX
        /// </summary>
        public double? HighlightHeight { get; set; }
    }

    /// <summary>
    /// editing an existing comment's text - see PDFLibraryService-Comment.cs's
    /// EditPDFPageCommentAsync for the full reasoning (author-only, no moderator override,
    /// unlike delete)
    /// </summary>
    public class PDFPageCommentEditViewModel
    {
        /// <summary>
        /// the corrected/improved text
        /// </summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// PDFPageComment, for listing - a flat list, not a nested tree (matches
    /// GanjoorCommentFullViewModel's own convention): each reply carries its parent's
    /// InReplyToId, and the client builds whatever threaded/indented display it wants from
    /// that rather than the server pre-shaping a tree.
    /// </summary>
    public class PDFPageCommentViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the page this comment is on
        /// </summary>
        public int PDFPageId { get; set; }

        /// <summary>
        /// the page's own page number within its book - not needed by the page-scoped
        /// GetPDFPageCommentsAsync (the caller already knows which page they asked for), but
        /// needed by the per-book and site-wide hubs (GetRecentPDFPageCommentsAsync), so they
        /// can link each comment back to the page it's on
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// the book this comment's page belongs to - same reasoning as PageNumber: not needed
        /// where the caller already knows which book (a single page's own comments, or a
        /// specific book's hub), but needed for the site-wide hub, where a comment could
        /// belong to any book
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// see PDFBookId
        /// </summary>
        public string BookTitle { get; set; }

        /// <summary>
        /// commenting user's id
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// commenting user's display name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// comment text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// when submitted
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// when the text was last edited by its own author - null if never edited
        /// </summary>
        public DateTime? EditedAt { get; set; }

        /// <summary>
        /// null for a top-level comment, otherwise the id of the comment this one replies to
        /// </summary>
        public Guid? InReplyToId { get; set; }

        /// <summary>
        /// true when the requesting user is this comment's own author - lets the client show
        /// a delete button without a separate ownership check of its own
        /// </summary>
        public bool MyComment { get; set; }

        /// <summary>
        /// Phase 2: URL of the highlighted region's cropped image, if this comment has one -
        /// null for a plain page-level comment. Built from RImage's own Id/ContentType via the
        /// generic api/rimages/{id}.{ext} route (RImageControllerBase.GetImageWithCustomExtension)
        /// - see GetPDFPageCommentsAsync's own doc comment on this: that exact route wasn't
        /// something already used elsewhere in this project to copy from, so the URL shape here
        /// is inferred from the route's confirmed parameter shape rather than an existing
        /// precedent - worth a quick check against this server's own Swagger UI.
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Phase 2: see PDFPageCommentPostViewModel.HighlightX - null unless ImageUrl is set
        /// </summary>
        public double? HighlightX { get; set; }

        /// <summary>
        /// see HighlightX
        /// </summary>
        public double? HighlightY { get; set; }

        /// <summary>
        /// see HighlightX
        /// </summary>
        public double? HighlightWidth { get; set; }

        /// <summary>
        /// see HighlightX
        /// </summary>
        public double? HighlightHeight { get; set; }
    }
}
