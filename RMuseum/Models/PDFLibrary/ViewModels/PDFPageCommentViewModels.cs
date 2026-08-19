using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// submitting a page comment (Phase 1 - no highlight/image yet)
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
        /// null for a top-level comment, otherwise the id of the comment this one replies to
        /// </summary>
        public Guid? InReplyToId { get; set; }

        /// <summary>
        /// true when the requesting user is this comment's own author - lets the client show
        /// a delete button without a separate ownership check of its own
        /// </summary>
        public bool MyComment { get; set; }
    }
}
