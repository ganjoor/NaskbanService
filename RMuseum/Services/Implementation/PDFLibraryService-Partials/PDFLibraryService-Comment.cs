using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RMuseum.Models.Artifact;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Models.Image;
using RSecurityBackend.Models.Notification;
using RSecurityBackend.Services;
using RSecurityBackend.Services.Implementation;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// submit a page comment, or a reply to one - Phase 1 fields (Text/InReplyToId) plus
        /// Phase 2's optional highlighted region (image + fractional coordinates, see
        /// PDFPageComment's own doc comment). Any authenticated user.
        ///
        /// Keyed by (pdfBookId, pageNumber), not the internal PDFPage.Id - a caller always
        /// already has book id + page number in hand and shouldn't need a separate round-trip
        /// just to resolve an internal id first before it can even submit a comment. Internally
        /// this still resolves to a PDFPage row and still uses its Id for the actual
        /// PDFPageComment.PDFPageId foreign key - only the public-facing identifier changes,
        /// not the underlying schema.
        /// </summary>
        public async Task<RServiceResult<Guid>> SubmitPDFPageCommentAsync(Guid userId, int pdfBookId, int pageNumber, PDFPageCommentPostViewModel model, IFormFile image)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Text))
                {
                    return new RServiceResult<Guid>(Guid.Empty, "متن دیدگاه نمی‌تواند خالی باشد");
                }

                // all four highlight coordinates must arrive together, or not at all - a
                // partial set (e.g. only HighlightX sent) is meaningless and would silently
                // corrupt the highlighted rectangle if allowed through
                bool anyHighlightField = model.HighlightX != null || model.HighlightY != null || model.HighlightWidth != null || model.HighlightHeight != null;
                bool allHighlightFields = model.HighlightX != null && model.HighlightY != null && model.HighlightWidth != null && model.HighlightHeight != null;
                if (anyHighlightField && !allHighlightFields)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "مختصات هایلایت باید همگی یا هیچ‌کدام ارسال شوند");
                }
                if (allHighlightFields && image == null)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "برای دیدگاه دارای هایلایت، تصویر بخش هایلایت‌شده الزامی است");
                }

                var page = await _context.PDFPages.AsNoTracking()
                    .Where(p => p.PDFBookId == pdfBookId && p.PageNumber == pageNumber)
                    .SingleOrDefaultAsync();
                if (page == null)
                {
                    return new RServiceResult<Guid>(Guid.Empty, $"page {pageNumber} of book {pdfBookId} not found");
                }
                int pdfPageId = page.Id;

                // declared here (rather than inside the if block below, where it's assigned)
                // so it's still in scope further down, where a successful reply needs its
                // parent's UserId to notify - stays null for a top-level (non-reply) comment
                PDFPageComment parent = null;
                if (model.InReplyToId != null)
                {
                    parent = await _context.PDFPageComments.AsNoTracking()
                        .Where(c => c.Id == model.InReplyToId.Value)
                        .SingleOrDefaultAsync();
                    if (parent == null)
                    {
                        return new RServiceResult<Guid>(Guid.Empty, $"comment {model.InReplyToId} not found");
                    }
                    if (parent.PDFPageId != pdfPageId)
                    {
                        // a reply must stay on the same page as the comment it replies to -
                        // otherwise a client bug (or a malicious request) could attach a reply
                        // to a page that never showed its parent at all
                        return new RServiceResult<Guid>(Guid.Empty, "دیدگاه مرجع مربوط به این صفحه نیست");
                    }
                }

                Guid? imageId = null;
                if (image != null)
                {
                    var imageRes = await _imageFileService.Add(image, null, image.FileName, "PDFPageCommentImages");
                    if (!string.IsNullOrEmpty(imageRes.ExceptionString))
                    {
                        return new RServiceResult<Guid>(Guid.Empty, imageRes.ExceptionString);
                    }
                    // Add() writes the file and builds the RImage object, but does not persist
                    // it to the database on its own - confirmed directly by the FK violation
                    // this caused (INSERT on PDFPageComments failing against
                    // FK_PDFPageComments_GeneralImages_ImageId, "the conflict occurred ... table
                    // GeneralImages, column Id") when only the returned Id was referenced below
                    // without the row actually existing yet. Explicitly tracked and inserted
                    // here instead, in the same SaveChangesAsync call as the comment itself,
                    // rather than assuming Add already committed it.
                    _context.Set<RImage>().Add(imageRes.Result);
                    imageId = imageRes.Result.Id;
                }

                var comment = new PDFPageComment()
                {
                    Id = Guid.NewGuid(),
                    PDFPageId = pdfPageId,
                    UserId = userId,
                    Text = model.Text.Trim(),
                    CreatedAt = DateTime.Now,
                    InReplyToId = model.InReplyToId,
                    Status = PublishStatus.Published,
                    ImageId = imageId,
                    HighlightX = model.HighlightX,
                    HighlightY = model.HighlightY,
                    HighlightWidth = model.HighlightWidth,
                    HighlightHeight = model.HighlightHeight,
                };
                _context.PDFPageComments.Add(comment);
                await _context.SaveChangesAsync();

                // notify the parent comment's author, never yourself when replying to your
                // own comment. Wrapped in its own try/catch, deliberately separate from the
                // method's outer one: the comment above is already saved by this point, so a
                // notification failure here must never make this method report failure back
                // to the client for a submission that actually succeeded - that could lead a
                // client to retry and create a duplicate comment. Same "swallow it, the
                // submission already succeeded" reasoning as SubmitPDFBookReportAsync's own
                // moderator-notification step.
                if (model.InReplyToId != null && parent.UserId != userId)
                {
                    try
                    {
                        var book = await _context.PDFBooks.AsNoTracking()
                            .Where(b => b.Id == page.PDFBookId)
                            .Select(b => new { b.Title })
                            .SingleOrDefaultAsync();

                        await _notificationService.PushNotification
                                        (
                                            parent.UserId,
                                            "پاسخ به دیدگاه شما",
                                            $"کسی به دیدگاه شما در صفحهٔ {page.PageNumber} کتاب «{book?.Title}» پاسخ داد:{Environment.NewLine}{comment.Text}",
                                            NotificationType.ActionRequired
                                        );
                    }
                    catch
                    {
                        // if not, do nothing - see doc comment above
                    }
                }

                return new RServiceResult<Guid>(comment.Id);
            }
            catch (Exception exp)
            {
                return new RServiceResult<Guid>(Guid.Empty, exp.ToString());
            }
        }

        /// <summary>
        /// every published comment on a page, flat (not nested - see
        /// PDFPageCommentViewModel's own doc comment on why), oldest first so replies
        /// naturally read in order under whatever a client groups them by. [requestingUserId]
        /// is optional (anonymous visitors can read comments) and only affects MyComment.
        ///
        /// Keyed by (pdfBookId, pageNumber), not the internal PDFPage.Id - same reasoning as
        /// SubmitPDFPageCommentAsync's own doc comment.
        /// </summary>
        public async Task<RServiceResult<PDFPageCommentViewModel[]>> GetPDFPageCommentsAsync(int pdfBookId, int pageNumber, Guid? requestingUserId)
        {
            try
            {
                // Image is loaded (Include) but ImageUrl is built afterward, in memory, not
                // inside the Select below - _BuildRImageUrl is a plain C# method, and EF Core
                // cannot translate an arbitrary method call into SQL as part of a query
                // projection. Same underlying mistake class as this project's earlier
                // EF.Property<T>-outside-a-live-query bugs (see PDFLibraryService-AuthorMerge.cs
                // and PDFLibraryService.cs's own doc comments on those) - different shape, same
                // root cause: something that only works outside a translated query, called
                // inside one.
                var comments = await _context.PDFPageComments.AsNoTracking()
                    .Include(c => c.User)
                    .Include(c => c.Image)
                    .Include(c => c.PDFPage)
                    .Where(c => c.PDFPage.PDFBookId == pdfBookId && c.PDFPage.PageNumber == pageNumber && c.Status == PublishStatus.Published)
                    .OrderBy(c => c.CreatedAt)
                    .ToArrayAsync();

                // every comment here is on the same book (the caller already told us which
                // one), so this is a single lookup, not a per-comment one
                var bookTitle = await _context.PDFBooks.AsNoTracking()
                    .Where(b => b.Id == pdfBookId)
                    .Select(b => b.Title)
                    .SingleOrDefaultAsync();

                var result = comments.Select(c => new PDFPageCommentViewModel()
                {
                    Id = c.Id,
                    PDFPageId = c.PDFPageId,
                    PageNumber = c.PDFPage.PageNumber,
                    PDFBookId = pdfBookId,
                    BookTitle = bookTitle,
                    UserId = c.UserId,
                    UserName = c.User.NickName,
                    Text = c.Text,
                    CreatedAt = c.CreatedAt,
                    EditedAt = c.EditedAt,
                    InReplyToId = c.InReplyToId,
                    MyComment = requestingUserId != null && c.UserId == requestingUserId.Value,
                    ImageUrl = c.Image == null ? null : _BuildRImageUrl(c.Image),
                    HighlightX = c.HighlightX,
                    HighlightY = c.HighlightY,
                    HighlightWidth = c.HighlightWidth,
                    HighlightHeight = c.HighlightHeight,
                }).ToArray();

                return new RServiceResult<PDFPageCommentViewModel[]>(result);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFPageCommentViewModel[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// count of published comments on a single page - deliberately keyed by
        /// (pdfBookId, pageNumber) rather than the resolved PDFPageId, since a client walking
        /// through pages (to show a comment-count badge as the reader turns pages) always
        /// already has book id + page number in hand and would otherwise need an extra
        /// round-trip just to resolve the PDFPageId first before it could even ask for a count.
        /// Meant to be cheap and safe to call on every page turn - no Include, no projection
        /// beyond a plain count.
        /// </summary>
        public async Task<RServiceResult<int>> GetPDFPageCommentCountAsync(int pdfBookId, int pageNumber)
        {
            try
            {
                var count = await _context.PDFPageComments.AsNoTracking()
                    .Where(c => c.PDFPage.PDFBookId == pdfBookId
                             && c.PDFPage.PageNumber == pageNumber
                             && c.Status == PublishStatus.Published)
                    .CountAsync();

                return new RServiceResult<int>(count);
            }
            catch (Exception exp)
            {
                return new RServiceResult<int>(0, exp.ToString());
            }
        }

        /// <summary>
        /// every published comment, newest first, paginated - one query serving three views.
        /// [pdfBookId] null → the site-wide comment hub / a specific book's own hub.
        /// [filterUserId] null → any author; a real value → only that user's own comments
        /// (the "my comments" view). Matches the sibling Ganjoor project's own
        /// GetRecentComments, which reuses one query the same three ways (a public feed, a
        /// per-poem-ish view, a "my comments" view) rather than near-duplicate methods.
        /// [requestingUserId] is independent of [filterUserId] - it only controls MyComment on
        /// the returned items (so a viewer's own comments show as editable/deletable even in
        /// the site-wide or per-book views, not just the "my comments" one); for the "my
        /// comments" view itself, callers pass the same id for both.
        ///
        /// Book titles are batch-fetched for the distinct PDFBookIds actually present in this
        /// page of results (one extra query, not one per comment) - for the per-book hub
        /// that's always exactly one id, but the site-wide hub can span many books in a single
        /// page of results, so this can't assume a single lookup the way
        /// GetPDFPageCommentsAsync does.
        /// </summary>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFPageCommentViewModel[] Items)>> GetRecentPDFPageCommentsAsync(int? pdfBookId, Guid? filterUserId, Guid? requestingUserId, PagingParameterModel paging)
        {
            try
            {
                var source = _context.PDFPageComments.AsNoTracking()
                    .Include(c => c.User)
                    .Include(c => c.Image)
                    .Include(c => c.PDFPage)
                    .Where(c => (pdfBookId == null || c.PDFPage.PDFBookId == pdfBookId.Value)
                             && (filterUserId == null || c.UserId == filterUserId.Value)
                             && c.Status == PublishStatus.Published)
                    .OrderByDescending(c => c.CreatedAt);

                (PaginationMetadata PagingMeta, PDFPageComment[] Items) paginatedResult =
                    await QueryablePaginator<PDFPageComment>.Paginate(source, paging);

                var bookIds = paginatedResult.Items.Select(c => c.PDFPage.PDFBookId).Distinct().ToArray();
                var bookTitles = await _context.PDFBooks.AsNoTracking()
                    .Where(b => bookIds.Contains(b.Id))
                    .Select(b => new { b.Id, b.Title })
                    .ToDictionaryAsync(b => b.Id, b => b.Title);

                var items = paginatedResult.Items.Select(c => new PDFPageCommentViewModel()
                {
                    Id = c.Id,
                    PDFPageId = c.PDFPageId,
                    PageNumber = c.PDFPage.PageNumber,
                    PDFBookId = c.PDFPage.PDFBookId,
                    BookTitle = bookTitles.TryGetValue(c.PDFPage.PDFBookId, out var title) ? title : null,
                    UserId = c.UserId,
                    UserName = c.User.NickName,
                    Text = c.Text,
                    CreatedAt = c.CreatedAt,
                    EditedAt = c.EditedAt,
                    InReplyToId = c.InReplyToId,
                    MyComment = requestingUserId != null && c.UserId == requestingUserId.Value,
                    ImageUrl = c.Image == null ? null : _BuildRImageUrl(c.Image),
                    HighlightX = c.HighlightX,
                    HighlightY = c.HighlightY,
                    HighlightWidth = c.HighlightWidth,
                    HighlightHeight = c.HighlightHeight,
                }).ToArray();

                return new RServiceResult<(PaginationMetadata, PDFPageCommentViewModel[])>((paginatedResult.PagingMeta, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata, PDFPageCommentViewModel[])>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// relative (not absolute) path to a stored image via the generic
        /// api/rimages/{id}.{ext} route (RImageControllerBase.GetImageWithCustomExtension) -
        /// relative rather than baking this server's own domain in, matching how every client
        /// already combines a known API root constant with a relative path for every other
        /// endpoint, rather than the server assuming one particular domain/reverse-proxy setup.
        /// This exact route wasn't already used elsewhere in this project to copy a URL-building
        /// convention from - confirmed the route's parameter shape from the RSecurityBackend
        /// package's own XML docs, but the shape here is otherwise inferred, not copied from a
        /// working example. Worth a quick check against this server's own Swagger UI.
        /// </summary>
        private static string _BuildRImageUrl(RImage image)
        {
            string ext = "jpg";
            if (!string.IsNullOrEmpty(image.ContentType))
            {
                if (image.ContentType.Contains("png")) ext = "png";
                else if (image.ContentType.Contains("gif")) ext = "gif";
                else if (image.ContentType.Contains("webp")) ext = "webp";
            }
            return $"api/rimages/{image.Id}.{ext}";
        }

        /// <summary>
        /// delete a comment - its own author always can, regardless of permissions; anyone
        /// else needs pdfcomment:moderate. This dual-path check is why the controller action
        /// for this is just [Authorize] (logged in) rather than a policy-gated
        /// [Authorize(Policy=...)] - a policy attribute can't conditionally allow "or it's your
        /// own", since ownership isn't known until the specific comment is loaded.
        ///
        /// This is a SOFT delete (Status set to Deleted, row kept), not a hard one - the
        /// generated migration confirmed why a hard delete isn't safe here: the InReplyToId
        /// self-reference has no cascade/set-null behavior at the database level at all (no
        /// FK path was even generated for it beyond plain NO ACTION), because SQL Server
        /// disallows cascading on this kind of self-referencing relationship in the first
        /// place. Deleting a comment that has any replies would throw a FK violation outright.
        /// Soft-deleting sidesteps that entirely (the row stays, so nothing referencing it via
        /// InReplyToId ever becomes invalid) and is arguably better UX anyway - a reply stays
        /// anchored to its real parent instead of becoming an orphaned top-level comment.
        /// GetPDFPageCommentsAsync already filters to Status == Published, so a soft-deleted
        /// comment simply stops appearing.
        /// </summary>
        public async Task<RServiceResult<bool>> DeletePDFPageCommentAsync(Guid requestingUserId, Guid commentId)
        {
            try
            {
                var comment = await _context.PDFPageComments.Where(c => c.Id == commentId).SingleOrDefaultAsync();
                if (comment == null)
                {
                    return new RServiceResult<bool>(false, $"comment {commentId} not found");
                }

                if (comment.UserId != requestingUserId)
                {
                    var permission = await _appUserService.HasPermission(requestingUserId, RMuseumSecurableItem.PDFPageCommentEntityShortName, RMuseumSecurableItem.ModerateOperationShortName);
                    if (!string.IsNullOrEmpty(permission.ExceptionString) || !permission.Result)
                    {
                        return new RServiceResult<bool>(false, "شما اجازهٔ حذف این دیدگاه را ندارید");
                    }
                }

                comment.Status = PublishStatus.Deleted;
                _context.Update(comment);
                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// edit a comment's text - its own author only, deliberately no moderator override
        /// (unlike delete, which pdfcomment:moderate can also do to anyone's comment).
        /// Rewriting someone else's words is a different, more invasive action than removing
        /// them - a moderator who finds a comment objectionable can already delete it; letting
        /// them silently change what it says risks misrepresenting what the person actually
        /// wrote, so this stays strictly author-only regardless of permissions.
        ///
        /// Sets EditedAt so readers (and the comment's own author, on their next view) can see
        /// the text changed since it was first posted - especially relevant once a reply
        /// exists, since the reply's own wording may no longer make sense against the edited
        /// version, and a silent edit here would leave no way to notice that happened.
        /// </summary>
        public async Task<RServiceResult<bool>> EditPDFPageCommentAsync(Guid requestingUserId, Guid commentId, string newText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newText))
                {
                    return new RServiceResult<bool>(false, "متن دیدگاه نمی‌تواند خالی باشد");
                }

                var comment = await _context.PDFPageComments.Where(c => c.Id == commentId).SingleOrDefaultAsync();
                if (comment == null)
                {
                    return new RServiceResult<bool>(false, $"comment {commentId} not found");
                }
                if (comment.Status != PublishStatus.Published)
                {
                    return new RServiceResult<bool>(false, "این دیدگاه دیگر در دسترس نیست");
                }
                if (comment.UserId != requestingUserId)
                {
                    return new RServiceResult<bool>(false, "فقط نویسندهٔ دیدگاه می‌تواند آن را ویرایش کند");
                }

                comment.Text = newText.Trim();
                comment.EditedAt = DateTime.Now;
                _context.Update(comment);
                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }
    }
}
