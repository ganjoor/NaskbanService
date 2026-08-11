using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RMuseum.Models.PDFLibrary.ViewModels;
using RMuseum.Services;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RMuseum.Controllers
{
    /// <summary>
    /// two-way sync endpoints for the client's offline structures: bookmarks (existing table,
    /// extended with sync fields), shelves/shelf-books, and the study log. Every pull takes a
    /// `since` cursor (pass DateTime.MinValue for a first/full sync) and returns a `serverTime`
    /// to use as the next call's `since`; every push is a plain array of the client's own
    /// locally-changed items, applied with last-write-wins (see each service's doc comments for
    /// the exact per-entity rule). All routes require login - none of this exists for
    /// anonymous/local-only use, matching the client's "sync only kicks in after login" design.
    /// </summary>
    [Produces("application/json")]
    [Route("api/pdf/sync")]
    [Authorize]
    public class PDFSyncController : Controller
    {
        private Guid _loggedOnUserId => new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

        /// <summary>
        /// bookmarks changed since `since` (pass DateTime.MinValue for a full sync)
        /// </summary>
        [HttpGet("bookmarks")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetBookmarkChangesAsync(DateTime since)
        {
            var res = await _bookmarkingService.GetBookmarkSyncChangesAsync(_loggedOnUserId, since);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(new { serverTime = res.Result.ServerTime, items = res.Result.Items });
        }

        /// <summary>
        /// pushes locally-changed bookmarks
        /// </summary>
        [HttpPost("bookmarks")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> PushBookmarkChangesAsync([FromBody] PDFBookmarkSyncItemViewModel[] items)
        {
            var res = await _bookmarkingService.ApplyBookmarkSyncChangesAsync(_loggedOnUserId, items);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// shelves changed since `since` (pass DateTime.MinValue for a full sync)
        /// </summary>
        [HttpGet("shelves")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetShelfChangesAsync(DateTime since)
        {
            var res = await _userSyncService.GetShelfChangesAsync(_loggedOnUserId, since);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(new { serverTime = res.Result.ServerTime, items = res.Result.Items });
        }

        /// <summary>
        /// pushes locally-changed shelves - push this BEFORE shelf-books in the same sync cycle
        /// </summary>
        [HttpPost("shelves")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> PushShelfChangesAsync([FromBody] PDFShelfSyncItemViewModel[] items)
        {
            var res = await _userSyncService.ApplyShelfChangesAsync(_loggedOnUserId, items);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// shelf-book memberships changed since `since` (pass DateTime.MinValue for a full sync)
        /// </summary>
        [HttpGet("shelfbooks")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetShelfBookChangesAsync(DateTime since)
        {
            var res = await _userSyncService.GetShelfBookChangesAsync(_loggedOnUserId, since);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(new { serverTime = res.Result.ServerTime, items = res.Result.Items });
        }

        /// <summary>
        /// pushes locally-changed shelf-book memberships - push shelves first (see PushShelfChangesAsync)
        /// </summary>
        [HttpPost("shelfbooks")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> PushShelfBookChangesAsync([FromBody] PDFShelfBookSyncItemViewModel[] items)
        {
            var res = await _userSyncService.ApplyShelfBookChangesAsync(_loggedOnUserId, items);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// study log entries changed since `since` (pass DateTime.MinValue for a full sync)
        /// </summary>
        [HttpGet("studylog")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetStudyLogChangesAsync(DateTime since)
        {
            var res = await _userSyncService.GetStudyLogChangesAsync(_loggedOnUserId, since);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(new { serverTime = res.Result.ServerTime, items = res.Result.Items });
        }

        /// <summary>
        /// pushes new (or cleared) locally-recorded study log entries
        /// </summary>
        [HttpPost("studylog")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> PushStudyLogChangesAsync([FromBody] PDFStudyLogSyncItemViewModel[] items)
        {
            var res = await _userSyncService.ApplyStudyLogChangesAsync(_loggedOnUserId, items);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// each book's current reading position for the user - derived from the study log, not
        /// synced/stored directly (see PDFStudyLogEntry's doc comment). Replaces the old
        /// PDFVisitRecord-derived GET api/pdf/visits "last activity" list for this purpose.
        /// </summary>
        [HttpGet("readingpositions")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFReadingPositionViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetReadingPositionsAsync()
        {
            var res = await _userSyncService.GetReadingPositionsAsync(_loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// bookmarking service (bookmark sync lives here alongside the rest of that table)
        /// </summary>
        protected readonly IPDFBookmarkService _bookmarkingService;

        /// <summary>
        /// shelves/study-log sync service
        /// </summary>
        protected readonly IPDFUserSyncService _userSyncService;

        /// <summary>
        /// constructor
        /// </summary>
        public PDFSyncController(IPDFBookmarkService bookmarkingService, IPDFUserSyncService userSyncService)
        {
            _bookmarkingService = bookmarkingService;
            _userSyncService = userSyncService;
        }
    }
}
