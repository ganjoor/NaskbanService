using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// a pair of PDFBook records the duplicate-detection job believes might be the same book,
    /// waiting for a human operator to verify before an actual merge happens
    /// </summary>
    public class PDFBookDuplicateCandidate
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// first (lower) PDFBook Id of the pair - immutable, set at detection time
        /// </summary>
        public int PDFBookId1 { get; set; }

        /// <summary>
        /// title of PDFBookId1 at detection time (denormalized for the review table)
        /// </summary>
        public string Title1 { get; set; }

        /// <summary>
        /// second (higher) PDFBook Id of the pair - immutable, set at detection time
        /// </summary>
        public int PDFBookId2 { get; set; }

        /// <summary>
        /// title of PDFBookId2 at detection time (denormalized for the review table)
        /// </summary>
        public string Title2 { get; set; }

        /// <summary>
        /// which one of PDFBookId1 / PDFBookId2 should survive the merge (the other one is the duplicate
        /// to be removed and redirected). Defaults to the lowest of the two ids at detection time, but
        /// the human operator reviewing the candidate can change this before confirming.
        /// </summary>
        public int SurvivorPDFBookId { get; set; }

        /// <summary>
        /// confidence score of the detection algorithm (0-100)
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// human readable explanation of why these two were matched (title similarity, same page count, same
        /// checksum, ...) - purely informational, helps the reviewer decide quickly
        /// </summary>
        public string MatchReasons { get; set; }

        /// <summary>
        /// review status
        /// </summary>
        public PDFBookDuplicateCandidateStatus Status { get; set; }

        /// <summary>
        /// detection queue time
        /// </summary>
        public DateTime QueueTime { get; set; }

        /// <summary>
        /// user id of the operator who reviewed this candidate
        /// </summary>
        public Guid? ReviewerId { get; set; }

        /// <summary>
        /// review date/time
        /// </summary>
        public DateTime? ReviewDate { get; set; }

        /// <summary>
        /// optional note left by the reviewer
        /// </summary>
        public string ReviewNote { get; set; }
    }
}
