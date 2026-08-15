using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RMuseum.Services;
using RSecurityBackend.Models.Auth.Memory;
using RSecurityBackend.Models.Generic;
using System.Linq;
using System;
using System.Net;
using System.Threading.Tasks;
using RSecurityBackend.Services;
using System.Collections.Generic;
using Newtonsoft.Json;
using RMuseum.Models.Artifact;
using RMuseum.Models.GanjoorIntegration.ViewModels;
using RMuseum.Models.GanjoorIntegration;
using RMuseum.Models.ImportJob;
using RMuseum.Models.PDFUserTracking;
using RMuseum.Models.PDFUserTracking.ViewModels;
using RMuseum.Models.Artifact.ViewModels;

namespace RMuseum.Controllers
{
    [Produces("application/json")]
    [Route("api/pdf")]
    public class PDFLibraryController : Controller
    {
        /// <summary>
        ///get all published pdfbooks (including CoverImage info but not pages or tagibutes info) - check paging-headers for paging info
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>

        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFBook>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetAllPDFBooksAsync([FromQuery] PagingParameterModel paging)
        {
            var pdfBooksInfo = await _pdfService.GetAllPDFBooksAsync(paging, [PublishStatus.Published]);
            if (!string.IsNullOrEmpty(pdfBooksInfo.ExceptionString))
            {
                return BadRequest(pdfBooksInfo.ExceptionString);
            }

            if (pdfBooksInfo.Result.Books.Count() > 0)
            {
                DateTime lastModification = pdfBooksInfo.Result.Books.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pdfBooksInfo.Result.PagingMeta));

            return Ok(pdfBooksInfo.Result.Books);
        }

        /// <summary>
        /// get all pdf books visible by user (including CoverImage info but not items info)
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("secure")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFBook>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUserVisiblePDFBooksAsync([FromQuery] PagingParameterModel paging)
        {
            RServiceResult<PublishStatus[]> v = await _GetUserVisiblePDFBooksStatusSetAsync
                (
                new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value),
                new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value)
                );
            if (!string.IsNullOrEmpty(v.ExceptionString))
                return BadRequest(v.ExceptionString);
            PublishStatus[] visibleItems = v.Result;

            if (visibleItems.Length == 1 && visibleItems[0] == PublishStatus.Published) //Caching
            {
                return await GetAllPDFBooksAsync(paging);
            }

            RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)> itemsInfo = await _pdfService.GetAllPDFBooksAsync(paging, visibleItems);
            if (!string.IsNullOrEmpty(itemsInfo.ExceptionString))
            {
                return BadRequest(itemsInfo.ExceptionString);
            }

            if (itemsInfo.Result.Books.Count() > 0)
            {
                DateTime lastModification = itemsInfo.Result.Books.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(itemsInfo.Result.PagingMeta));

            return Ok(itemsInfo.Result.Books);
        }

        /// <summary>
        /// secure get a pdf book
        /// </summary>
        /// <param name="id"></param>
        /// <param name="includePages"></param>
        /// <param name="includeBookText"></param>
        /// <param name="includePageText"></param>
        /// <returns></returns>
        [HttpGet("secure/{id}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFBook))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetUserVisiblePDFBookAsync(int id, bool includePages = false, bool includeBookText = false, bool includePageText = false)
        {
            RServiceResult<PublishStatus[]> v = await _GetUserVisiblePDFBooksStatusSetAsync
               (
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value),
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value)
               );
            if (!string.IsNullOrEmpty(v.ExceptionString))
                return BadRequest(v.ExceptionString);
            PublishStatus[] visibleItems = v.Result;
            RServiceResult<PDFBook> bookRes = null;
            if (visibleItems.Length == 1 && visibleItems[0] == PublishStatus.Published)
            {
                bookRes = await _pdfService.GetPDFBookByIdAsync(id, new PublishStatus[] { PublishStatus.Published }, includePages, includeBookText, includePageText);
                if (!string.IsNullOrEmpty(bookRes.ExceptionString))
                {
                    return BadRequest(bookRes.ExceptionString);
                }
                if (bookRes.Result == null)
                    return NotFound();
            }
            if (bookRes == null)
            {
                bookRes = await _pdfService.GetPDFBookByIdAsync(id, visibleItems, includePages, includeBookText, includePageText);
            }

            if (!string.IsNullOrEmpty(bookRes.ExceptionString))
            {
                return BadRequest(bookRes.ExceptionString);
            }
            if (bookRes.Result == null)
                return NotFound();

            Response.GetTypedHeaders().LastModified = bookRes.Result.LastModified;

            var requestHeaders = Request.GetTypedHeaders();
            if (requestHeaders.IfModifiedSince.HasValue &&
                requestHeaders.IfModifiedSince.Value >= bookRes.Result.LastModified)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }


            return Ok(bookRes.Result);
        }

        private async Task<RServiceResult<PublishStatus[]>> _GetUserVisiblePDFBooksStatusSetAsync(Guid loggedOnUserId, Guid sessionId)
        {
            RServiceResult<bool>
                canView =
                await _userPermissionChecker.Check
                    (
                        loggedOnUserId,
                        sessionId,
                        User.Claims.Any(c => c.Type == "Language") ? User.Claims.First(c => c.Type == "Language").Value : "fa-IR",
                        RMuseumSecurableItem.PDFLibraryEntityShortName,
                        RMuseumSecurableItem.ViewDraftOperationShortName
                        );
            if (!string.IsNullOrEmpty(canView.ExceptionString))
                return new RServiceResult<PublishStatus[]>(null, canView.ExceptionString);

            PublishStatus[] visibleItems =
                canView.Result
                ?
                new PublishStatus[]
                {
                    PublishStatus.Published,
                    PublishStatus.Restricted,
                    PublishStatus.Draft,
                    PublishStatus.Awaiting
                }
                :
                 new PublishStatus[]
                {
                    PublishStatus.Published
                };

            return new RServiceResult<PublishStatus[]>(visibleItems);
        }

        /// <summary>
        /// PDF Book contents by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("toc/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(RTitleInContents[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPDFBookTableOfContentsAsync(int id)
        {
            var contentRes = await _pdfService.GetPDFBookTableOfContentsAsync(id);

            if (!string.IsNullOrEmpty(contentRes.ExceptionString))
            {
                return BadRequest(contentRes.ExceptionString);
            }
            return Ok(contentRes.Result);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFBook))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPDFBookByIdAsync(int id, bool includePages = false, bool includeBookText = false, bool includePageText = false)
        {
            var bookRes = await _pdfService.GetPDFBookByIdAsync(id, [PublishStatus.Published], includePages, includeBookText, includePageText);

            if (!string.IsNullOrEmpty(bookRes.ExceptionString))
            {
                return BadRequest(bookRes.ExceptionString);
            }
            if (bookRes.Result == null)
                return NotFound();

            Response.GetTypedHeaders().LastModified = bookRes.Result.LastModified;

            var requestHeaders = Request.GetTypedHeaders();
            if (requestHeaders.IfModifiedSince.HasValue &&
                requestHeaders.IfModifiedSince.Value >= bookRes.Result.LastModified)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }


            return Ok(bookRes.Result);
        }


        /// <summary>
        /// start importing a local pdf file
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> StartImportingLocalPDFAsync([FromBody] NewPDFBookViewModel model)
        {
            var res = await _pdfService.StartImportingLocalPDFAsync(model);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }
        /// <summary>
        /// import from known sources
        /// </summary>
        /// <param name="srcUrl"></param>
        /// <returns></returns>

        [HttpPost("import")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> StartImportingKnownSourceAsync([FromBody]string srcUrl)
        {
            var res = await _pdfService.StartImportingKnownSourceAsync(srcUrl);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// queue import from know source
        /// </summary>
        /// <param name="srcUrl"></param>
        /// <returns></returns>
        [HttpPost("import/queue")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> QueueImportfFromKnownSourceAsync([FromBody] string srcUrl)
        {
            var res = await _pdfService.ImportfFromKnownSourceAsync(srcUrl, false);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// batch import soha library
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="finalizeDownload"></param>
        /// <returns></returns>
        [HttpPost("soha/{start}/{end}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult BatchImportSohaLibraryAsync(int start, int end, bool finalizeDownload)
        {
            _pdfService.BatchImportSohaLibraryAsync(start, end, finalizeDownload);
            return Ok();
        }

        
        /// <summary>
        /// batch import eliteraturebook.com library
        /// </summary>
        /// <param name="ajaxPageIndexStart">start from 0</param>
        /// <param name="ajaxPageIndexEnd"></param>
        /// <param name="finalizeDownload"></param>
        /// <returns></returns>
        [HttpPost("elit/{ajaxPageIndexStart}/{ajaxPageIndexEnd}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult BatchImportELiteratureBookLibraryAsync(int ajaxPageIndexStart, int ajaxPageIndexEnd, bool finalizeDownload)
        {
            _pdfService.BatchImportELiteratureBookLibraryAsync(ajaxPageIndexStart, ajaxPageIndexEnd, finalizeDownload);
            return Ok();
        }

        /// <summary>
        /// view import jobs status
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("jobs")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<ImportJob>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetImportJobs([FromQuery] PagingParameterModel paging)
        {
            RServiceResult<(PaginationMetadata PagingMeta, ImportJob[] Items)> itemsInfo = await _pdfService.GetImportJobs(paging);
            if (!string.IsNullOrEmpty(itemsInfo.ExceptionString))
            {
                return BadRequest(itemsInfo.ExceptionString);
            }
            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(itemsInfo.Result.PagingMeta));

            return Ok(itemsInfo.Result.Items);
        }


        /// <summary>
        /// edit pdf book master record (user should have additional permissions pdf:awaiting and pdf:publish to change status of pdf book)
        /// </summary>
        /// <remarks>
        /// editing related collections such as pages and attributed or complex properties such as CoverImage is ignored
        /// </remarks>
        /// <param name="pdf"></param>
        /// <returns></returns>
        [HttpPut]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> PutPDFBookAsync([FromBody] PDFBook pdf)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            Guid sessionId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value);

            RServiceResult<bool>
                canChangeStatusToAwaiting =
                await _userPermissionChecker.Check
                    (
                        loggedOnUserId,
                        sessionId,
                        User.Claims.Any(c => c.Type == "Language") ? User.Claims.First(c => c.Type == "Language").Value : "fa-IR",
                        RMuseumSecurableItem.PDFLibraryEntityShortName,
                        RMuseumSecurableItem.ToAwaitingStatusOperationShortName
                        );
            if (!string.IsNullOrEmpty(canChangeStatusToAwaiting.ExceptionString))
                return BadRequest(canChangeStatusToAwaiting.ExceptionString);

            RServiceResult<bool>
                canPublish =
                await _userPermissionChecker.Check
                    (
                        loggedOnUserId,
                        sessionId,
                        User.Claims.Any(c => c.Type == "Language") ? User.Claims.First(c => c.Type == "Language").Value : "fa-IR",
                        RMuseumSecurableItem.PDFLibraryEntityShortName,
                        RMuseumSecurableItem.PublishOperationShortName
                        );
            if (!string.IsNullOrEmpty(canPublish.ExceptionString))
                return BadRequest(canPublish.ExceptionString);

            RServiceResult<PDFBook> itemInfo = await _pdfService.EditPDFBookMasterRecordAsync(pdf, canChangeStatusToAwaiting.Result, canPublish.Result);
            if (!string.IsNullOrEmpty(itemInfo.ExceptionString))
            {
                return BadRequest(itemInfo.ExceptionString);
            }

            if (itemInfo == null)
            {
                return NotFound();
            }

            return Ok();
        }

        /// <summary>
        /// Copy PDF Book Cover Image From Page Thumbnail image
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pageId"></param>
        /// <returns></returns>
        [HttpPut("{id}/cover/{pageId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> SetPDFBookCoverImageFromPageAsync(int id, int pageId)
        {
            RServiceResult<bool> res = await _pdfService.SetPDFBookCoverImageFromPageAsync(id, pageId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest();
            return Ok();
        }

        /// <summary>
        /// remove unpublished pdf book
        /// </summary>
        /// <param name="bookId"></param>
        /// <returns></returns>
        [HttpDelete("{bookId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.DeleteOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> RemovePDFBookAsync(int bookId)
        {
            RServiceResult<bool> res = await _pdfService.RemovePDFBookAsync(bookId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            // opportunistically reclaim storage right away; safe/cheap to call even if there's
            // nothing pending, and safe to call again later if this particular run doesn't finish
            _pdfService.StartCleaningUpPendingPDFStorageAsync();
            return Ok(res.Result);
        }

        /// <summary>
        /// add new tag value to pdf book
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="tag">only name is processed</param>
        /// <returns></returns>
        [HttpPost("tagvalue/{pdfBookId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + RMuseumSecurableItem.EditTagValueOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(RTagValue))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> TagPDFBookAsync(int pdfBookId, [FromBody] RTag tag)
        {
            RServiceResult<RTagValue> res = await _pdfService.TagPDFBookAsync(pdfBookId, tag);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// edit pdf book attribute value
        /// </summary>
        /// <remarks>
        /// editable fields are limited
        /// </remarks>
        /// <param name="pdfBookId"></param>
        /// <param name="tagvalue"></param>
        /// <param name="global">apply on all same value tags</param>
        /// <returns></returns>
        [HttpPut("tagvalue/{pdfBookId}/{global=true}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + RMuseumSecurableItem.EditTagValueOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> EditPDFBookTagValueAsync(int pdfBookId, bool global, [FromBody] RTagValue tagvalue)
        {

            RServiceResult<RTagValue> itemInfo = await _pdfService.EditPDFBookTagValueAsync(pdfBookId, tagvalue, global);
            if (!string.IsNullOrEmpty(itemInfo.ExceptionString))
            {
                return BadRequest(itemInfo.ExceptionString);
            }

            if (itemInfo == null)
            {
                return NotFound();
            }

            return Ok(); ;
        }

        /// <summary>
        /// remove tag from pdf book
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="tagValueId"></param>
        /// <returns></returns>
        [HttpDelete("tagvalue/{pdfBookId}/{tagValueId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + RMuseumSecurableItem.EditTagValueOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> UnTagPDFBookAsync(int pdfBookId, Guid tagValueId)
        {
            RServiceResult<bool> res = await _pdfService.UnTagPDFBookAsync(pdfBookId, tagValueId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// get tagged publish pdfbooks (including CoverImage info but not pages or tagibutes info) 
        /// </summary>
        /// <param name="tagUrl"></param>
        /// <param name="valueUrl"></param>
        /// <returns></returns>

        [HttpGet("tagged/{tagUrl}/{valueUrl}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFBook>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetByTagValueAsync(string tagUrl, string valueUrl)
        {
            RServiceResult<PDFBook[]> itemsInfo = await _pdfService.GetPDFBookByTagValueAsync(tagUrl, valueUrl, new PublishStatus[] { PublishStatus.Published });
            if (!string.IsNullOrEmpty(itemsInfo.ExceptionString))
            {
                return BadRequest(itemsInfo.ExceptionString);
            }

            if (itemsInfo.Result.Length > 0)
            {
                DateTime lastModification = itemsInfo.Result.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            return Ok(itemsInfo.Result);
        }

        /// <summary>
        /// get authors
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorName"></param>
        /// <returns></returns>
        [HttpGet("author")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<Author>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetAuthorsAsync([FromQuery] PagingParameterModel paging, string authorName = null)
        {
            var authorsRes = await _pdfService.GetAuthorsAsync(paging, authorName);
            if (!string.IsNullOrEmpty(authorsRes.ExceptionString))
            {
                return BadRequest(authorsRes.ExceptionString);
            }

            if (authorsRes.Result.Authors.Count() > 0)
            {
                DateTime lastModification = authorsRes.Result.Authors.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(authorsRes.Result.PagingMeta));

            return Ok(authorsRes.Result.Authors);
        }

        /// <summary>
        /// list authors with a computed count of distinct books they're credited on, optionally
        /// filtered to a single role (e.g. "مترجم"/"نویسنده"/"مصحح" - see the distinct values in
        /// AuthorRole.Role) and/or by part of the author's name, sortable by name (ascending) or
        /// by book count (descending), paginated.
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="role">exact AuthorRole.Role to filter to; omit for all roles combined</param>
        /// <param name="sortByBookCount">true: sort by book count descending; false (default): sort by name ascending</param>
        /// <param name="authorName">part of the author's name; omit for no name filter</param>
        /// <returns></returns>
        [HttpGet("authors")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<AuthorWithBookCount>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetAuthorsWithBookCountAsync([FromQuery] PagingParameterModel paging, string role = null, bool sortByBookCount = false, string authorName = null)
        {
            var authorsRes = await _pdfService.GetAuthorsWithBookCountAsync(paging, role, sortByBookCount, authorName);
            if (!string.IsNullOrEmpty(authorsRes.ExceptionString))
            {
                return BadRequest(authorsRes.ExceptionString);
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(authorsRes.Result.PagingMeta));

            return Ok(authorsRes.Result.Authors);
        }

        /// <summary>
        /// get author by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("author/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(Author))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetAuthorByIdAsync(int id)
        {

            var authorsRes = await _pdfService.GetAuthorByIdAsync(id);
            if (!string.IsNullOrEmpty(authorsRes.ExceptionString))
            {
                return BadRequest(authorsRes.ExceptionString);
            }

            if(authorsRes.Result == null)
            {
                return NotFound();
            }

            return Ok(authorsRes.Result);
        }

        /// <summary>
        /// add a new author
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>

        [HttpPost("author")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(Author))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> AddAuthorAsync([FromBody] Author author)
        {
            var res = await _pdfService.AddAuthorAsync(author);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// edit an existing author
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>

        [HttpPut("author")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> UpdateAuthorAsync([FromBody] Author author)
        {
            var res = await _pdfService.UpdateAuthorAsync(author);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// delete an existing author
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpDelete("author/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.DeleteOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeleteAuthorAsync(int id)
        {
            var res = await _pdfService.DeleteAuthorAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        [HttpPost("pdfbook/{pdfBookId}/contributor")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> AddPDFBookContributerAsync(int pdfBookId, [FromBody] AuthorRole role)
        {
            var res = await _pdfService.AddPDFBookContributerAsync(pdfBookId, role.Author.Id, role.Role);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }


        /// <summary>
        /// delete an existing contribution from pdf book
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="contributorRecordId"></param>
        /// <returns></returns>

        [HttpDelete("pdfbook/{pdfBookId}/contributor/{contributorRecordId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeletePDFBookContributerAsync(int pdfBookId, int contributorRecordId)
        {
            var res = await _pdfService.DeletePDFBookContributerAsync(pdfBookId, contributorRecordId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// pdf book by contributer
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        [HttpGet("pdfbook/by/contributer/{authorId}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFBook>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetPublishedPDFBooksByAuthorAsync([FromQuery] PagingParameterModel paging, int authorId, string role = null)
        {
            var pdfBooksInfo = await _pdfService.GetPublishedPDFBooksByAuthorAsync(paging, authorId, role);
            if (!string.IsNullOrEmpty(pdfBooksInfo.ExceptionString))
            {
                return BadRequest(pdfBooksInfo.ExceptionString);
            }

            if (pdfBooksInfo.Result.Books.Count() > 0)
            {
                DateTime lastModification = pdfBooksInfo.Result.Books.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pdfBooksInfo.Result.PagingMeta));

            return Ok(pdfBooksInfo.Result.Books);
        }

        /// <summary>
        /// get published pdf books by author stats (group by role)
        /// </summary>
        /// <param name="authorId"></param>
        /// <returns></returns>
        [HttpGet("pdfbook/by/contributer/{authorId}/groupby/role")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<AuthorRoleCount>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetPublishedPDFBookbyAuthorGroupedByRoleAsync(int authorId)
        {
            var res = await _pdfService.GetPublishedPDFBookbyAuthorGroupedByRoleAsync(authorId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            return Ok(res.Result);
        }

        /// <summary>
        /// get all books
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>

        [HttpGet("book")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<Book>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetAllBooksAsync([FromQuery] PagingParameterModel paging)
        {
            var res = await _pdfService.GetAllBooksAsync(paging);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            if (res.Result.Books.Count() > 0)
            {
                DateTime lastModification = res.Result.Books.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            return Ok(res.Result.Books);
        }

        /// <summary>
        /// book by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("book/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(Book))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetBookByIdAsync(int id)
        {
            var res = await _pdfService.GetBookByIdAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            return Ok(res.Result);
        }

        /// <summary>
        /// add a new book
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>

        [HttpPost("book")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(Book))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> AddBookAsync([FromBody] Book book)
        {
            var res = await _pdfService.AddBookAsync(book);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// update book
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        [HttpPut("book")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> UpdateBookAsync([FromBody] Book book)
        {
            var res = await _pdfService.UpdateBookAsync(book);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// delete book
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("book")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.DeleteOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeleteBookAsync(int id)
        {
            var res = await _pdfService.DeleteBookAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// add book author
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        [HttpPost("book/{bookId}/author")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> AddBookAuthorAsync(int bookId, [FromBody] AuthorRole role)
        {
            var res = await _pdfService.AddBookAuthorAsync(bookId, role.Author.Id, role.Role);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }


        /// <summary>
        /// delete an existing author from book
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="authorRecordId"></param>
        /// <returns></returns>

        [HttpDelete("book/{bookId}/author/{authorRecordId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeleteBookAuthorAsync(int bookId, int authorRecordId)
        {
            var res = await _pdfService.DeleteBookAuthorAsync(bookId, authorRecordId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// books by author
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        [HttpGet("book/by/author/{authorId}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<Book>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetBooksByAuthorAsync([FromQuery] PagingParameterModel paging, int authorId, string role = null)
        {
            var res = await _pdfService.GetBooksByAuthorAsync(paging, authorId, role);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            if (res.Result.Books.Count() > 0)
            {
                DateTime lastModification = res.Result.Books.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            return Ok(res.Result.Books);
        }

        /// <summary>
        /// get books by author stats (group by role)
        /// </summary>
        /// <param name="authorId"></param>
        /// <returns></returns>
        [HttpGet("book/by/author/{authorId}/groupby/role")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<AuthorRoleCount>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetBookbyAuthorGroupedByRoleAsync(int authorId)
        {
            var res = await _pdfService.GetBookbyAuthorGroupedByRoleAsync(authorId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            return Ok(res.Result);
        }

        /// <summary>
        /// get book related pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="sourceId"></param>
        /// <returns></returns>

        [HttpGet("book/{bookId}/pdfs")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFBook>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetBookRelatedPDFBooksAsync([FromQuery] PagingParameterModel paging, int sourceId)
        {
            var res = await _pdfService.GetBookRelatedPDFBooksAsync(paging, sourceId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            if (res.Result.Books.Count() > 0)
            {
                DateTime lastModification = res.Result.Books.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            return Ok(res.Result.Books);
        }

        /// <summary>
        /// volumes by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpGet("volumes/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(MultiVolumePDFCollection))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetMultiVolumePDFCollectionByIdAsync(int id)
        {
            var res = await _pdfService.GetMultiVolumePDFCollectionByIdAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            return Ok(res.Result);
        }


        /// <summary>
        /// add a new multi volume pdf collection
        /// </summary>
        /// <param name="volumes"></param>
        /// <returns></returns>
        [HttpPost("volumes")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(MultiVolumePDFCollection))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> AddMultiVolumePDFCollectionAsync([FromBody] MultiVolumePDFCollection volumes)
        {
            var res = await _pdfService.AddMultiVolumePDFCollectionAsync(volumes);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// update multi volume pdf collection
        /// </summary>
        /// <param name="volumes"></param>
        /// <returns></returns>

        [HttpPut("volumes")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> UpdateMultiVolumePDFCollectionAsync([FromBody] MultiVolumePDFCollection volumes)
        {
            var res = await _pdfService.UpdateMultiVolumePDFCollectionAsync(volumes);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// delete multi volume pdf collection
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("volumes")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.DeleteOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeleteMultiVolumePDFCollectionAsync(int id)
        {
            var res = await _pdfService.DeleteMultiVolumePDFCollectionAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }
        /// <summary>
        /// get volumes pdf books
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        [HttpGet("volumes/{volumeId}/pdfs")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFBook>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetVolumesPDFBooks(int volumeId)
        {
            var res = await _pdfService.GetVolumesPDFBooks(volumeId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            return Ok(res.Result);
        }

        /// <summary>
        /// get all sources
        /// </summary>
        /// <returns></returns>

        [HttpGet("source")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFSource>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetPDFSourcesAsync()
        {
            var res = await _pdfService.GetPDFSourcesAsync();
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            return Ok(res.Result);
        }

        /// <summary>
        /// source by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("source/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFSource))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetPDFSourceByIdAsync(int id)
        {
            var res = await _pdfService.GetPDFSourceByIdAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            return Ok(res.Result);
        }

        /// <summary>
        /// add a new source
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>

        [HttpPost("source")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.AddOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFSource))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> AddPDFSourceAsync([FromBody] PDFSource source)
        {
            var res = await _pdfService.AddPDFSourceAsync(source);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// update source
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        [HttpPut("source")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> UpdatePDFSourceAsync([FromBody] PDFSource source)
        {
            var res = await _pdfService.UpdatePDFSourceAsync(source);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// delete book
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("source")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.DeleteOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeletePDFSourceAsync(int id)
        {
            var res = await _pdfService.DeletePDFSourceAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// get pdf source pdfs
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="sourceId"></param>
        /// <returns></returns>
        [HttpGet("source/{sourceId}/pdfs")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFBook>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetSourceRelatedPDFBooksAsync([FromQuery] PagingParameterModel paging, int sourceId)
        {
            var res = await _pdfService.GetSourceRelatedPDFBooksAsync(paging, sourceId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            if (res.Result.Books.Count() > 0)
            {
                DateTime lastModification = res.Result.Books.Max(i => i.LastModified);
                Response.GetTypedHeaders().LastModified = lastModification;

                var requestHeaders = Request.GetTypedHeaders();
                if (requestHeaders.IfModifiedSince.HasValue &&
                    requestHeaders.IfModifiedSince.Value >= lastModification)
                {
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            return Ok(res.Result.Books);
        }

        /// <summary>
        /// search pdf books (titles and authors and translators and tags)
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("search")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<RArtifactMasterRecord>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> SearchPDFBooksAsync([FromQuery] PagingParameterModel paging, string term)
        {
            var pagedResult = await _pdfService.SearchPDFBooksAsync(paging, term);
            if (!string.IsNullOrEmpty(pagedResult.ExceptionString))
                return BadRequest(pagedResult.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pagedResult.Result.PagingMeta));

            return Ok(pagedResult.Result.Items);
        }

        /// <summary>
        /// search pdf book pages text
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="id"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("search/pdfbook/{id}/text")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<RArtifactMasterRecord>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> SearchPDFPagesTextAsync([FromQuery] PagingParameterModel paging, int id, string term)
        {
            var pagedResult = await _pdfService.SearchPDFPagesTextAsync(paging, id, term);
            if (!string.IsNullOrEmpty(pagedResult.ExceptionString))
                return BadRequest(pagedResult.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pagedResult.Result.PagingMeta));

            return Ok(pagedResult.Result.Items);
        }

        /// <summary>
        /// search book text
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("search/pages/text")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<RArtifactMasterRecord>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> SearchPDFBookForPDFPagesTextAsync([FromQuery] PagingParameterModel paging, string term)
        {
            var pagedResult = await _pdfService.SearchPDFBookForPDFPagesTextAsync(paging, term);
            if (!string.IsNullOrEmpty(pagedResult.ExceptionString))
                return BadRequest(pagedResult.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pagedResult.Result.PagingMeta));

            return Ok(pagedResult.Result.Books);
        }

        /// <summary>
        /// suggest ganjoor link
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("ganjoor")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> SuggestGanjoorLinkAsync([FromBody] PDFGanjoorLinkSuggestion link)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> suggestion = await _pdfService.SuggestGanjoorLinkAsync(loggedOnUserId, link);
            if (!string.IsNullOrEmpty(suggestion.ExceptionString))
                return BadRequest(suggestion.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// is book link to ganjoor poem
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="poemId"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("ganjoor/islinked/book/{bookId}/poem/{poemId}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> IsBookRelatedToPoemAsync(int bookId, int poemId)
        {
            var res = await _pdfService.IsBookRelatedToPoemAsync(bookId, poemId);
            if(!string.IsNullOrEmpty(res.ExceptionString)) return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// finds next awaiting suggested link 
        /// return value might be null (has paging-headers)
        /// </summary>
        /// <remarks>has paging-headers</remarks>
        /// <param name="skip"></param>
        /// <param name="onlyMachineSuggested"></param>
        /// <returns> return value might be null </returns>
        [HttpGet]
        [Route("ganjoor/nextunreviewed")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(GanjoorLinkViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetNextUnreviewedGanjoorLinkAsync(int skip, bool onlyMachineSuggested = false)
        {
            RServiceResult<GanjoorLinkViewModel> res = await _pdfService.GetNextUnreviewedGanjoorLinkAsync(skip, onlyMachineSuggested);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            var resCount = await _pdfService.GetUnreviewedGanjoorLinksCountAsync();
            if (!string.IsNullOrEmpty(resCount.ExceptionString))
                return BadRequest(resCount.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers",
                JsonConvert.SerializeObject(
                    new PaginationMetadata()
                    {
                        totalCount = resCount.Result,
                        pageSize = -1,
                        currentPage = -1,
                        hasNextPage = false,
                        hasPreviousPage = false,
                        totalPages = -1
                    })
                );
            return Ok(res.Result);
        }

        /// <summary>
        /// review suggested ganjoor link
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("ganjoor/review/{linkId}/{result}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + RMuseumSecurableItem.ReviewGanjoorLinksOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ReviewSuggestedLinkAsync(Guid linkId, ReviewResult result)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> suggestion = await _pdfService.ReviewSuggestedLinkAsync(linkId, loggedOnUserId, result);
            if (!string.IsNullOrEmpty(suggestion.ExceptionString))
                return BadRequest(suggestion.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// ganjoor approved unsycned links
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("ganjoor/unsynched")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + RMuseumSecurableItem.ReviewGanjoorLinksOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFGanjoorLink[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUnsyncedPDFGanjoorLinksAsync()
        {
            RServiceResult<PDFGanjoorLink[]> res = await _pdfService.GetUnsyncedPDFGanjoorLinksAsync();
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// synchronize ganjoor link
        /// </summary>
        /// <param name="linkId"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("ganjoor/sync/{linkId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + RMuseumSecurableItem.ReviewGanjoorLinksOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> SynchronizePDFGanjoorLinkAsync(Guid linkId)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> suggestion = await _pdfService.SynchronizePDFGanjoorLinkAsync(linkId);
            if (!string.IsNullOrEmpty(suggestion.ExceptionString))
                return BadRequest(suggestion.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// next un-ocred pdf book
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("ocr/nextunocred")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFBook))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetNextUnOCRedPDFBookAsync()
        {
            RServiceResult<PDFBook> res = await _pdfService.GetNextUnOCRedPDFBookAsync();
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
           
            return Ok(res.Result);
        }

        /// <summary>
        /// set page ocr info
        /// </summary>
        /// <param name="pdf"></param>
        /// <returns></returns>

        [HttpPut]
        [Route("ocr")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SetPDFPageOCRInfoAsync([FromBody] PDFPageOCRDataViewModel pdf)
        {

            RServiceResult<bool> res = await _pdfService.SetPDFPageOCRInfoAsync(pdf);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok();
        }

        /// <summary>
        /// reset ocr queue
        /// </summary>
        /// <returns></returns>
        [HttpDelete("ocr/queue")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ResetOCRQueueAsync()
        {
            var res = await _pdfService.ResetOCRQueueAsync();
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// next un-AIed pdf book
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("ai/nextunaid")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFBook))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetNextUnAIedPDFBookAsync()
        {
            RServiceResult<PDFBook> res = await _pdfService.GetNextUnAIedPDFBookAsync();
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok(res.Result);
        }

        /// <summary>
        /// reset AI queue
        /// </summary>
        /// <returns></returns>
        [HttpDelete("ai/queue")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ResetAIQueueAsync()
        {
            var res = await _pdfService.ResetAIQueueAsync();
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// fill book text
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Route("ocr/fillbooktext")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public IActionResult StartFillingMissingBookTextsAsync()
        {

            _pdfService.StartFillingMissingBookTextsAsync();
            return Ok();
        }

        /// <summary>
        /// page of published book by page number
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        [HttpGet("{pdfBookId}/page/{pageNumber}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFPage))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPDFPageAsync(int pdfBookId, int pageNumber)
        {
            // logged-in only, matching PDFStudyLogEntry's schema (see its doc comment) -
            // anonymous page views simply aren't tracked anymore rather than being forced into
            // a table that's also the user's own synced reading history. Best-effort: a failed
            // write here shouldn't stop the page itself from loading.
            var loggedOnUserIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (loggedOnUserIdClaim != null)
            {
                await _userSyncService.RecordStudyLogEntryAsync(new Guid(loggedOnUserIdClaim.Value), pdfBookId, pageNumber);
            }

            var bookRes = await _pdfService.GetPDFPageAsync(pdfBookId, pageNumber);

            if (!string.IsNullOrEmpty(bookRes.ExceptionString))
            {
                return BadRequest(bookRes.ExceptionString);
            }
            if (bookRes.Result == null)
                return NotFound();

            Response.GetTypedHeaders().LastModified = bookRes.Result.LastModified;

            var requestHeaders = Request.GetTypedHeaders();
            if (requestHeaders.IfModifiedSince.HasValue &&
                requestHeaders.IfModifiedSince.Value >= bookRes.Result.LastModified)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }


            return Ok(bookRes.Result);
        }

        /// <summary>
        /// queued downloding pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet("q")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<QueuedPDFBook>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetQueuedPDFBooksAsync([FromQuery] PagingParameterModel paging)
        {
            var pdfBooksInfo = await _pdfService.GetQueuedPDFBooksAsync(paging);
            if (!string.IsNullOrEmpty(pdfBooksInfo.ExceptionString))
            {
                return BadRequest(pdfBooksInfo.ExceptionString);
            }


            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pdfBooksInfo.Result.PagingMeta));

            return Ok(pdfBooksInfo.Result.Books);
        }

        /// <summary>
        /// delete queued books
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpDelete("q")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.DeleteOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> DeleteQueuedPDFBookAsync(Guid id)
        {
            RServiceResult<bool> res = await _pdfService.DeleteQueuedPDFBookAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// mix queued pdf books 
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("q/mix/{step}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> MixQueuedPDFBooksAsync(int step = 10)
        {
            var res = await _pdfService.MixQueuedPDFBooksAsync(step);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok();
        }

        /// <summary>
        /// start processing queue pdf books
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("q/process/{count}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public IActionResult StartProcessingQueuedPDFBooks(int count = 1000)
        {

            _pdfService.StartProcessingQueuedPDFBooks(count);
            return Ok();
        }

        /// <summary>
        /// switch book mark
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="pageNumber">send 0 for the whole book</param>
        /// <param name="note"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("bookmark/{pdfBookId}/{pageNumber}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFUserBookmark))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SwitchBookmarkAsync(int pdfBookId, int? pageNumber, [FromBody] string note)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _bookmarkingService.SwitchBookmarkAsync(pdfBookId, loggedOnUserId, pageNumber, note);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok(res);
        }

        /// <summary>
        /// get user bookmarks
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="pdfBookId"></param>
        /// <param name="pageNo"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("bookmark/{pdfBookId}/{pageNo}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFUserBookmarkViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetBookmarksAsync([FromQuery] PagingParameterModel paging, int? pdfBookId, int? pageNo)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _bookmarkingService.GetBookmarksAsync(paging, loggedOnUserId, pdfBookId, pageNo);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            return Ok(res.Result.Bookmarks);
        }

        /// <summary>
        /// delete all bookmarks
        /// </summary>
        /// <returns></returns>
        [HttpDelete("all")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFVisistViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> DeleteAllBookmarks()
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _bookmarkingService.DeleteAllBookmarks(loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// get user last activity
        /// </summary>
        /// <returns></returns>
        [HttpGet("visits")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFVisistViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetUserLastActivityAsync()
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _pdfService.GetUserLastActivityAsync(loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// get matchings
        /// </summary>
        /// <param name="notStarted"></param>
        /// <param name="notFinished"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("ganjoor/matching")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(GanjoorPoemMatchFinding[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetGanjoorPoemMatchQueueAsync(bool notStarted = false, bool notFinished = true)
        {
            var res = await _pdfService.GetGanjoorPoemMatchQueueAsync(notStarted, notFinished);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// queue ganjoor poem match finding
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("ganjoor/matching")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> QueueGanjoorPoemMatchAsync([FromBody] GanjoorPoemMatchViewModel model)
        {
            var res = await _pdfService.QueueGanjoorPoemMatchAsync(model);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok();
        }

        /// <summary>
        /// update a ganjoor poem match finding
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>

        [HttpPut]
        [Route("ganjoor/matching")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> UpdateGanjoorPoemMatchFindingAsync([FromBody] GanjoorPoemMatchFinding model)
        {
            model.LastUpdatedByUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _pdfService.UpdateGanjoorPoemMatchFindingAsync(model);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok();
        }

        /// <summary>
        /// put first verse to ganjoor toc titles
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Route("ganjoor/toc/fill")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public IActionResult StartCompletingGanjoorTOCVersesAsync()
        {

            _pdfService.StartCompletingGanjoorTOCVersesAsync();
            return Ok();
        }

        /// <summary>
        /// start scanning PDFBooks for possible duplicates and queue the findings for human review
        /// </summary>
        /// <param name="forceRestart">if true, restart the title-comparison pass from scratch instead of resuming an interrupted run (use after manually clearing PDFBookDuplicateCandidates)</param>
        /// <returns></returns>
        [HttpPut]
        [Route("duplicates/detect")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public IActionResult StartDetectingDuplicatePDFBooksAsync(bool forceRestart = false)
        {
            _pdfService.StartDetectingDuplicatePDFBooksAsync(forceRestart);
            return Ok();
        }

        /// <summary>
        /// current duplicate-detection progress/resume state (title-fuzzy-matching pass)
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("duplicates/detect/state")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PDFBookDuplicateDetectionState))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPDFBookDuplicateDetectionStateAsync()
        {
            var res = await _pdfService.GetPDFBookDuplicateDetectionStateAsync();
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// get duplicate candidates queue - check paging-headers for paging info
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="status">defaults to New if not specified</param>
        /// <returns></returns>
        [HttpGet]
        [Route("duplicates")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PDFBookDuplicateCandidate>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPDFBookDuplicateCandidatesAsync([FromQuery] PagingParameterModel paging, [FromQuery] PDFBookDuplicateCandidateStatus[] status)
        {
            var statusArray = (status == null || status.Length == 0) ? new[] { PDFBookDuplicateCandidateStatus.New } : status;
            var res = await _pdfService.GetPDFBookDuplicateCandidatesAsync(paging, statusArray);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            return Ok(res.Result.Items);
        }

        /// <summary>
        /// update a duplicate candidate's review decision (survivor choice / status / note)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("duplicates")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> UpdatePDFBookDuplicateCandidateAsync([FromBody] PDFBookDuplicateCandidate model)
        {
            model.ReviewerId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _pdfService.UpdatePDFBookDuplicateCandidateAsync(model);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok();
        }

        /// <summary>
        /// execute a Confirmed duplicate candidate's merge - fills metadata gaps, repoints
        /// references, redirects the merged-away duplicate's id to the survivor, and removes the
        /// duplicate's PDFBook row (queuing its storage for cleanup). The candidate must already
        /// be in Confirmed status (set via PUT duplicates) before this will do anything.
        /// </summary>
        /// <param name="id">duplicate candidate id</param>
        /// <returns></returns>
        [HttpPut]
        [Route("duplicates/{id}/merge")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> MergePDFBookDuplicateAsync(Guid id)
        {
            var reviewerId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _pdfService.MergePDFBookDuplicateAsync(id, reviewerId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            // reclaim the merged-away duplicate's storage right away; safe/cheap to call, and
            // safe to call again later (via storage-cleanup) if this run doesn't finish
            _pdfService.StartCleaningUpPendingPDFStorageAsync();
            return Ok();
        }

        /// <summary>
        /// start merging EVERY Confirmed duplicate candidate in a single background job, instead
        /// of one at a time via PUT duplicates/{id}/merge. Each candidate is still merged in its
        /// own transaction; a candidate whose merge fails is skipped for this run (recorded in its
        /// ReviewNote, left as Confirmed) rather than blocking the rest of the batch.
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Route("duplicates/merge-all-confirmed")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public IActionResult MergeAllConfirmedPDFBookDuplicatesAsync()
        {
            _pdfService.StartMergingConfirmedPDFBookDuplicatesAsync();
            return Ok();
        }

        /// <summary>
        /// manually merge two PDFBooks by id directly, without needing a pre-existing duplicate-
        /// candidate row - for an operator who spots a duplicate directly (e.g. while browsing)
        /// rather than through the automated detection queue. Requires PDFBook delete permission,
        /// matching RemovePDFBookAsync, since duplicatePDFBookId ends up removed either way.
        /// </summary>
        /// <param name="survivorPDFBookId">the PDFBook id that stays and receives the merged data</param>
        /// <param name="duplicatePDFBookId">the PDFBook id that gets merged away and removed</param>
        /// <returns></returns>
        [HttpPut]
        [Route("merge/{survivorPDFBookId}/{duplicatePDFBookId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.DeleteOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> MergePDFBooksByIdAsync(int survivorPDFBookId, int duplicatePDFBookId)
        {
            var reviewerId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _pdfService.MergePDFBooksByIdAsync(survivorPDFBookId, duplicatePDFBookId, reviewerId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            // reclaim the merged-away duplicate's storage right away; safe/cheap to call, and
            // safe to call again later (via storage-cleanup) if this run doesn't finish
            _pdfService.StartCleaningUpPendingPDFStorageAsync();
            return Ok();
        }

        /// <summary>
        /// merge two Author records by id (no redirect - see MergeAuthorsByIdAsync's doc comment)
        /// </summary>
        /// <param name="survivorAuthorId">the Author id that stays</param>
        /// <param name="duplicateAuthorId">the Author id that gets merged away and removed</param>
        /// <returns></returns>
        [HttpPut]
        [Route("author/merge/{survivorAuthorId}/{duplicateAuthorId}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.DeleteOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> MergeAuthorsByIdAsync(int survivorAuthorId, int duplicateAuthorId)
        {
            var res = await _pdfService.MergeAuthorsByIdAsync(survivorAuthorId, duplicateAuthorId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok();
        }

        /// <summary>
        /// delete a duplicate candidate row (e.g. a false positive)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("duplicates/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeletePDFBookDuplicateCandidateAsync(Guid id)
        {
            var res = await _pdfService.DeletePDFBookDuplicateCandidateAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok();
        }

        /// <summary>
        /// start physically cleaning up storage folders (FTP + local disk) queued by removed/merged
        /// PDFBooks. Safe to call repeatedly / after an interruption.
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Route("storage-cleanup")]
        [Authorize(Policy = RMuseumSecurableItem.PDFLibraryEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public IActionResult StartCleaningUpPendingPDFStorageAsync()
        {
            _pdfService.StartCleaningUpPendingPDFStorageAsync();
            return Ok();
        }


        /// <summary>
        /// PDF Service
        /// </summary>
        protected readonly IPDFLibraryService _pdfService;

        /// <summary>
        /// IUserPermissionChecker instance
        /// </summary>
        protected IUserPermissionChecker _userPermissionChecker;

        /// <summary>
        /// bookmarking service
        /// </summary>
        protected readonly IPDFBookmarkService _bookmarkingService;

        /// <summary>
        /// shelves/study-log sync service (also used here to record a study log entry for a
        /// logged-in user's page view - see GetPDFPageAsync)
        /// </summary>
        protected readonly IPDFUserSyncService _userSyncService;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="pdfService"></param>
        /// <param name="userPermissionChecker"></param>
        /// <param name="bookmarkingService"></param>
        /// <param name="userSyncService"></param>
        public PDFLibraryController(IPDFLibraryService pdfService, IUserPermissionChecker userPermissionChecker, IPDFBookmarkService bookmarkingService, IPDFUserSyncService userSyncService)
        {
            _pdfService = pdfService;
            _userPermissionChecker = userPermissionChecker;
            _bookmarkingService = bookmarkingService;
            _userSyncService = userSyncService;
        }
    }
}
