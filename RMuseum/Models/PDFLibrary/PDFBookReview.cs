using RMuseum.Models.Artifact;
using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a registered user's review of a book - descriptive text plus an optional 1-5 star
    /// rating. Modeled after PDFPageComment (same Text/Status/CreatedAt/EditedAt shape, same
    /// soft-delete via PublishStatus), but with three deliberate differences: at most one
    /// PUBLISHED review per (PDFBookId, UserId) pair, enforced by a filtered unique index
    /// (Status = Published only) rather than just application-level checking (see the
    /// migration and RMuseumDbContext.OnModelCreating's own comment on why the filter matters
    /// - a plain, unfiltered index would still count a soft-deleted review's row, blocking a
    /// brand-new one for the same book after the old one was deleted); no InReplyToId - reviews are not
    /// threaded, since "replying to a review" is a different feature (that's what a book's own
    /// comments are for, arguably, though this project doesn't have per-book comments either);
    /// and LikeCount/DislikeCount, denormalized the same way PDFBook.AverageRating is (see
    /// that field's own doc comment for why) - sorting reviews by collective score needs a
    /// plain column, not a join-and-aggregate over PDFBookReviewVote on every list request.
    /// </summary>
    public class PDFBookReview
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the reviewed book
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// the reviewed book
        /// </summary>
        public virtual PDFBook PDFBook { get; set; }

        /// <summary>
        /// reviewing user
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// reviewing user
        /// </summary>
        public virtual RAppUser User { get; set; }

        /// <summary>
        /// plain text, not HTML - same reasoning as PDFPageComment.Text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 1-5 stars, optional - a reviewer may rate without writing much, or write a full
        /// review without rating at all. Null means no rating given, not "0 stars" - excluded
        /// from PDFBook.AverageRating/RatingCount either way.
        /// </summary>
        public int? Rating { get; set; }

        /// <summary>
        /// when submitted
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// when last edited by its own author - null if never edited. Same "edited" indicator
        /// reasoning as PDFPageComment.EditedAt, though without threaded replies here the
        /// "a reply no longer makes sense against edited wording" case doesn't apply - shown
        /// purely for the same general transparency (other readers can see the text changed
        /// since first posted).
        /// </summary>
        public DateTime? EditedAt { get; set; }

        /// <summary>
        /// published immediately, no moderation queue - same PublishStatus enum used across
        /// this project for soft-delete, same as PDFPageComment.Status
        /// </summary>
        public PublishStatus Status { get; set; }

        /// <summary>
        /// denormalized count of PDFBookReviewVote rows with IsLike true for this review - see
        /// this class's own doc comment for why this isn't just computed fresh on read
        /// </summary>
        public int LikeCount { get; set; }

        /// <summary>
        /// denormalized count of PDFBookReviewVote rows with IsLike false for this review
        /// </summary>
        public int DislikeCount { get; set; }
    }
}
