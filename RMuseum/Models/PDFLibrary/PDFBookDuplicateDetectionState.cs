using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// singleton row tracking the progress of the title-fuzzy-matching pass of duplicate
    /// detection, so an interrupted run can resume instead of rescanning ~30000 books from
    /// scratch. The cheap exact-match passes (MD5 checksum, ISBN) are always re-run in full
    /// on every start, since they are simple grouped queries and finish in well under a second.
    /// </summary>
    public class PDFBookDuplicateDetectionState
    {
        /// <summary>
        /// Id (single row is ever expected to exist)
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// true once a full title-bucket pass has completed without interruption. When true,
        /// the next start begins a fresh full rescan (so newly imported books get covered);
        /// when false, the next start resumes right after LastProcessedTitleBucketKey.
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// title buckets are processed in ascending (ordinal) order of their bucket key; this
        /// is the last one fully finished. Null means either never started, or a completed run
        /// (about to start fresh).
        /// </summary>
        public string LastProcessedTitleBucketKey { get; set; }

        /// <summary>
        /// total number of title buckets computed for the current/last run (informational)
        /// </summary>
        public int TotalTitleBuckets { get; set; }

        /// <summary>
        /// number of title buckets processed so far in the current/last run (informational)
        /// </summary>
        public int ProcessedTitleBuckets { get; set; }

        /// <summary>
        /// when the current/last run started (fresh runs reset this; resumed runs keep it)
        /// </summary>
        public DateTime LastRunStarted { get; set; }

        /// <summary>
        /// last time this row was checkpointed
        /// </summary>
        public DateTime LastRunUpdated { get; set; }
    }
}
