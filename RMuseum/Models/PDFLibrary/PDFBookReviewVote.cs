using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a registered user's like or dislike of someone else's book review - exactly one vote
    /// per (PDFBookReviewId, UserId) pair, enforced by a unique index (see the migration);
    /// switching a vote updates this same row rather than creating a second one. A user can't
    /// vote on their own review - enforced in PDFLibraryService-BookReview.cs, same principle
    /// as not being able to report your own comment.
    /// </summary>
    public class PDFBookReviewVote
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// the review this vote is on
        /// </summary>
        public Guid PDFBookReviewId { get; set; }

        /// <summary>
        /// the review this vote is on
        /// </summary>
        public virtual PDFBookReview PDFBookReview { get; set; }

        /// <summary>
        /// voting user
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// voting user
        /// </summary>
        public virtual RAppUser User { get; set; }

        /// <summary>
        /// true = like, false = dislike - a plain bool, not a nullable tri-state, since there
        /// is no "neutral" vote: removing a vote deletes this row entirely rather than setting
        /// it to some third value.
        /// </summary>
        public bool IsLike { get; set; }

        /// <summary>
        /// when this vote was cast or last changed (like to dislike or vice versa)
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
