using Microsoft.EntityFrameworkCore;
using RMuseum.Models.Artifact;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// recomputes PDFBook.AverageRating/RatingCount from scratch over every published
        /// review with a non-null Rating for this book - called after any submit/edit/delete
        /// that could change the aggregate. A fresh recompute rather than incremental math
        /// (subtract old rating, add new one) - simpler and safer against drift, and ratings
        /// change infrequently enough that the extra query per edit is not a real cost.
        /// </summary>
        private async Task _RecomputeBookRatingAsync(int bookId)
        {
            var ratings = await _context.PDFBookReviews.AsNoTracking()
                .Where(r => r.PDFBookId == bookId && r.Status == PublishStatus.Published && r.Rating != null)
                .Select(r => r.Rating.Value)
                .ToArrayAsync();

            var book = await _context.PDFBooks.Where(b => b.Id == bookId).SingleOrDefaultAsync();
            if (book == null) return;

            book.RatingCount = ratings.Length;
            book.AverageRating = ratings.Length == 0 ? (double?)null : ratings.Average();
            _context.Update(book);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// submit a new review - rejected if the caller already has one for this book (one
        /// review per (book, user), also enforced at the DB level by a unique index - see
        /// PDFBookReview's own doc comment); editing an existing review is
        /// EditPDFBookReviewAsync, a separate action, not an upsert here.
        /// </summary>
        public async Task<RServiceResult<Guid>> SubmitPDFBookReviewAsync(Guid userId, int bookId, PDFBookReviewSubmitViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Text))
                {
                    return new RServiceResult<Guid>(Guid.Empty, "متن نقد نمی‌تواند خالی باشد");
                }
                if (model.Rating != null && (model.Rating < 1 || model.Rating > 5))
                {
                    return new RServiceResult<Guid>(Guid.Empty, "امتیاز باید بین ۱ تا ۵ باشد");
                }

                var book = await _context.PDFBooks.AsNoTracking().Where(b => b.Id == bookId).SingleOrDefaultAsync();
                if (book == null || book.Status != PublishStatus.Published)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "این کتاب یافت نشد یا در دسترس نیست");
                }

                var alreadyReviewed = await _context.PDFBookReviews.AsNoTracking()
                    .AnyAsync(r => r.PDFBookId == bookId && r.UserId == userId && r.Status == PublishStatus.Published);
                if (alreadyReviewed)
                {
                    return new RServiceResult<Guid>(Guid.Empty, "شما پیش‌تر این کتاب را نقد کرده‌اید؛ می‌توانید نقد خود را ویرایش کنید");
                }

                var review = new PDFBookReview()
                {
                    Id = Guid.NewGuid(),
                    PDFBookId = bookId,
                    UserId = userId,
                    Text = model.Text.Trim(),
                    Rating = model.Rating,
                    Status = PublishStatus.Published,
                    CreatedAt = DateTime.Now,
                    LikeCount = 0,
                    DislikeCount = 0,
                };
                _context.PDFBookReviews.Add(review);
                await _context.SaveChangesAsync();

                if (model.Rating != null)
                {
                    await _RecomputeBookRatingAsync(bookId);
                }

                return new RServiceResult<Guid>(review.Id);
            }
            catch (Exception exp)
            {
                return new RServiceResult<Guid>(Guid.Empty, exp.ToString());
            }
        }

        /// <summary>
        /// edit an existing review's text and/or rating - own author only, no moderator
        /// override, same reasoning as EditPDFPageCommentAsync
        /// </summary>
        public async Task<RServiceResult<bool>> EditPDFBookReviewAsync(Guid userId, Guid reviewId, PDFBookReviewEditViewModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Text))
                {
                    return new RServiceResult<bool>(false, "متن نقد نمی‌تواند خالی باشد");
                }
                if (model.Rating != null && (model.Rating < 1 || model.Rating > 5))
                {
                    return new RServiceResult<bool>(false, "امتیاز باید بین ۱ تا ۵ باشد");
                }

                var review = await _context.PDFBookReviews.Where(r => r.Id == reviewId).SingleOrDefaultAsync();
                if (review == null)
                {
                    return new RServiceResult<bool>(false, $"review {reviewId} not found");
                }
                if (review.Status != PublishStatus.Published)
                {
                    return new RServiceResult<bool>(false, "این نقد دیگر در دسترس نیست");
                }
                if (review.UserId != userId)
                {
                    return new RServiceResult<bool>(false, "فقط نویسندهٔ نقد می‌تواند آن را ویرایش کند");
                }

                var ratingChanged = review.Rating != model.Rating;
                var bookId = review.PDFBookId;

                review.Text = model.Text.Trim();
                review.Rating = model.Rating;
                review.EditedAt = DateTime.Now;
                _context.Update(review);
                await _context.SaveChangesAsync();

                if (ratingChanged)
                {
                    await _RecomputeBookRatingAsync(bookId);
                }

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// delete a review - its own author always can; deleting someone else's needs
        /// pdfbookreview:moderate, checked here inside the service (not via a controller-level
        /// policy attribute or a canModerate parameter passed in) - same pattern as
        /// DeletePDFPageCommentAsync, since a policy attribute alone can't express "or it's
        /// your own", and keeping the check here (not split between controller and service)
        /// means there is exactly one place this rule can be gotten wrong, not two.
        /// </summary>
        public async Task<RServiceResult<bool>> DeletePDFBookReviewAsync(Guid requestingUserId, Guid reviewId)
        {
            try
            {
                var review = await _context.PDFBookReviews.Where(r => r.Id == reviewId).SingleOrDefaultAsync();
                if (review == null)
                {
                    return new RServiceResult<bool>(false, $"review {reviewId} not found");
                }

                if (review.UserId != requestingUserId)
                {
                    var permission = await _appUserService.HasPermission(requestingUserId, RMuseumSecurableItem.PDFBookReviewEntityShortName, RMuseumSecurableItem.ModerateOperationShortName);
                    if (!string.IsNullOrEmpty(permission.ExceptionString) || !permission.Result)
                    {
                        return new RServiceResult<bool>(false, "شما اجازهٔ حذف این نقد را ندارید");
                    }
                }

                var bookId = review.PDFBookId;
                var hadRating = review.Rating != null;

                review.Status = PublishStatus.Deleted;
                _context.Update(review);
                await _context.SaveChangesAsync();

                if (hadRating)
                {
                    await _RecomputeBookRatingAsync(bookId);
                }

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// paginated reviews for a single book, sortable - MyVote/MyReview are populated from
        /// requestingUserId if given (null for an anonymous caller, matching
        /// GetPDFPageCommentsAsync's own optional-auth reasoning: the route this backs is
        /// public, but a logged-in caller's own review/votes should still come back correctly
        /// shaped).
        /// </summary>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBookReviewViewModel[] Items)>> GetPDFBookReviewsAsync(int bookId, Guid? requestingUserId, PDFBookReviewSortMode sortMode, PagingParameterModel paging)
        {
            try
            {
                var source = _context.PDFBookReviews.AsNoTracking()
                    .Include(r => r.User)
                    .Where(r => r.PDFBookId == bookId && r.Status == PublishStatus.Published);

                source = sortMode switch
                {
                    PDFBookReviewSortMode.HighestRated => source
                        .OrderByDescending(r => r.Rating ?? -1)
                        .ThenByDescending(r => r.CreatedAt),
                    PDFBookReviewSortMode.MostLiked => source
                        .OrderByDescending(r => r.LikeCount - r.DislikeCount)
                        .ThenByDescending(r => r.CreatedAt),
                    _ => source.OrderByDescending(r => r.CreatedAt),
                };

                (PaginationMetadata PagingMeta, PDFBookReview[] Items) paginatedResult =
                    await QueryablePaginator<PDFBookReview>.Paginate(source, paging);

                var reviewIds = paginatedResult.Items.Select(r => r.Id).ToArray();
                var myVotes = requestingUserId == null
                    ? new Dictionary<Guid, bool>()
                    : await _context.PDFBookReviewVotes.AsNoTracking()
                        .Where(v => reviewIds.Contains(v.PDFBookReviewId) && v.UserId == requestingUserId.Value)
                        .ToDictionaryAsync(v => v.PDFBookReviewId, v => v.IsLike);

                var items = paginatedResult.Items.Select(r => new PDFBookReviewViewModel()
                {
                    Id = r.Id,
                    PDFBookId = r.PDFBookId,
                    UserId = r.UserId,
                    UserName = r.User.NickName,
                    Text = r.Text,
                    Rating = r.Rating,
                    CreatedAt = r.CreatedAt,
                    EditedAt = r.EditedAt,
                    MyReview = requestingUserId != null && r.UserId == requestingUserId.Value,
                    LikeCount = r.LikeCount,
                    DislikeCount = r.DislikeCount,
                    MyVote = myVotes.TryGetValue(r.Id, out var isLike) ? isLike : (bool?)null,
                }).ToArray();

                return new RServiceResult<(PaginationMetadata, PDFBookReviewViewModel[])>((paginatedResult.PagingMeta, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata, PDFBookReviewViewModel[])>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// paginated reviews across every book, newest first - the site-wide "latest reviews"
        /// hub. [filterUserId] null → any author; a real value → only that user's own reviews
        /// (a future "my reviews" view, matching GetRecentPDFPageCommentsAsync's own
        /// three-way-reuse precedent - included from the start here since it costs nothing
        /// extra now and avoids a signature change later). [requestingUserId] is independent
        /// of [filterUserId] - it only controls MyReview/MyVote on the results.
        /// </summary>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBookReviewViewModel[] Items)>> GetRecentPDFBookReviewsAsync(Guid? filterUserId, Guid? requestingUserId, PagingParameterModel paging)
        {
            try
            {
                var source = _context.PDFBookReviews.AsNoTracking()
                    .Include(r => r.User)
                    .Include(r => r.PDFBook)
                    .Where(r => (filterUserId == null || r.UserId == filterUserId.Value) && r.Status == PublishStatus.Published)
                    .OrderByDescending(r => r.CreatedAt);

                (PaginationMetadata PagingMeta, PDFBookReview[] Items) paginatedResult =
                    await QueryablePaginator<PDFBookReview>.Paginate(source, paging);

                var reviewIds = paginatedResult.Items.Select(r => r.Id).ToArray();
                var myVotes = requestingUserId == null
                    ? new Dictionary<Guid, bool>()
                    : await _context.PDFBookReviewVotes.AsNoTracking()
                        .Where(v => reviewIds.Contains(v.PDFBookReviewId) && v.UserId == requestingUserId.Value)
                        .ToDictionaryAsync(v => v.PDFBookReviewId, v => v.IsLike);

                var items = paginatedResult.Items.Select(r => new PDFBookReviewViewModel()
                {
                    Id = r.Id,
                    PDFBookId = r.PDFBookId,
                    BookTitle = r.PDFBook.Title,
                    UserId = r.UserId,
                    UserName = r.User.NickName,
                    Text = r.Text,
                    Rating = r.Rating,
                    CreatedAt = r.CreatedAt,
                    EditedAt = r.EditedAt,
                    MyReview = requestingUserId != null && r.UserId == requestingUserId.Value,
                    LikeCount = r.LikeCount,
                    DislikeCount = r.DislikeCount,
                    MyVote = myVotes.TryGetValue(r.Id, out var isLike) ? isLike : (bool?)null,
                }).ToArray();

                return new RServiceResult<(PaginationMetadata, PDFBookReviewViewModel[])>((paginatedResult.PagingMeta, items));
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata, PDFBookReviewViewModel[])>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// cast or change a vote on someone else's review - a user can't vote on their own
        /// review, same principle as not being able to report your own comment (trivially
        /// inflating your own score otherwise). Upserts: if this user already voted on this
        /// review, the existing PDFBookReviewVote row is updated (like to dislike or vice
        /// versa) rather than a second row being created - see PDFBookReviewVote's own doc
        /// comment. LikeCount/DislikeCount on the review are kept in sync here rather than
        /// recomputed by a separate pass.
        /// </summary>
        public async Task<RServiceResult<bool>> CastPDFBookReviewVoteAsync(Guid userId, Guid reviewId, bool isLike)
        {
            try
            {
                var review = await _context.PDFBookReviews.Where(r => r.Id == reviewId).SingleOrDefaultAsync();
                if (review == null || review.Status != PublishStatus.Published)
                {
                    return new RServiceResult<bool>(false, "این نقد یافت نشد یا در دسترس نیست");
                }
                if (review.UserId == userId)
                {
                    return new RServiceResult<bool>(false, "امکان رأی دادن به نقد خودتان وجود ندارد");
                }

                var existingVote = await _context.PDFBookReviewVotes
                    .Where(v => v.PDFBookReviewId == reviewId && v.UserId == userId)
                    .SingleOrDefaultAsync();

                if (existingVote == null)
                {
                    _context.PDFBookReviewVotes.Add(new PDFBookReviewVote()
                    {
                        Id = Guid.NewGuid(),
                        PDFBookReviewId = reviewId,
                        UserId = userId,
                        IsLike = isLike,
                        CreatedAt = DateTime.Now,
                    });
                    if (isLike) review.LikeCount++; else review.DislikeCount++;
                }
                else if (existingVote.IsLike != isLike)
                {
                    if (existingVote.IsLike) review.LikeCount--; else review.DislikeCount--;
                    if (isLike) review.LikeCount++; else review.DislikeCount++;
                    existingVote.IsLike = isLike;
                    existingVote.CreatedAt = DateTime.Now;
                    _context.Update(existingVote);
                }
                // else: identical vote resubmitted - nothing to change

                _context.Update(review);
                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// remove the caller's own vote from a review, if they had one - a no-op (not an
        /// error) if they hadn't voted on it at all
        /// </summary>
        public async Task<RServiceResult<bool>> RemovePDFBookReviewVoteAsync(Guid userId, Guid reviewId)
        {
            try
            {
                var existingVote = await _context.PDFBookReviewVotes
                    .Where(v => v.PDFBookReviewId == reviewId && v.UserId == userId)
                    .SingleOrDefaultAsync();
                if (existingVote == null)
                {
                    return new RServiceResult<bool>(true);
                }

                var review = await _context.PDFBookReviews.Where(r => r.Id == reviewId).SingleOrDefaultAsync();
                if (review != null)
                {
                    if (existingVote.IsLike) review.LikeCount--; else review.DislikeCount--;
                    _context.Update(review);
                }

                _context.PDFBookReviewVotes.Remove(existingVote);
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
