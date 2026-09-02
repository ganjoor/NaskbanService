using System;

namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// submitting a new review - rejected server-side if the caller already has one for this
    /// book (see SubmitPDFBookReviewAsync's own doc comment) rather than silently overwriting;
    /// editing an existing review is a separate action (PDFBookReviewEditViewModel below).
    /// </summary>
    public class PDFBookReviewSubmitViewModel
    {
        /// <summary>
        /// review text - plain text, not HTML, same as PDFPageComment.Text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 1-5 stars, optional - send null for a text-only review with no rating
        /// </summary>
        public int? Rating { get; set; }
    }

    /// <summary>
    /// editing an existing review - author-only, no moderator override, same reasoning as
    /// PDFPageCommentEditViewModel/EditPDFPageCommentAsync
    /// </summary>
    public class PDFBookReviewEditViewModel
    {
        /// <summary>
        /// the corrected/improved text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 1-5 stars, optional - send null to remove a previously-given rating, or to keep a
        /// review text-only
        /// </summary>
        public int? Rating { get; set; }
    }

    /// <summary>
    /// casting or changing a vote on someone else's review - see CastPDFBookReviewVoteAsync's
    /// own doc comment on why a user can't vote on their own review
    /// </summary>
    public class PDFBookReviewVoteViewModel
    {
        /// <summary>
        /// true = like, false = dislike
        /// </summary>
        public bool IsLike { get; set; }
    }

    /// <summary>
    /// PDFBookReview, for listing - no threading (see PDFBookReview's own doc comment), so
    /// unlike PDFPageCommentViewModel there's no InReplyToId here at all.
    /// </summary>
    public class PDFBookReviewViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the reviewed book's id - not needed where the caller already knows which book (a
        /// single book's own review list), but needed by the site-wide "latest reviews" hub
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// see PDFBookId
        /// </summary>
        public string BookTitle { get; set; }

        /// <summary>
        /// reviewing user's id
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// reviewing user's display name
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// review text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 1-5 stars, or null if this review has no rating
        /// </summary>
        public int? Rating { get; set; }

        /// <summary>
        /// when submitted
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// when the text was last edited by its own author - null if never edited
        /// </summary>
        public DateTime? EditedAt { get; set; }

        /// <summary>
        /// true when the requesting user is this review's own author - lets the client show
        /// edit/delete buttons without a separate ownership check of its own, same convention
        /// as PDFPageCommentViewModel.MyComment
        /// </summary>
        public bool MyReview { get; set; }

        /// <summary>
        /// denormalized like count - see PDFBookReview.LikeCount's own doc comment
        /// </summary>
        public int LikeCount { get; set; }

        /// <summary>
        /// denormalized dislike count - see PDFBookReview.DislikeCount's own doc comment
        /// </summary>
        public int DislikeCount { get; set; }

        /// <summary>
        /// null if the requesting user hasn't voted on this review (or isn't logged in); true
        /// if they liked it, false if they disliked it - lets the client show which vote
        /// button, if either, should appear pressed
        /// </summary>
        public bool? MyVote { get; set; }
    }
}
