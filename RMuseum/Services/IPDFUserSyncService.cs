using RMuseum.Models.PDFLibrary.ViewModels;
using RSecurityBackend.Models.Generic;
using System;
using System.Threading.Tasks;

namespace RMuseum.Services
{
    /// <summary>
    /// two-way sync for the structures that have no other server-side owner: shelves,
    /// shelf-book membership, and the study log (which also doubles as the source of each
    /// book's current reading position - see PDFStudyLogEntry's doc comment). Bookmark sync
    /// lives on IPDFBookmarkService instead, next to the rest of that table's logic.
    /// </summary>
    public interface IPDFUserSyncService
    {
        /// <summary>
        /// shelves changed for this user since a given server time - a sync pull
        /// </summary>
        Task<RServiceResult<(DateTime ServerTime, PDFShelfSyncItemViewModel[] Items)>> GetShelfChangesAsync(Guid userId, DateTime since, int take = 500);

        /// <summary>
        /// applies a batch of client-side shelf changes - a sync push. Last-write-wins per item
        /// by ClientModifiedAt, same rule as bookmarks.
        /// </summary>
        Task<RServiceResult<bool>> ApplyShelfChangesAsync(Guid userId, PDFShelfSyncItemViewModel[] items);

        /// <summary>
        /// shelf-book memberships changed for this user since a given server time - a sync pull
        /// </summary>
        Task<RServiceResult<(DateTime ServerTime, PDFShelfBookSyncItemViewModel[] Items)>> GetShelfBookChangesAsync(Guid userId, DateTime since, int take = 500);

        /// <summary>
        /// applies a batch of client-side shelf-book membership changes - a sync push. Callers
        /// MUST push shelf changes (ApplyShelfChangesAsync) before shelf-book changes in the
        /// same sync cycle: an item referencing a shelf id the server doesn't have yet is
        /// skipped rather than applied, on the assumption a newly-created shelf was already
        /// pushed moments earlier and simply hasn't been retried yet if it's still missing.
        /// </summary>
        Task<RServiceResult<bool>> ApplyShelfBookChangesAsync(Guid userId, PDFShelfBookSyncItemViewModel[] items);

        /// <summary>
        /// study log entries changed for this user since a given server time - a sync pull
        /// </summary>
        Task<RServiceResult<(DateTime ServerTime, PDFStudyLogSyncItemViewModel[] Items)>> GetStudyLogChangesAsync(Guid userId, DateTime since, int take = 500);

        /// <summary>
        /// applies a batch of client-side study log entries - a sync push. Entries are
        /// append-only and identified by their own client-generated Id, so this is always an
        /// idempotent upsert, never a last-write-wins comparison against existing content.
        /// </summary>
        Task<RServiceResult<bool>> ApplyStudyLogChangesAsync(Guid userId, PDFStudyLogSyncItemViewModel[] items);

        /// <summary>
        /// each book's current reading position for the user - the latest non-deleted study log
        /// entry per PDFBookId, replacing the old PDFVisitRecord-derived "last activity" list.
        /// </summary>
        Task<RServiceResult<PDFReadingPositionViewModel[]>> GetReadingPositionsAsync(Guid userId);
    }
}
