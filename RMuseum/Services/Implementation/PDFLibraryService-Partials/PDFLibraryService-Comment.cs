using Microsoft.EntityFrameworkCore;
using RMuseum.Models.Artifact;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Models.Notification;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// submit a page comment, or a reply to one - Phase 1: plain text only, no
        /// highlight/image yet (see PDFPageComment's own doc comment). Any authenticated user.
        /// </summary>
        public async Task<RServiceResult<Guid>> SubmitPDFPageCommentAsync(Guid userId, int pdfPageId, PDFPageCommentPostViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Text))
                {
                    return new RServiceResult<Guid>(Guid.Empty, "متن دیدگاه نمی‌تواند خالی باشد");
                }

                var page = await _context.PDFPages.AsNoTracking().Where(p => p.Id == pdfPageId).SingleOrDefaultAsync();
                if (page == null)
                {
                    return new RServiceResult<Guid>(Guid.Empty, $"PDFPage {pdfPageId} not found");
                }

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

                var comment = new PDFPageComment()
                {
                    Id = Guid.NewGuid(),
                    PDFPageId = pdfPageId,
                    UserId = userId,
                    Text = model.Text.Trim(),
                    CreatedAt = DateTime.Now,
                    InReplyToId = model.InReplyToId,
                    Status = PublishStatus.Published,
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
        /// </summary>
        public async Task<RServiceResult<PDFPageCommentViewModel[]>> GetPDFPageCommentsAsync(int pdfPageId, Guid? requestingUserId)
        {
            try
            {
                var comments = await _context.PDFPageComments.AsNoTracking()
                    .Include(c => c.User)
                    .Where(c => c.PDFPageId == pdfPageId && c.Status == PublishStatus.Published)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new PDFPageCommentViewModel()
                    {
                        Id = c.Id,
                        PDFPageId = c.PDFPageId,
                        UserId = c.UserId,
                        UserName = c.User.NickName,
                        Text = c.Text,
                        CreatedAt = c.CreatedAt,
                        InReplyToId = c.InReplyToId,
                        MyComment = requestingUserId != null && c.UserId == requestingUserId.Value,
                    })
                    .ToArrayAsync();

                return new RServiceResult<PDFPageCommentViewModel[]>(comments);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFPageCommentViewModel[]>(null, exp.ToString());
            }
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
    }
}
