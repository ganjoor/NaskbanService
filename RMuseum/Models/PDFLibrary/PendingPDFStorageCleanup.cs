using System;

namespace RMuseum.Models.PDFLibrary
{
    /// <summary>
    /// tracks a storage folder (FTP + local disk) that still needs to be physically deleted after
    /// its owning PDFBook's database row is already gone. Deleting the DB rows is fast and
    /// transactional; deleting from FTP can time out or fail transiently, so it's handled by a
    /// separate resumable background job instead of blocking the delete/merge request itself -
    /// this row is how that job knows what's still left to clean up, and survives a crash mid-run
    /// since it isn't removed until the physical cleanup actually succeeds.
    /// </summary>
    public class PendingPDFStorageCleanup
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// storage folder name (matches PDFBook.StorageFolderName at the time it was queued)
        /// </summary>
        public string StorageFolderName { get; set; }

        /// <summary>
        /// whether an FTP delete is needed (only PDFBooks that were Published get uploaded to the
        /// external FTP server in the first place, mirroring the original check in RemovePDFBookAsync)
        /// </summary>
        public bool NeedsFtpDelete { get; set; }

        /// <summary>
        /// when this cleanup was queued
        /// </summary>
        public DateTime QueueTime { get; set; }

        /// <summary>
        /// how many cleanup attempts have been made so far
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// last attempt time, if any
        /// </summary>
        public DateTime? LastAttempt { get; set; }

        /// <summary>
        /// last error, if the most recent attempt failed
        /// </summary>
        public string LastError { get; set; }
    }
}
