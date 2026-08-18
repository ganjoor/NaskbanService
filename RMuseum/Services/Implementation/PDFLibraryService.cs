using DNTPersianUtils.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RMuseum.DbContext;
using RMuseum.Models.Artifact;
using RMuseum.Models.Artifact.ViewModels;
using RMuseum.Models.GanjoorIntegration.ViewModels;
using RMuseum.Models.GanjoorIntegration;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RSecurityBackend.Models.Auth.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Models.Image;
using RSecurityBackend.Services;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentFTP;
using RMuseum.Models.PDFUserTracking;
using RMuseum.Models.PDFUserTracking.ViewModels;
using RMuseum.Models.ImportJob;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RMuseum.Models.Ganjoor.ViewModels;
using RMuseum.Models.Ganjoor;
using System.Net.Http;
using System.Net;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// PDF Library Services
    /// </summary>
    public partial class PDFLibraryService : IPDFLibraryService
    {

        /// <summary>
        /// check known source before download (0 means no problem, -1 problem, greater than zero id for downloaed pdf)
        /// </summary>
        /// <param name="srcUrl"></param>
        /// <returns></returns>
        public async Task<RServiceResult<int>> CheckKnownSourceAsync(string srcUrl)
        {
            try
            {
                var downloaded = await _context.PDFBooks.AsNoTracking().Where(a => a.OriginalSourceUrl == srcUrl).FirstOrDefaultAsync();
                if (
                    downloaded
                    !=
                    null
                    )
                {
                    return new RServiceResult<int>(downloaded.Id, $"duplicated srcUrl '{srcUrl}'");
                }
                if (
                    (
                    await _context.ImportJobs
                        .Where(j => j.JobType == JobType.Pdf && j.SrcContent == ("scrapping ..." + srcUrl) && !(j.Status == ImportJobStatus.Failed || j.Status == ImportJobStatus.Aborted))
                        .SingleOrDefaultAsync()
                    )
                    !=
                    null
                    )
                {
                    return new RServiceResult<int>(-1, $"Job is already scheduled or running for importing source url: {srcUrl}");
                }

                return new RServiceResult<int>(0);

            }
            catch (Exception exp)
            {
                return new RServiceResult<int>(0, exp.ToString());
            }
        }

        /// <summary>
        /// import from uknown sources
        /// </summary>
        /// <param name="srcUrl"></param>
        /// <returns></returns>
        public async Task<RServiceResult<int>> StartImportingKnownSourceAsync(string srcUrl)
        {
            var res = await CheckKnownSourceAsync(srcUrl);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return res;
            }
            _backgroundTaskQueue.QueueBackgroundWorkItem
                      (
                          async token =>
                          {
                              using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                              {
                                  await _ImportfFromKnownSourceAsync(_context, srcUrl);
                              }
                          }
                      );
            return res;
        }
        /// <summary>
        /// import from known sources
        /// </summary>
        /// <param name="context"></param>
        /// <param name="srcUrl"></param>
        /// <returns></returns>
        private async Task<RServiceResult<int>> _ImportfFromKnownSourceAsync(RMuseumDbContext context, string srcUrl)
        {
            if (srcUrl.Contains("https://sohalibrary.com"))
            {
                return await StartImportingSohaLibraryUrlAsync(context, srcUrl);
            }
            else
            {
                return await StartImportingELiteratureBookUrlAsync(context, srcUrl);
            }
        }

        /// <summary>
        /// import from known source
        /// </summary>
        /// <param name="srcUrl"></param>
        /// <param name="finalizeDownload"></param>
        /// <returns></returns>
        public async Task<RServiceResult<int>> ImportfFromKnownSourceAsync(string srcUrl, bool finalizeDownload)
        {
            if (srcUrl.Contains("https://sohalibrary.com"))
            {
                return await ImportSohaLibraryUrlAsync(srcUrl, _context, finalizeDownload);
            }
            else
            {
                return await ImportELiteratureBookLibraryUrlAsync(srcUrl, _context, finalizeDownload);
            }
        }

        /// <summary>
        /// get pdf book by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="statusArray"></param>
        /// <param name="includePages"></param>
        /// <param name="includeBookText"></param>
        /// <param name="includePageText"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFBook>> GetPDFBookByIdAsync(int id, PublishStatus[] statusArray, bool includePages, bool includeBookText, bool includePageText)
        {
            try
            {
                // if this id was merged away as a duplicate, transparently serve the survivor's
                // data instead of a 404. Both GetPDFBookByIdAsync and GetUserVisiblePDFBookAsync
                // (which calls this) are covered; other id-based lookups (search, TOC, admin/tag
                // tools) query PDFBooks directly and would need the same check added separately
                // if old ids need to resolve there too - see GetPDFBookTableOfContentsAsync for
                // the same pattern, already added since it's the other main public GET-by-id route
                var redirect = await _context.PDFBookRedirects.AsNoTracking().Where(r => r.OldPDFBookId == id).SingleOrDefaultAsync();
                if (redirect != null)
                {
                    id = redirect.SurvivorPDFBookId;
                }

                var pdfBook =
                    includePages ?
                    await _context.PDFBooks.AsNoTracking()
                            .Include(b => b.Book)
                            .Include(b => b.PDFFile)
                            .Include(b => b.MultiVolumePDFCollection)
                            .Include(b => b.PDFSource)
                            .Include(b => b.Contributers).ThenInclude(c => c.Author)
                            .Include(b => b.Tags).ThenInclude(t => t.RTag)
                            .Include(b => b.Pages)
                            .Where(b => statusArray.Contains(b.Status) && b.Id == id)
                            .SingleOrDefaultAsync()
                    :
                    await _context.PDFBooks.AsNoTracking()
                            .Include(b => b.Book)
                            .Include(b => b.PDFFile)
                            .Include(b => b.MultiVolumePDFCollection)
                            .Include(b => b.PDFSource)
                            .Include(b => b.Contributers).ThenInclude(c => c.Author)
                            .Include(b => b.Tags).ThenInclude(t => t.RTag)
                            .Where(b => statusArray.Contains(b.Status) && b.Id == id)
                            .SingleOrDefaultAsync();
                if (pdfBook != null)
                {
                    if (!includeBookText)
                    {
                        pdfBook.BookText = "";
                    }
                    if (!includePageText && pdfBook.Pages != null)
                    {
                        foreach (var page in pdfBook.Pages)
                        {
                            page.PageText = "";
                        }
                    }
                    if (pdfBook.Book != null)
                    {
                        pdfBook.Book.PDFBooks = await _context.PDFBooks.AsNoTracking()
                            .Include(a => a.CoverImage)
                            .Where(a => a.Status == PublishStatus.Published && a.BookId == id)
                           .OrderByDescending(t => t.Title).ToArrayAsync();
                    }
                    if (pdfBook.MultiVolumePDFCollection != null)
                    {
                        pdfBook.MultiVolumePDFCollection.PDFBooks = await _context.PDFBooks.AsNoTracking()
                            .Include(a => a.CoverImage)
                            .Where(a => a.Status == PublishStatus.Published && a.MultiVolumePDFCollectionId == id)
                           .OrderBy(t => t.VolumeOrder)
                           .ToArrayAsync();
                    }
                    List<RArtifactTagViewModel> rArtifactTags = new List<RArtifactTagViewModel>();
                    if (pdfBook.Tags != null)
                    {
                        foreach (RTagValue tag in pdfBook.Tags)
                        {
                            RArtifactTagViewModel related = rArtifactTags.Where(t => t.Id == tag.RTagId).SingleOrDefault();
                            List<RTagValue> values = (related == null) ? new List<RTagValue>() : new List<RTagValue>(related.Values);
                            if (related == null)
                            {
                                related =
                                    new RArtifactTagViewModel()
                                    {
                                        Id = tag.RTag.Id,
                                        Order = tag.RTag.Order,
                                        TagType = tag.RTag.TagType,
                                        FriendlyUrl = tag.RTag.FriendlyUrl,
                                        Status = tag.RTag.Status,
                                        Name = tag.RTag.Name,
                                        NameInEnglish = tag.RTag.NameInEnglish,
                                        GlobalValue = tag.RTag.GlobalValue,
                                        PluralName = tag.RTag.PluralName,
                                        PluralNameInEnglish = tag.RTag.PluralNameInEnglish
                                    };
                                rArtifactTags.Add(related);

                            }
                            values.Add(tag);
                            values.Sort((a, b) => a.Order - b.Order);
                            related.Values = values;
                        }

                        rArtifactTags.Sort((a, b) => a.Order - b.Order);
                    }
                    pdfBook.ArtifactTags = rArtifactTags;
                }
                return new RServiceResult<PDFBook>(pdfBook);

            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFBook>(null, exp.ToString());
            }
        }


        /// <summary>
        /// get page by page number
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFPage>> GetPDFPageAsync(int pdfBookId, int pageNumber)
        {
            try
            {
                var pdfBook = await _context.PDFBooks.AsNoTracking().Where(b => b.Id == pdfBookId).SingleAsync();
                if (pdfBook.Status != PublishStatus.Published)
                {
                    return null;
                }

                pdfBook.BookText = "";
                var pdfPage = await _context.PDFPages.AsNoTracking()
                            .Include(b => b.Tags).ThenInclude(t => t.RTag)
                            .Where(b => b.PDFBookId == pdfBookId && b.PageNumber == pageNumber)
                            .SingleOrDefaultAsync();
                if (pdfPage != null)
                {
                    pdfPage.PDFBook = pdfBook;

                    List<RArtifactTagViewModel> rArtifactTags = new List<RArtifactTagViewModel>();
                    if (pdfPage.Tags != null)
                    {
                        foreach (RTagValue tag in pdfPage.Tags)
                        {
                            RArtifactTagViewModel related = rArtifactTags.Where(t => t.Id == tag.RTagId).SingleOrDefault();
                            List<RTagValue> values = (related == null) ? new List<RTagValue>() : new List<RTagValue>(related.Values);
                            if (related == null)
                            {
                                related =
                                    new RArtifactTagViewModel()
                                    {
                                        Id = tag.RTag.Id,
                                        Order = tag.RTag.Order,
                                        TagType = tag.RTag.TagType,
                                        FriendlyUrl = tag.RTag.FriendlyUrl,
                                        Status = tag.RTag.Status,
                                        Name = tag.RTag.Name,
                                        NameInEnglish = tag.RTag.NameInEnglish,
                                        GlobalValue = tag.RTag.GlobalValue,
                                        PluralName = tag.RTag.PluralName,
                                        PluralNameInEnglish = tag.RTag.PluralNameInEnglish
                                    };
                                rArtifactTags.Add(related);

                            }
                            values.Add(tag);
                            values.Sort((a, b) => a.Order - b.Order);
                            related.Values = values;
                        }

                        rArtifactTags.Sort((a, b) => a.Order - b.Order);

                        var unrevisedText = await _context.PDFPageUnrevisedTexts.AsNoTracking().Where(t => t.PageId == pdfPage.Id).SingleOrDefaultAsync();
                        if (unrevisedText != null)
                        {
                            pdfPage.Tags.Add
                                (
                                new RTagValue()
                                {
                                    Id = Guid.Empty,
                                    Order = 1000,
                                    RTagId = Guid.Empty,
                                    Value = unrevisedText.PageText,
                                    RTag = new RTag()
                                    {
                                        Id = Guid.Empty,
                                        Order = 1000,
                                        TagType = RTagType.Ordinary,
                                        FriendlyUrl = "",
                                        Status = PublishStatus.Published,
                                        Name = "متن بازبینی نشده توسط هوش مصنوعی",
                                        NameInEnglish = "Unrevised Text",
                                        GlobalValue = false,
                                        PluralName = "",
                                        PluralNameInEnglish = "",
                                    }
                                }
                                );
                            rArtifactTags.Add(new RArtifactTagViewModel()
                            {
                                Id = Guid.Empty,
                                Order = 1000,
                                TagType = RTagType.Ordinary,
                                FriendlyUrl = "",
                                Status = PublishStatus.Published,
                                Name = "متن بازبینی نشده توسط هوش مصنوعی",
                                NameInEnglish = "Unrevised Text",
                                GlobalValue = false,
                                PluralName = "",
                                PluralNameInEnglish = "",
                                Values = [
                                    new RTagValue()
                                    {
                                        Id = Guid.Empty,
                                        Order = 1000,
                                        RTagId = Guid.Empty,
                                        Value = unrevisedText.PageText,
                                    }],
                            }
                            );
                        }
                    }
                    pdfPage.ArtifactTags = rArtifactTags;
                }
                return new RServiceResult<PDFPage>(pdfPage);

            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFPage>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get all pdfbooks (including CoverImage info but not pages or tagibutes info)
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="statusArray"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> GetAllPDFBooksAsync(PagingParameterModel paging, PublishStatus[] statusArray)
        {
            try
            {
                var source =
                _context.PDFBooks.AsNoTracking()
                .Include(a => a.CoverImage)
                .Where(a => statusArray.Contains(a.Status))
                .Select(b => new PDFBook()
                {
                    Id = b.Id,
                    BookId = b.BookId,
                    Status = b.Status,
                    Title = b.Title,
                    SubTitle = b.SubTitle,
                    AuthorsLine = b.AuthorsLine,
                    ISBN = b.ISBN,
                    Description = b.Description,
                    Language = b.Language,
                    IsTranslation = b.IsTranslation,
                    TranslatorsLine = b.TranslatorsLine,
                    TitleInOriginalLanguage = b.TitleInOriginalLanguage,
                    PublisherLine = b.PublisherLine,
                    PublishingDate = b.PublishingDate,
                    PublishingLocation = b.PublishingLocation,
                    PublishingNumber = b.PublishingNumber,
                    ClaimedPageCount = b.ClaimedPageCount,
                    MultiVolumePDFCollectionId = b.MultiVolumePDFCollectionId,
                    VolumeOrder = b.VolumeOrder,
                    DateTime = b.DateTime,
                    LastModified = b.LastModified,
                    ExternalPDFFileUrl = b.ExternalPDFFileUrl,
                    CoverImage = b.CoverImage,
                    CoverImageId = b.CoverImageId,
                    ExtenalCoverImageUrl = b.ExtenalCoverImageUrl,
                    OriginalSourceName = b.OriginalSourceName,
                    OriginalSourceUrl = b.OriginalSourceUrl,
                    OriginalFileUrl = b.OriginalFileUrl,
                    PageCount = b.PageCount,
                    FileMD5CheckSum = b.FileMD5CheckSum,
                    OriginalFileName = b.OriginalFileName,
                    StorageFolderName = b.StorageFolderName,
                    BookScriptType = b.BookScriptType,
                    PDFSourceId = b.PDFSourceId,
                    OCRed = b.OCRed,
                    OCRTime = b.OCRTime,
                    BookText = "",
                })
               .OrderByDescending(t => t.DateTime)
               .AsQueryable();
                (PaginationMetadata PagingMeta, PDFBook[] Books) paginatedResult =
                    await QueryablePaginator<PDFBook>.Paginate(source, paging);
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>((null, null), exp.ToString());
            }
        }


        /// <summary>
        /// edit pdf book master record
        /// </summary>
        /// <param name="model"></param>
        /// <param name="canChangeStatusToAwaiting"></param>
        /// <param name="canPublish"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFBook>> EditPDFBookMasterRecordAsync(PDFBook model, bool canChangeStatusToAwaiting, bool canPublish)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Title))
                {
                    return new RServiceResult<PDFBook>(null, "Name could not be empty.");
                }

                PDFBook pdfBook =
                     await _context.PDFBooks
                     .Include(b => b.Contributers).ThenInclude(c => c.Author)
                     .Where(a => a.Id == model.Id)
                    .SingleOrDefaultAsync();


                if (pdfBook != null)
                {
                    if (pdfBook.Status != model.Status)
                    {
                        if (!canChangeStatusToAwaiting)
                        {
                            return new RServiceResult<PDFBook>(null, "User should be able to change status to Awaiting to complete this operation.");
                        }

                        if (
                            !
                            (
                            (pdfBook.Status == PublishStatus.Draft && model.Status == PublishStatus.Awaiting)
                            ||
                            (pdfBook.Status == PublishStatus.Awaiting && model.Status == PublishStatus.Draft)
                            )
                            )
                        {
                            if (!canPublish)
                            {
                                return new RServiceResult<PDFBook>(null, "User should have Publish permission to complete this operation.");
                            }
                        }
                    }

                    // captured before the scalar copy below overwrites them - drives whether
                    // Contributers ("نویسنده"/"مترجم" rows) need to be resynced at all
                    bool authorsLineChanged = pdfBook.AuthorsLine != model.AuthorsLine;
                    bool translatorsLineChanged = pdfBook.TranslatorsLine != model.TranslatorsLine;

                    pdfBook.Status = model.Status;
                    pdfBook.Title = model.Title;
                    pdfBook.SubTitle = model.SubTitle;
                    pdfBook.AuthorsLine = model.AuthorsLine;
                    pdfBook.ISBN = model.ISBN;
                    pdfBook.Description = model.Description;
                    pdfBook.IsTranslation = model.IsTranslation;
                    pdfBook.TranslatorsLine = model.TranslatorsLine;
                    pdfBook.TitleInOriginalLanguage = model.TitleInOriginalLanguage;
                    pdfBook.PublisherLine = model.PublisherLine;
                    pdfBook.PublishingDate = model.PublishingDate;
                    pdfBook.PublishingLocation = model.PublishingLocation;
                    pdfBook.PublishingNumber = model.PublishingNumber == 0 ? null : model.PublishingNumber;
                    pdfBook.ClaimedPageCount = model.ClaimedPageCount == 0 ? null : model.ClaimedPageCount;
                    pdfBook.OriginalSourceName = model.OriginalSourceName;
                    pdfBook.OriginalFileUrl = model.OriginalFileUrl;
                    pdfBook.VolumeOrder = model.VolumeOrder;
                    pdfBook.MultiVolumePDFCollectionId = model.MultiVolumePDFCollectionId;
                    pdfBook.BookId = model.BookId;
                    pdfBook.PDFSourceId = model.PDFSourceId;
                    pdfBook.BookScriptType = model.BookScriptType;
                    pdfBook.LastModified = DateTime.Now;

                    // keep the structured Contributers (AuthorRole) rows behind AuthorsLine/
                    // TranslatorsLine in sync with the edited text - see
                    // _SyncContributorRoleAsync for what "in sync" means (existing links for
                    // names still on the line are left untouched, links for names dropped from
                    // the line are removed, links for new names are added).
                    List<int> orphanCandidateAuthorIds = new List<int>();

                    if (authorsLineChanged)
                    {
                        orphanCandidateAuthorIds.AddRange(
                            await _SyncContributorRoleAsync(_context, pdfBook, "نویسنده", pdfBook.AuthorsLine)
                            );
                    }

                    if (translatorsLineChanged)
                    {
                        orphanCandidateAuthorIds.AddRange(
                            await _SyncContributorRoleAsync(_context, pdfBook, "مترجم", pdfBook.TranslatorsLine)
                            );
                    }

                    _context.Update(pdfBook);
                    await _context.SaveChangesAsync();

                    // only worth checking Authors that actually lost a link just now, and only
                    // after the removal above is committed - see _RemoveOrphanedAuthorsAsync
                    if (orphanCandidateAuthorIds.Count > 0)
                    {
                        await _RemoveOrphanedAuthorsAsync(_context, orphanCandidateAuthorIds);
                    }
                }
                return new RServiceResult<PDFBook>(pdfBook);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFBook>(null, exp.ToString());
            }
        }

        /// <summary>
        /// splits a comma-separated line of author/translator names into individual Author
        /// records, reusing an existing row (exact Name match) or creating a new one - the same
        /// resolution rule ImportSohaLibraryUrlAsync applies when it scrapes نویسنده/مترجم
        /// values off the source page, extracted here so Edit can reuse it. The line is
        /// normalized with the same ToPersianNumbers()/ApplyCorrectYeKe() pipeline import
        /// applies before splitting, so a name typed by hand in the edit form still matches an
        /// importer-created Author row instead of spawning a near-duplicate. Newly created
        /// Authors are saved immediately (same convention the importer uses) so a name repeated
        /// twice on the same line resolves to one row, not two. Preserves the line's order;
        /// de-duplicates by Author.Id if the same name appears twice.
        /// </summary>
        private async Task<List<Author>> _ResolveAuthorsFromLineAsync(RMuseumDbContext context, string line)
        {
            List<Author> resolved = new List<Author>();
            if (string.IsNullOrWhiteSpace(line))
                return resolved;

            string normalizedLine = line.ToPersianNumbers().ApplyCorrectYeKe();

            foreach (string rawName in normalizedLine.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string name = rawName.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                Author author =
                    resolved.Where(a => a.Name == name).FirstOrDefault()
                    ??
                    await context.Authors.Where(a => a.Name == name).FirstOrDefaultAsync();

                if (author == null)
                {
                    author = new Author() { Name = name };
                    context.Authors.Add(author);
                    await context.SaveChangesAsync();
                }

                if (!resolved.Any(a => a.Id == author.Id))
                {
                    resolved.Add(author);
                }
            }

            return resolved;
        }

        /// <summary>
        /// keeps pdfBook.Contributers rows for a single Role ("نویسنده", "مترجم", ...) in sync
        /// with a free-text, comma-separated line (AuthorsLine/TranslatorsLine): a link whose
        /// Author is still named on the line is left exactly as it is (not removed/re-added, so
        /// no duplicate AuthorRole row and no needless Author lookup/create for it); a link
        /// whose Author is no longer on the line is removed; a name newly present on the line
        /// gets a new link, reusing/creating its Author via _ResolveAuthorsFromLineAsync. Only
        /// touches AuthorRole rows with the given Role - "مصحح" and any custom
        /// OtherContributerRole rows on the same book are left untouched, since Edit has no text
        /// field driving those. Does not call SaveChangesAsync - caller controls the save.
        /// Returns the AuthorIds that lost their link here, for the caller to check for
        /// orphaning once the removal is actually committed.
        /// </summary>
        private async Task<List<int>> _SyncContributorRoleAsync(RMuseumDbContext context, PDFBook pdfBook, string role, string line)
        {
            List<Author> desiredAuthors = await _ResolveAuthorsFromLineAsync(context, line);
            List<int> desiredIds = desiredAuthors.Select(a => a.Id).ToList();

            List<AuthorRole> existingRoles = pdfBook.Contributers.Where(c => c.Role == role).ToList();
            List<int> existingIds = existingRoles.Select(c => c.Author.Id).ToList();

            List<int> removedAuthorIds = new List<int>();
            foreach (AuthorRole existing in existingRoles)
            {
                if (!desiredIds.Contains(existing.Author.Id))
                {
                    removedAuthorIds.Add(existing.Author.Id);
                    pdfBook.Contributers.Remove(existing);
                    context.Remove(existing);
                }
            }

            foreach (Author author in desiredAuthors)
            {
                if (!existingIds.Contains(author.Id))
                {
                    pdfBook.Contributers.Add(new AuthorRole()
                    {
                        Author = author,
                        Role = role
                    });
                }
            }

            return removedAuthorIds;
        }

        /// <summary>
        /// deletes Author rows that no longer have any AuthorRole pointing at them, checked
        /// across the whole AuthorRole table - not just one book's Contributers - since the same
        /// Author can be credited on other PDFBooks, or on a plain Book via Book.Authors. Must
        /// only be called after the AuthorRole removal that made candidateAuthorIds candidates
        /// has actually been committed with SaveChangesAsync: AuthorRole.AuthorId has no
        /// cascade/restrict delete behavior, so a query run beforehand would still see the
        /// about-to-be-deleted row in the database and wrongly treat the Author as still in use.
        /// </summary>
        private async Task _RemoveOrphanedAuthorsAsync(RMuseumDbContext context, IEnumerable<int> candidateAuthorIds)
        {
            bool anyRemoved = false;
            foreach (int authorId in candidateAuthorIds.Distinct())
            {
                bool stillReferenced = await context.Set<AuthorRole>().AnyAsync(ar => EF.Property<int>(ar, "AuthorId") == authorId);
                if (stillReferenced)
                    continue;

                Author author = await context.Authors.Where(a => a.Id == authorId).SingleOrDefaultAsync();
                if (author != null)
                {
                    context.Authors.Remove(author);
                    anyRemoved = true;
                }
            }

            if (anyRemoved)
            {
                await context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Copy PDF Book Cover Image From Page Thumbnail image
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="pdfpageId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> SetPDFBookCoverImageFromPageAsync(int pdfBookId, int pdfpageId)
        {
            try
            {
                PDFBook pdfBook = await _context
                    .PDFBooks.Where(a => a.Id == pdfBookId)
                    .SingleOrDefaultAsync();
                if (pdfBook == null)
                    return new RServiceResult<bool>(false, "pdf book not found.");

                PDFPage pdfPage = await _context.PDFPages.AsNoTracking().Include(p => p.ThumbnailImage).Where(p => p.Id == pdfpageId).SingleOrDefaultAsync();

                if (pdfPage == null)
                    return new RServiceResult<bool>(false, "Page not found.");

                pdfBook.CoverImage = RImage.DuplicateExcludingId(pdfPage.ThumbnailImage);
                pdfBook.ExtenalCoverImageUrl = pdfPage.ExtenalThumbnailImageUrl;

                pdfBook.LastModified = DateTime.Now;

                _context.Update(pdfBook);
                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        private void FtpClient_ValidateCertificate(FluentFTP.Client.BaseClient.BaseFtpClient control, FtpSslValidationEventArgs e)
        {
            e.Accept = true;
        }

        /// <summary>
        /// remove a PDF book: cleans up every dependent record that could otherwise block the
        /// delete (author/contributor links, user bookmarks pointing at the book or any of its
        /// pages) or be left orphaned by it (AI/OCR queue entries, Ganjoor/Pinterest links, visit
        /// records, pending duplicate-candidate rows), then queues the slow, failure-prone part -
        /// deleting the actual file from the external FTP server and local disk - as a
        /// PendingPDFStorageCleanup row instead of doing it inline. This method itself never talks
        /// to FTP, so it can no longer time out or fail because of it; run
        /// StartCleaningUpPendingPDFStorageAsync to actually reclaim the storage, retryable
        /// indefinitely and independent of this call succeeding.
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> RemovePDFBookAsync(int pdfBookId)
        {
            try
            {
                PDFBook record = await _context.PDFBooks
                        .Include(a => a.Pages).ThenInclude(i => i.ThumbnailImage)
                        .Include(a => a.Pages).ThenInclude(i => i.Tags)
                        .Include(a => a.Contributers)
                        .Include(a => a.Tags)
                        .Where(a => a.Id == pdfBookId)
                        .SingleOrDefaultAsync();
                if (record == null)
                {
                    return new RServiceResult<bool>(false, "PDFBook not found.");
                }

                List<int> contributorAuthorIds = await _QueuePDFBookRemovalAsync(_context, record);

                await _context.SaveChangesAsync();

                // only after the above is committed - see _RemoveOrphanedAuthorsAsync
                if (contributorAuthorIds.Count > 0)
                {
                    await _RemoveOrphanedAuthorsAsync(_context, contributorAuthorIds);
                }
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// shared dependent-record cleanup + storage-cleanup queuing used by both
        /// RemovePDFBookAsync and the duplicate-merge flow. Removes whatever is still attached to
        /// `record` at call time (Pages, Tags, Contributers) and the book row itself, cleans up
        /// same-id references in tables with no FK constraint, and queues physical storage
        /// cleanup. Does NOT call SaveChangesAsync - the caller controls the transaction boundary,
        /// so a caller that first moves some of record's Tags/Contributers/references elsewhere
        /// (as the merge flow does, onto a survivor) only has the leftovers cleaned up here.
        /// Takes `context` explicitly (rather than always using the instance's own _context) so it
        /// can be called from a background job running against its own RMuseumDbContext, not just
        /// from a request-scoped call. Returns the AuthorIds that were linked only through the
        /// Contributers rows removed here, so the caller can hand them to
        /// _RemoveOrphanedAuthorsAsync once this removal is actually committed - this method
        /// itself must not do that check, since it runs before the caller's SaveChangesAsync.
        /// </summary>
        private async Task<List<int>> _QueuePDFBookRemovalAsync(RMuseumDbContext context, PDFBook record)
        {
            int pdfBookId = record.Id;
            var pageIds = record.Pages.Select(p => p.Id).ToArray();

            // --- dependent records that WILL block the delete with a foreign key violation
            //     if left in place (verified against the actual FK constraints in the schema) ---

            // user bookmarks pointing at this book directly, or at any of its pages
            var bookmarksToRemove =
                await context.PDFUserBookmarks
                .Where(bm => bm.PDFBookId == pdfBookId || (bm.PageId != null && pageIds.Contains(bm.PageId.Value)))
                .ToArrayAsync();
            context.PDFUserBookmarks.RemoveRange(bookmarksToRemove);

            // author ids about to lose this link - read via the AuthorId shadow property so this
            // works whether or not the caller included AuthorRole.Author. EF.Property<T>() only
            // works inside a live LINQ-to-Entities expression being translated to SQL -
            // record.Contributers is already an in-memory, materialized collection at this point
            // (LINQ-to-Objects from here on), so the shadow property value has to come from the
            // change tracker's own Entry(...).Property(...) API instead, which works regardless
            // of query context. Same fix as the one applied to Author merge's
            // _RepointAuthorRoleRowsAsync, which is where this exact mistake was first caught -
            // this one predates that fix and was missed at the time.
            List<int> contributorAuthorIds = record.Contributers
                .Select(c => (int)context.Entry(c).Property("AuthorId").CurrentValue)
                .Distinct()
                .ToList();

            // author/contributor links still attached to record (a caller may have already moved
            // some of these onto a survivor before calling this method)
            context.RemoveRange(record.Contributers);

            // --- dependent records that do NOT have a database-level foreign key constraint
            //     (so they would not block the delete), but would be left as orphaned rows
            //     silently pointing at a PDFBookId that no longer exists otherwise ---

            context.AIQueuedItems.RemoveRange(context.AIQueuedItems.Where(q => q.PDFBookId == pdfBookId));
            context.OCRQueuedItems.RemoveRange(context.OCRQueuedItems.Where(q => q.PDFBookId == pdfBookId));
            context.PDFGanjoorLinks.RemoveRange(context.PDFGanjoorLinks.Where(l => l.PDFBookId == pdfBookId));
            context.PinterestLinks.RemoveRange(context.PinterestLinks.Where(l => l.PDFBookId == pdfBookId));
            context.PDFVisitRecords.RemoveRange(context.PDFVisitRecords.Where(v => v.PDFBookId == pdfBookId));
            context.PDFBookDuplicateCandidates.RemoveRange(context.PDFBookDuplicateCandidates.Where(c => c.PDFBookId1 == pdfBookId || c.PDFBookId2 == pdfBookId));

            foreach (PDFPage page in record.Pages)
            {
                context.TagValues.RemoveRange(page.Tags);
            }

            context.RemoveRange(record.Pages);
            context.TagValues.RemoveRange(record.Tags);
            context.Remove(record);

            if (!string.IsNullOrEmpty(record.StorageFolderName))
            {
                context.PendingPDFStorageCleanups.Add
                (
                    new PendingPDFStorageCleanup()
                    {
                        Id = Guid.NewGuid(),
                        StorageFolderName = record.StorageFolderName,
                        NeedsFtpDelete = record.Status == PublishStatus.Published,
                        QueueTime = DateTime.Now
                    }
                );
            }

            return contributorAuthorIds;
        }

        /// <summary>
        /// get tagged publish pdfbooks (including CoverImage info but not pages or tagibutes info) 
        /// </summary>
        /// <param name="tagUrl"></param>
        /// <param name="valueUrl"></param>
        /// <param name="statusArray"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFBook[]>> GetPDFBookByTagValueAsync(string tagUrl, string valueUrl, PublishStatus[] statusArray)
        {
            try
            {
                RTag tag =
                        await _context.Tags
                        .Where(a => a.FriendlyUrl == tagUrl)
                    .SingleOrDefaultAsync();
                if (tag == null)
                    return new RServiceResult<PDFBook[]>(new PDFBook[] { });


                PDFBook[] taggedItems =
                    await _context.PDFBooks.Include(a => a.Tags)
                     .Include(a => a.CoverImage)
                    .Where(a => statusArray.Contains(a.Status) && a.Tags != null && a.Tags.Any(v => v.RTagId == tag.Id && v.FriendlyUrl == valueUrl))
                    .OrderByDescending(t => t.DateTime)
                    .AsNoTracking()
                    .ToArrayAsync();

                foreach (PDFBook taggedItem in taggedItems)
                {
                    taggedItem.Tags = null;
                    taggedItem.BookText = "";
                }

                return new RServiceResult<PDFBook[]>(taggedItems);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFBook[]>(null, exp.ToString());
            }

        }

        /// <summary>
        /// add pdf book tag value
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="rTag"></param>
        /// <returns></returns>
        public async Task<RServiceResult<RTagValue>> TagPDFBookAsync(int pdfBookId, RTag rTag)
        {
            try
            {
                RTag type = await _context.Tags.Where(a => a.Id == rTag.Id).SingleOrDefaultAsync();

                PDFBook item = await _context.PDFBooks.Include(i => i.Tags).Where(i => i.Id == pdfBookId).SingleOrDefaultAsync();

                int order = item.Tags.Where(t => t.RTagId == type.Id).Count() == 0 ? 1 : item.Tags.Where(t => t.RTagId == type.Id).OrderByDescending(t => t.Order).FirstOrDefault().Order + 1;

                RTagValue tag =
                new RTagValue()
                {
                    Order = order,
                    Value = "",
                    ValueInEnglish = "",
                    ValueSupplement = "",
                    RTag = type,
                    Status = PublishStatus.Published
                };

                item.Tags.Add(tag);
                item.LastModified = DateTime.Now;
                _context.Update(item);
                await _context.SaveChangesAsync();

                return new RServiceResult<RTagValue>(tag);
            }
            catch (Exception exp)
            {
                return new RServiceResult<RTagValue>(null, exp.ToString());
            }
        }

        /// <summary>
        /// remove pdf book tag value
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="tagValueId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> UnTagPDFBookAsync(int pdfBookId, Guid tagValueId)
        {
            try
            {
                PDFBook item = await _context.PDFBooks.Include(i => i.Tags).Where(i => i.Id == pdfBookId).SingleOrDefaultAsync();
                item.Tags.Remove(item.Tags.Where(t => t.Id == tagValueId).SingleOrDefault());
                item.LastModified = DateTime.Now;
                _context.Update(item);
                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }


        /// <summary>
        /// edit pdf book tag value
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="edited"></param>
        /// <param name="global">apply on all same value tags</param>
        /// <returns></returns>
        public async Task<RServiceResult<RTagValue>> EditPDFBookTagValueAsync(int pdfBookId, RTagValue edited, bool global)
        {
            try
            {
                if (string.IsNullOrEmpty(edited.Value))
                {
                    return new RServiceResult<RTagValue>(null, "Value could not be empty.");
                }

                PDFBook pdfBook =
                    await _context.PDFBooks
                     .Include(a => a.Tags)
                     .Where(a => a.Id == pdfBookId)
                    .SingleOrDefaultAsync();
                if (pdfBook == null)
                    return new RServiceResult<RTagValue>(null);

                RTagValue tag =
                    pdfBook.Tags.Where(a => a.Id == edited.Id)
                    .SingleOrDefault();


                if (tag != null)
                {
                    tag.Order = edited.Order;
                    tag.ValueSupplement = edited.ValueSupplement;
                    _context.Update(tag);

                    if (global)
                    {
                        RTagValue[] sameValueTags = await _context.TagValues.Where(v => v.Value == tag.Value && v.RTagId == tag.RTagId).ToArrayAsync();
                        foreach (RTagValue sameValueTag in sameValueTags)
                        {
                            sameValueTag.Value = edited.Value;
                            sameValueTag.ValueInEnglish = edited.ValueInEnglish;
                            sameValueTag.Status = edited.Status;
                            sameValueTag.FriendlyUrl = edited.FriendlyUrl;
                            _context.Update(sameValueTag);

                            RArtifactMasterRecord correspondingArtifact =
                                await _context.Artifacts.Include(a => a.Tags).Where(a => a.Tags.Contains(sameValueTag)).SingleOrDefaultAsync();
                            if (correspondingArtifact != null)
                            {
                                correspondingArtifact.LastModified = DateTime.Now;
                                _context.Update(correspondingArtifact);
                            }

                            RArtifactItemRecord correspondingItem =
                                await _context.Items.Include(a => a.Tags).Where(a => a.Tags.Contains(sameValueTag)).SingleOrDefaultAsync();
                            if (correspondingItem != null)
                            {
                                correspondingItem.LastModified = DateTime.Now;
                                _context.Update(correspondingItem);
                            }

                        }
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        tag.Value = edited.Value;
                        tag.ValueInEnglish = edited.ValueInEnglish;
                        tag.Order = edited.Order;
                        tag.FriendlyUrl = edited.FriendlyUrl;
                        tag.Status = edited.Status;
                        tag.ValueSupplement = edited.ValueSupplement;
                        _context.Update(tag);
                        pdfBook.LastModified = DateTime.Now;
                        _context.Update(pdfBook);
                        await _context.SaveChangesAsync();
                    }
                }
                return new RServiceResult<RTagValue>(tag);
            }
            catch (Exception exp)
            {
                return new RServiceResult<RTagValue>(null, exp.ToString());
            }
        }

        /// <summary>
        /// add author
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public async Task<RServiceResult<Author>> AddAuthorAsync(Author author)
        {
            try
            {
                _context.Add(author);
                await _context.SaveChangesAsync();
                return new RServiceResult<Author>(author);
            }
            catch (Exception exp)
            {
                return new RServiceResult<Author>(null, exp.ToString());
            }
        }

        /// <summary>
        /// update author
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<RServiceResult<Author>> UpdateAuthorAsync(Author model)
        {
            try
            {
                var dbAuthor = await _context.Authors.Where(author => author.Id == model.Id).SingleOrDefaultAsync();
                dbAuthor.Name = model.Name;
                dbAuthor.NameInOriginalLanguage = model.NameInOriginalLanguage;
                dbAuthor.Bio = model.Bio;
                dbAuthor.ImageId = model.ImageId;
                dbAuthor.ExtenalImageUrl = model.ExtenalImageUrl;
                dbAuthor.LastModified = DateTime.Now;
                _context.Update(dbAuthor);
                await _context.SaveChangesAsync();
                return new RServiceResult<Author>(model);
            }
            catch (Exception exp)
            {
                return new RServiceResult<Author>(null, exp.ToString());
            }
        }

        /// <summary>
        /// delete author by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <summary>
        /// delete author by id - for a generic/placeholder credit (e.g. "جمعی از نویسندگان")
        /// that shouldn't exist as a browsable/searchable author entry, but whose exact wording
        /// is still worth keeping on the books it's credited on. Removes every AuthorRole and
        /// PDFPinnedAuthor referencing this author first (see _RemoveAuthorRoleRowsAsync/
        /// _RemoveAuthorPinsAsync in PDFLibraryService-AuthorDelete.cs) - the previous version of
        /// this method skipped that and went straight for _context.Remove(dbAuthor), which would
        /// throw the same FK_AuthorRole_Authors_AuthorId violation debugged at length for Author
        /// merge, the first time it was called on any author with existing contributions.
        /// Deliberately does NOT touch any PDFBook's AuthorsLine/TranslatorsLine - unlike merge,
        /// there's no survivor to substitute in, and the wording is still meaningful as
        /// descriptive text on the book itself even once the structured Author entity is gone.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteAuthorAsync(int id)
        {
            try
            {
                var dbAuthor = await _context.Authors.Where(author => author.Id == id).SingleOrDefaultAsync();
                if (dbAuthor == null)
                {
                    return new RServiceResult<bool>(false, $"author {id} not found");
                }

                await _RemoveAuthorRoleRowsAsync(id);
                await _RemoveAuthorPinsAsync(id);

                _context.Remove(dbAuthor);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get author by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<Author>> GetAuthorByIdAsync(int id)
        {
            try
            {
                return new RServiceResult<Author>(await _context.Authors.AsNoTracking().Where(a => a.Id == id).SingleOrDefaultAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<Author>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get authors
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorName"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, Author[] Authors)>> GetAuthorsAsync(PagingParameterModel paging, string authorName)
        {
            try
            {
                var source =
                  _context.Authors
                  .Where(a => string.IsNullOrEmpty(authorName) || (authorName.Contains(a.Name) || (!string.IsNullOrEmpty(a.NameInOriginalLanguage) && authorName.Contains(a.NameInOriginalLanguage))))
                 .AsQueryable();
                (PaginationMetadata PagingMeta, Author[] Items) paginatedResult =
                    await QueryablePaginator<Author>.Paginate(source, paging);
                return new RServiceResult<(PaginationMetadata PagingMeta, Author[] Authors)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, Author[] Authors)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// list authors (optionally filtered to a single AuthorRole.Role, e.g. "مترجم", and/or by
        /// part of their name), each with a computed count of distinct books they're credited on,
        /// sortable by name (ascending) or by that book count (descending), paginated.
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="role">exact AuthorRole.Role to filter to (e.g. "نویسنده"); null/empty for all roles combined</param>
        /// <param name="sortByBookCountDesc">true: sort by book count descending; false: sort by name ascending</param>
        /// <param name="authorName">part of the author's name; null/empty for no name filter</param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, AuthorWithBookCount[] Authors)>> GetAuthorsWithBookCountAsync(PagingParameterModel paging, string role, bool sortByBookCountDesc, string authorName = null)
        {
            try
            {
                // NameInOriginalLanguage is unused and, in this dataset, always null - so a
                // Contains() check against it can never match anything. Filtering on Name alone
                // both matches actual data and keeps the generated WHERE clause simpler.
                var authorsFiltered =
                    _context.Authors
                    .Where(a => string.IsNullOrEmpty(authorName) || a.Name.Contains(authorName));

                if (!string.IsNullOrEmpty(role))
                {
                    // only authors who actually have at least one book in that role - a plain
                    // EXISTS-style filter, translated straightforwardly regardless of how BookCount
                    // itself ends up being computed below
                    authorsFiltered = authorsFiltered.Where(a =>
                        _context.Set<AuthorRole>().Any(ar => EF.Property<int>(ar, "AuthorId") == a.Id && ar.Role == role)
                    );
                }

                // AuthorRole can attach an author either directly to a Book (BookId) or to a
                // specific PDFBook (PDFBookId, which itself belongs to a Book via PDFBook.BookId).
                // AuthorId/BookId/PDFBookId are all EF Core shadow properties (no C# property on
                // AuthorRole itself), hence EF.Property<>() below.
                //
                // BookCount is computed as a per-row CORRELATED SCALAR SUBQUERY rather than a
                // separate GroupBy+LeftJoin composed with the Authors query. Two rounds of fixes
                // taught this the hard way: a GroupBy+LeftJoin's nullable-coalesce, while fine in a
                // plain SELECT, hits a genuine EF Core translation gap once it's also used as an
                // ORDER BY key (exactly the sortByBookCountDesc=true case) - "could not be
                // translated". A correlated COUNT subquery avoids that shape entirely: it's a
                // standard, well-supported "count of related rows per parent" pattern, and since
                // SQL COUNT() never returns NULL, there's no nullable-coalesce-in-ORDER-BY problem
                // to hit in the first place. "Number of books" is still counted at the Book level
                // (not PDFBook) - PDFBookId-attached rows are resolved to their owning Book - so
                // multiple scans/editions of the same work don't inflate the count.
                //
                // Only Id/Name/BookCount are ever projected - every other Author column
                // (NameInOriginalLanguage, Bio, ImageId, ExtenalImageUrl, LastModified) is unused
                // by this endpoint and always null in this data anyway. Since none of them are
                // referenced below, EF Core's generated SQL only selects Id and Name from Authors
                // rather than the full row - less data read and transferred per author.
                IQueryable<AuthorWithBookCount> source =
                    from a in authorsFiltered
                    select new AuthorWithBookCount
                    {
                        Id = a.Id,
                        Name = a.Name,
                        BookCount =
                            (
                                from ar in _context.Set<AuthorRole>()
                                where EF.Property<int>(ar, "AuthorId") == a.Id
                                where string.IsNullOrEmpty(role) || ar.Role == role
                                join pb in _context.PDFBooks on EF.Property<int?>(ar, "PDFBookId") equals (int?)pb.Id into pbGroup
                                from pb in pbGroup.DefaultIfEmpty()
                                // see GetAuthorsWithBookCountAsync history: casting directly to int?
                                // here (no `pb == null ? ... : ...` wrapper) is required - reading a
                                // non-nullable column from the unmatched side of a LEFT JOIN throws
                                // at materialization time otherwise. Not a concern here regardless,
                                // since this whole subquery collapses to a single COUNT and nothing
                                // intermediate ever gets materialized into .NET.
                                select EF.Property<int?>(ar, "BookId") ?? (int?)pb.BookId
                            )
                            .Where(bookId => bookId != null)
                            .Distinct()
                            .Count()
                    };

                source = sortByBookCountDesc
                    ? source.OrderByDescending(a => a.BookCount).ThenBy(a => a.Name)
                    : source.OrderBy(a => a.Name);

                (PaginationMetadata PagingMeta, AuthorWithBookCount[] Items) paginatedResult =
                    await QueryablePaginator<AuthorWithBookCount>.Paginate(source, paging);
                return new RServiceResult<(PaginationMetadata PagingMeta, AuthorWithBookCount[] Authors)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, AuthorWithBookCount[] Authors)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// add pdf book contributer
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> AddPDFBookContributerAsync(int pdfBookId, int authorId, string role)
        {
            try
            {
                role = role.Trim();
                var pdfBook = await _context.PDFBooks.Include(b => b.Contributers).ThenInclude(a => a.Author).Where(b => b.Id == pdfBookId).SingleAsync();
                if (pdfBook.Contributers.Any(a => a.Author.Id == authorId && a.Role == role))
                {
                    return new RServiceResult<bool>(false, "author contribution already added");
                }
                var author = await _context.Authors.AsNoTracking().Where(a => a.Id == authorId).SingleAsync();
                pdfBook.Contributers.Add(new AuthorRole()
                {
                    Author = author,
                    Role = role
                });
                _context.Update(pdfBook);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// remove contribution from pdf book
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="contributionId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeletePDFBookContributerAsync(int pdfBookId, int contributionId)
        {
            try
            {
                var pdfBook = await _context.PDFBooks.Include(b => b.Contributers).ThenInclude(a => a.Author).Where(b => b.Id == pdfBookId).SingleAsync();
                if (!pdfBook.Contributers.Any(a => a.Id == contributionId))
                {
                    return new RServiceResult<bool>(false, "author contribution not found.");
                }

                var contribution = pdfBook.Contributers.Where(a => a.Id == contributionId).Single();


                pdfBook.Contributers.Remove(contribution);
                _context.Update(pdfBook);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get published pdf books by author
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> GetPublishedPDFBooksByAuthorAsync(PagingParameterModel paging, int authorId, string role)
        {
            try
            {
                var source =
                _context.PDFBooks.AsNoTracking()
                .Include(a => a.CoverImage).Include(a => a.Contributers).ThenInclude(c => c.Author)
                .Where(a => a.Status == PublishStatus.Published && a.Contributers.Any(a => a.Author.Id == authorId && (string.IsNullOrEmpty(role) || (!string.IsNullOrEmpty(role) && a.Role == role))))
               .AsQueryable();
                (PaginationMetadata PagingMeta, PDFBook[] Books) paginatedResult =
                    await QueryablePaginator<PDFBook>.Paginate(source, paging);
                foreach (var book in paginatedResult.Books)
                {
                    book.BookText = "";
                }
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// get published pdf books by author stats (group by role)
        /// </summary>
        /// <param name="authorId"></param>
        /// <returns></returns>

        public async Task<RServiceResult<AuthorRoleCount[]>> GetPublishedPDFBookbyAuthorGroupedByRoleAsync(int authorId)
        {
            try
            {
                var books = await _context.PDFBooks.AsNoTracking().Include(b => b.Contributers).ThenInclude(c => c.Author)
                                        .Where(a => a.Status == PublishStatus.Published && a.Contributers.Any(a => a.Author.Id == authorId))
                                        .ToListAsync();
                Dictionary<string, int> roleCount = new Dictionary<string, int>();
                foreach (var book in books)
                {
                    foreach (var contributer in book.Contributers)
                    {
                        if (contributer.Author.Id == authorId)
                        {
                            if (roleCount.ContainsKey(contributer.Role))
                            {
                                roleCount[contributer.Role]++;                                
                            }
                            else
                            {
                                roleCount[contributer.Role] = 1;
                            }
                        }
                    }
                }

                List<AuthorRoleCount> authorRoles = new List<AuthorRoleCount>();
                foreach (var role in roleCount.Keys)
                {
                    authorRoles.Add
                        (
                        new AuthorRoleCount()
                        {
                            Role = role,
                            Count = roleCount[role]
                        }
                        );
                }
                return new RServiceResult<AuthorRoleCount[]>(authorRoles.ToArray());
            }
            catch (Exception exp)
            {
                return new RServiceResult<AuthorRoleCount[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get all books
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, Book[] Books)>> GetAllBooksAsync(PagingParameterModel paging)
        {
            try
            {
                var source =
                _context.Books.AsNoTracking()
                .Include(a => a.CoverImage)
               .OrderByDescending(t => t.Name)
               .AsQueryable();
                (PaginationMetadata PagingMeta, Book[] Books) paginatedResult =
                    await QueryablePaginator<Book>.Paginate(source, paging);
                return new RServiceResult<(PaginationMetadata PagingMeta, Book[] Books)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, Book[] Books)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// add book
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        public async Task<RServiceResult<Book>> AddBookAsync(Book book)
        {
            try
            {
                _context.Add(book);
                await _context.SaveChangesAsync();
                return new RServiceResult<Book>(book);
            }
            catch (Exception exp)
            {
                return new RServiceResult<Book>(null, exp.ToString());
            }
        }

        /// <summary>
        /// update book
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<RServiceResult<Book>> UpdateBookAsync(Book model)
        {
            try
            {
                var dbBook = await _context.Books.Where(b => b.Id == model.Id).SingleAsync();
                dbBook.Name = model.Name;
                dbBook.Description = model.Description;
                dbBook.CoverImageId = model.CoverImageId;
                dbBook.ExtenalCoverImageUrl = model.ExtenalCoverImageUrl;
                dbBook.LastModified = DateTime.Now;

                _context.Update(model);
                await _context.SaveChangesAsync();
                return new RServiceResult<Book>(model);
            }
            catch (Exception exp)
            {
                return new RServiceResult<Book>(null, exp.ToString());
            }
        }

        /// <summary>
        /// delete book
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteBookAsync(int id)
        {
            try
            {
                var dbBook = await _context.Books.Where(b => b.Id == id).SingleAsync();
                _context.Books.Remove(dbBook);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// add book author
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> AddBookAuthorAsync(int bookId, int authorId, string role)
        {
            try
            {
                role = role.Trim();
                var book = await _context.Books.Include(b => b.Authors).ThenInclude(a => a.Author).Where(b => b.Id == bookId).SingleAsync();
                if (book.Authors.Any(a => a.Author.Id == authorId && a.Role == role))
                {
                    return new RServiceResult<bool>(false, "author contribution already added.");
                }
                var author = await _context.Authors.AsNoTracking().Where(a => a.Id == authorId).SingleAsync();
                book.Authors.Add(new AuthorRole()
                {
                    Author = author,
                    Role = role
                });
                _context.Update(book);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// remove author from book
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="contributionId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteBookAuthorAsync(int bookId, int contributionId)
        {
            try
            {
                var book = await _context.Books.Include(b => b.Authors).ThenInclude(a => a.Author).Where(b => b.Id == bookId).SingleAsync();
                if (!book.Authors.Any(a => a.Id == contributionId))
                {
                    return new RServiceResult<bool>(false, "author contribution not found.");
                }

                var contribution = book.Authors.Where(a => a.Id == contributionId).Single();


                book.Authors.Remove(contribution);
                _context.Update(book);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// book by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<Book>> GetBookByIdAsync(int id)
        {
            try
            {
                var book = await _context.Books.AsNoTracking()
                    .Include(b => b.CoverImage)
                    .Include(b => b.Authors).ThenInclude(a => a.Author)
                    .Include(b => b.Tags)
                    .Where(b => b.Id == id).SingleOrDefaultAsync();
                if (book != null)
                {
                    book.PDFBooks = await _context.PDFBooks.AsNoTracking()
                        .Include(a => a.CoverImage)
                        .Where(a => a.Status == PublishStatus.Published && a.BookId == id)
                       .OrderByDescending(t => t.Title).ToArrayAsync();
                }

                return new RServiceResult<Book>(book);
            }
            catch (Exception exp)
            {
                return new RServiceResult<Book>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get books by author
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, Book[] Books)>> GetBooksByAuthorAsync(PagingParameterModel paging, int authorId, string role)
        {
            try
            {
                var source =
                _context.Books.AsNoTracking()
                .Include(a => a.CoverImage).Include(a => a.Authors).ThenInclude(c => c.Author)
                .Where(a => a.Authors.Any(a => a.Author.Id == authorId && (string.IsNullOrEmpty(role) || (!string.IsNullOrEmpty(role) && a.Role == role))))
               .AsQueryable();
                (PaginationMetadata PagingMeta, Book[] Books) paginatedResult =
                    await QueryablePaginator<Book>.Paginate(source, paging);
                return new RServiceResult<(PaginationMetadata PagingMeta, Book[] Books)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, Book[] Books)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// get books by author stats (group by role)
        /// </summary>
        /// <param name="authorId"></param>
        /// <returns></returns>

        public async Task<RServiceResult<AuthorRoleCount[]>> GetBookbyAuthorGroupedByRoleAsync(int authorId)
        {
            try
            {
                var books = await _context.Books.AsNoTracking().Include(b => b.Authors).ThenInclude(c => c.Author)
                                        .Where(a => a.Authors.Any(a => a.Author.Id == authorId))
                                        .ToListAsync();
                Dictionary<string, int> roleCount = new Dictionary<string, int>();
                foreach (var book in books)
                {
                    foreach (var contributer in book.Authors)
                    {
                        if (contributer.Author.Id == authorId)
                        {
                            if (roleCount.ContainsKey(contributer.Role))
                            {
                                roleCount[contributer.Role] = 1;
                            }
                            else
                            {
                                roleCount[contributer.Role]++;
                            }
                        }
                    }
                }

                List<AuthorRoleCount> authorRoles = new List<AuthorRoleCount>();
                foreach (var role in roleCount.Keys)
                {
                    authorRoles.Add
                        (
                        new AuthorRoleCount()
                        {
                            Role = role,
                            Count = roleCount[role]
                        }
                        );
                }
                return new RServiceResult<AuthorRoleCount[]>(authorRoles.ToArray());
            }
            catch (Exception exp)
            {
                return new RServiceResult<AuthorRoleCount[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get book related pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="bookId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> GetBookRelatedPDFBooksAsync(PagingParameterModel paging, int bookId)
        {
            try
            {
                var source =
                _context.PDFBooks.AsNoTracking()
                .Include(a => a.CoverImage)
                .Where(a => a.Status == PublishStatus.Published && a.BookId == bookId)
               .OrderByDescending(t => t.Title)
               .AsQueryable();
                (PaginationMetadata PagingMeta, PDFBook[] Books) paginatedResult =
                    await QueryablePaginator<PDFBook>.Paginate(source, paging);
                foreach (var book in paginatedResult.Books)
                {
                    book.BookText = "";
                }
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>((null, null), exp.ToString());
            }
        }


        /// <summary>
        /// add multi volume pdf collection
        /// </summary>
        /// <param name="multiVolumePDFCollection"></param>
        /// <returns></returns>
        public async Task<RServiceResult<MultiVolumePDFCollection>> AddMultiVolumePDFCollectionAsync(MultiVolumePDFCollection multiVolumePDFCollection)
        {
            try
            {
                _context.Add(multiVolumePDFCollection);
                await _context.SaveChangesAsync();
                return new RServiceResult<MultiVolumePDFCollection>(multiVolumePDFCollection);
            }
            catch (Exception exp)
            {
                return new RServiceResult<MultiVolumePDFCollection>(null, exp.ToString());
            }
        }

        /// <summary>
        /// update multi volume pdf collection
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<RServiceResult<MultiVolumePDFCollection>> UpdateMultiVolumePDFCollectionAsync(MultiVolumePDFCollection model)
        {
            try
            {
                var dbVolumes = await _context.MultiVolumePDFCollections.Where(x => x.Id == model.Id).SingleAsync();
                dbVolumes.Name = model.Name;
                dbVolumes.Description = model.Description;
                dbVolumes.VolumeCount = model.VolumeCount;
                dbVolumes.BookId = model.BookId;
                _context.Update(dbVolumes);
                await _context.SaveChangesAsync();
                return new RServiceResult<MultiVolumePDFCollection>(model);
            }
            catch (Exception exp)
            {
                return new RServiceResult<MultiVolumePDFCollection>(null, exp.ToString());
            }
        }

        /// <summary>
        /// delete multi volume pdf collection
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteMultiVolumePDFCollectionAsync(int id)
        {
            try
            {
                var dbVolumes = await _context.MultiVolumePDFCollections.Where(x => x.Id == id).SingleAsync();
                _context.Remove(dbVolumes);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get volumes by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<MultiVolumePDFCollection>> GetMultiVolumePDFCollectionByIdAsync(int id)
        {
            try
            {
                var volumes = await _context.MultiVolumePDFCollections.Include(v => v.Book).AsNoTracking().Where(x => x.Id == id).SingleOrDefaultAsync();
                if (volumes != null)
                {
                    volumes.PDFBooks = await _context.PDFBooks.AsNoTracking()
                        .Include(a => a.CoverImage)
                        .Where(a => a.Status == PublishStatus.Published && a.MultiVolumePDFCollectionId == id)
                       .OrderBy(t => t.VolumeOrder)
                       .ToArrayAsync();
                }
                return new RServiceResult<MultiVolumePDFCollection>(volumes);
            }
            catch (Exception exp)
            {
                return new RServiceResult<MultiVolumePDFCollection>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get volumes pdf books
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFBook[]>> GetVolumesPDFBooks(int volumeId)
        {
            try
            {
                var books =
                await _context.PDFBooks.AsNoTracking()
                   .Include(a => a.CoverImage)
                   .Where(a => a.Status == PublishStatus.Published && a.MultiVolumePDFCollectionId == volumeId)
                   .OrderBy(t => t.VolumeOrder)
                   .ToArrayAsync();
                foreach (var book in books)
                {
                    book.BookText = "";
                }
                return new RServiceResult<PDFBook[]>(books);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFBook[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get pdf source by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFSource>> GetPDFSourceByIdAsync(int id)
        {
            try
            {
                return new RServiceResult<PDFSource>(await _context.PDFSources.AsNoTracking().Where(s => s.Id == id).SingleOrDefaultAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFSource>(null, exp.ToString());
            }
        }

        /// <summary>
        /// Get All PDF Sources
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<PDFSource[]>> GetPDFSourcesAsync()
        {
            try
            {
                return new RServiceResult<PDFSource[]>(await _context.PDFSources.AsNoTracking().ToArrayAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFSource[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// Add PDF Source
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFSource>> AddPDFSourceAsync(PDFSource source)
        {
            try
            {
                _context.PDFSources.Add(source);
                await _context.SaveChangesAsync();
                return new RServiceResult<PDFSource>(source);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFSource>(null, exp.ToString());
            }
        }

        /// <summary>
        /// update PDF Source
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFSource>> UpdatePDFSourceAsync(PDFSource model)
        {
            try
            {
                var dbSource = await _context.PDFSources.Where(s => s.Id == model.Id).SingleAsync();
                dbSource.Name = model.Name;
                dbSource.Description = model.Description;
                dbSource.Url = model.Url;
                _context.Update(dbSource);
                await _context.SaveChangesAsync();
                return new RServiceResult<PDFSource>(dbSource);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFSource>(null, exp.ToString());
            }
        }

        /// <summary>
        /// delete PDF Source
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeletePDFSourceAsync(int id)
        {
            try
            {
                var dbSource = await _context.PDFSources.Where(s => s.Id == id).SingleAsync();
                _context.Remove(dbSource);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get source pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="sourceId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> GetSourceRelatedPDFBooksAsync(PagingParameterModel paging, int sourceId)
        {
            try
            {
                var source =
                _context.PDFBooks.AsNoTracking()
                   .Include(a => a.CoverImage)
                   .Where(a => a.Status == PublishStatus.Published && a.PDFSourceId == sourceId)
                   .OrderByDescending(t => t.Title)
                   .AsQueryable();
                (PaginationMetadata PagingMeta, PDFBook[] Books) paginatedResult =
                    await QueryablePaginator<PDFBook>.Paginate(source, paging);
                foreach (var book in paginatedResult.Books)
                {
                    book.BookText = "";
                }
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// search pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Items)>> SearchPDFBooksAsync(PagingParameterModel paging, string term)
        {
            try
            {
                term = term.Trim().ApplyCorrectYeKe();
                term = term.Replace("‌", " ");//replace zwnj with space

                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Items)>((null, null), "خطای طول عبارت جستجو");
                }

                string searchConditions;
                if (term.IndexOf('"') == 0 && term.LastIndexOf('"') == (term.Length - 1))
                {
                    searchConditions = term.Replace("\"", "").Replace("'", "");
                    searchConditions = $"\"{searchConditions}\"";
                }
                else
                {
                    string[] words = term.Replace("\"", "").Replace("'", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    searchConditions = "";
                    string emptyOrAnd = "";
                    foreach (string word in words)
                    {
                        // trailing wildcard only - SQL Server's CONTAINS predicate does not
                        // support leading wildcards at all (undocumented/unsupported, not just
                        // slow), so "*word*" here was never a valid substring search to begin
                        // with. "word*" is the supported prefix-match form and is what the
                        // full-text index can actually seek efficiently. Trade-off: a search for
                        // "خانه" now only matches words starting with it (e.g. "خانه‌ای"), not a
                        // mid-word occurrence like "کتابخانه" - inherent to CONTAINS, not
                        // something fixable while staying on it.
                        searchConditions += $" {emptyOrAnd} \"{word}*\" ";
                        emptyOrAnd = " AND ";
                    }
                }
                //full text catalogue should be created manually

                var source =
                    _context.PDFBooks.AsNoTracking()
                    // Tags is a to-many collection - without AsSplitQuery, combining a
                    // collection like this with the paginator's Skip/Take below joins and
                    // multiplies each matched book by its own tag count before pagination can
                    // even apply, repeating that cost across every row in the page. Same
                    // reasoning already applied to the merge queries in
                    // PDFLibraryService-Merge.cs. Still applies here even though Tags is now
                    // brought in via the Select projection below rather than Include - it's the
                    // same collection-materialization behavior either way.
                    .AsSplitQuery()
                    .Where(p =>
                           p.Status == PublishStatus.Published
                           &&
                           (
                           EF.Functions.Contains(p.Title, searchConditions)
                           ||
                           EF.Functions.Contains(p.AuthorsLine, searchConditions)
                           ||
                           EF.Functions.Contains(p.TranslatorsLine, searchConditions)
                           ||
                           EF.Functions.Contains(p.Description, searchConditions)
                           ||
                           p.Tags.Where(t => EF.Functions.Contains(t.Value, searchConditions) || EF.Functions.Contains(t.ValueInEnglish, searchConditions)).Any()
                           )
                           )
                    // explicit projection, excluding BookText - the full OCR'd text of the
                    // whole book, not searched or shown here, can be large per book, and was
                    // being loaded for every row on every page only to be immediately discarded
                    // a few lines down. Tags is kept (this method's callers expect it, unlike
                    // SearchPDFBookForPDFPagesTextAsync); every other field mirrors what this
                    // query already returned before (PDFFile/CoverImage/Contributers/Pages/
                    // PDFSource/Book/MultiVolumePDFCollection were never Include()'d here
                    // either, so they stay unset, same as before).
                    .Select(p => new PDFBook
                    {
                        Id = p.Id,
                        BookId = p.BookId,
                        Status = p.Status,
                        Title = p.Title,
                        SubTitle = p.SubTitle,
                        AuthorsLine = p.AuthorsLine,
                        ISBN = p.ISBN,
                        Description = p.Description,
                        Language = p.Language,
                        IsTranslation = p.IsTranslation,
                        TranslatorsLine = p.TranslatorsLine,
                        TitleInOriginalLanguage = p.TitleInOriginalLanguage,
                        PublisherLine = p.PublisherLine,
                        PublishingDate = p.PublishingDate,
                        PublishingLocation = p.PublishingLocation,
                        PublishingNumber = p.PublishingNumber,
                        ClaimedPageCount = p.ClaimedPageCount,
                        MultiVolumePDFCollectionId = p.MultiVolumePDFCollectionId,
                        VolumeOrder = p.VolumeOrder,
                        DateTime = p.DateTime,
                        LastModified = p.LastModified,
                        ExternalPDFFileUrl = p.ExternalPDFFileUrl,
                        CoverImageId = p.CoverImageId,
                        ExtenalCoverImageUrl = p.ExtenalCoverImageUrl,
                        OriginalSourceName = p.OriginalSourceName,
                        OriginalSourceUrl = p.OriginalSourceUrl,
                        OriginalFileUrl = p.OriginalFileUrl,
                        PageCount = p.PageCount,
                        Tags = p.Tags,
                        FileMD5CheckSum = p.FileMD5CheckSum,
                        OriginalFileName = p.OriginalFileName,
                        StorageFolderName = p.StorageFolderName,
                        BookScriptType = p.BookScriptType,
                        PDFSourceId = p.PDFSourceId,
                        OCRed = p.OCRed,
                        OCRTime = p.OCRTime,
                        AIRevised = p.AIRevised,
                    })
                    .OrderBy(i => i.Title);


                (PaginationMetadata PagingMeta, PDFBook[] Items) paginatedResult =
                   await QueryablePaginator<PDFBook>.Paginate(source, paging);

                // no BookText-clearing loop needed anymore - the projection above never
                // loads it in the first place, so every returned book's BookText is already
                // its default (null)


                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Items)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Items)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// check to see if book is related to poem
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="poemId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> IsBookRelatedToPoemAsync(int bookId, int poemId)
        {
            try
            {
                return new RServiceResult<bool>
                    (
                    await _context.PDFGanjoorLinks.AsNoTracking().
                        Where(l => l.GanjoorPostId == poemId && l.PDFBookId == bookId && l.ReviewResult != ReviewResult.Rejected)
                            .AnyAsync()
                    );
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// suggest ganjoor link
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="link"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> SuggestGanjoorLinkAsync(Guid userId, PDFGanjoorLinkSuggestion link)
        {
            try
            {
                PDFBook pdfBook = await _context.PDFBooks.AsNoTracking().Where(a => a.Id == link.PDFBookId).SingleOrDefaultAsync();

                PDFGanjoorLink alreadySuggest =
                await _context.PDFGanjoorLinks.AsNoTracking().
                    Where(l => l.GanjoorPostId == link.GanjoorPostId && l.PDFBookId == pdfBook.Id && l.PageNumber == link.PageNumber && l.ReviewResult != ReviewResult.Rejected)
                    .SingleOrDefaultAsync();
                if (alreadySuggest != null)
                    return new RServiceResult<bool>(false, "این مورد پیشتر پیشنهاد شده است.");

                string pdfPageTitle = pdfBook.Title;
                if (!string.IsNullOrEmpty(pdfBook.AuthorsLine))
                {
                    pdfPageTitle += $" - {pdfBook.AuthorsLine}";
                }

                PDFGanjoorLink suggestion =
                    new PDFGanjoorLink()
                    {
                        GanjoorPostId = link.GanjoorPostId,
                        GanjoorTitle = link.GanjoorTitle,
                        GanjoorUrl = link.GanjoorUrl,
                        PDFBookId = pdfBook.Id,
                        PageNumber = link.PageNumber,
                        SuggestedById = userId,
                        SuggestionDate = DateTime.Now,
                        ReviewResult = ReviewResult.Awaiting,
                        ExternalThumbnailImageUrl = (await _context.PDFPages.AsNoTracking().Where(l => l.PDFBookId == link.PDFBookId && l.PageNumber == link.PageNumber).SingleAsync()).ExtenalThumbnailImageUrl,
                        PDFPageTitle = pdfPageTitle + " - تصویر " + link.PageNumber.ToString().ToPersianNumbers(),
                        IsTextOriginalSource = link.IsTextOriginalSource,
                        SuggestedByMachine = link.SuggestedByMachine,
                    };

                _context.PDFGanjoorLinks.Add(suggestion);
                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// finds what the method name suggests
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="onlyMachineSuggested"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorLinkViewModel>> GetNextUnreviewedGanjoorLinkAsync(int skip, bool onlyMachineSuggested)
        {
            try
            {
                var link = await _context.PDFGanjoorLinks.AsNoTracking()
                 .Include(l => l.SuggestedBy)
                 .Where
                 (
                    l =>
                    l.ReviewResult == ReviewResult.Awaiting
                    &&
                    (onlyMachineSuggested == false || l.SuggestedByMachine == true)
                    )
                 .OrderBy(l => l.SuggestionDate)
                 .Skip(skip)
                 .FirstOrDefaultAsync();
                if (link != null)
                {
                    return new RServiceResult<GanjoorLinkViewModel>(
                        new GanjoorLinkViewModel()
                        {
                            Id = link.Id,
                            GanjoorPostId = link.GanjoorPostId,
                            GanjoorUrl = link.GanjoorUrl,
                            GanjoorTitle = link.GanjoorTitle,
                            EntityName = link.PDFPageTitle,
                            EntityFriendlyUrl = $"https://naskban.ir/{link.PDFBookId}/{link.PageNumber}",
                            ReviewResult = link.ReviewResult,
                            Synchronized = link.Synchronized,
                            SuggestedBy = new PublicRAppUser()
                            {
                                Id = link.SuggestedBy.Id,
                                Username = link.SuggestedBy.UserName,
                                Email = link.SuggestedBy.Email,
                                FirstName = link.SuggestedBy.FirstName,
                                SurName = link.SuggestedBy.SurName,
                                PhoneNumber = link.SuggestedBy.PhoneNumber,
                                RImageId = link.SuggestedBy.RImageId,
                                Status = link.SuggestedBy.Status,
                                NickName = link.SuggestedBy.NickName,
                                Website = link.SuggestedBy.Website,
                                Bio = link.SuggestedBy.Bio,
                                EmailConfirmed = link.SuggestedBy.EmailConfirmed
                            },
                            IsTextOriginalSource = link.IsTextOriginalSource,
                        });
                }
                return new RServiceResult<GanjoorLinkViewModel>(null);
            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorLinkViewModel>(null, exp.ToString());
            }

        }

        /// <summary>
        /// get unreviewed image count
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<int>> GetUnreviewedGanjoorLinksCountAsync()
        {
            try
            {
                return new RServiceResult<int>
               (
                 await _context.PDFGanjoorLinks.AsNoTracking()
                .Where(l => l.ReviewResult == ReviewResult.Awaiting)
                .CountAsync()
                );
            }
            catch (Exception exp)
            {
                return new RServiceResult<int>(0, exp.ToString());
            }

        }

        /// <summary>
        /// get unsynced approved pdf ganjoor links
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<PDFGanjoorLink[]>> GetUnsyncedPDFGanjoorLinksAsync()
        {
            try
            {
                return new RServiceResult<PDFGanjoorLink[]>
                    (
                    await _context.PDFGanjoorLinks.AsNoTracking()
                            .Where(l => l.ReviewResult == ReviewResult.Approved && l.Synchronized == false)
                            .ToArrayAsync()
                    );
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFGanjoorLink[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// synchronize ganjoor link
        /// </summary>
        /// <param name="linkId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> SynchronizePDFGanjoorLinkAsync(Guid linkId)
        {
            try
            {
                var link = await _context.PDFGanjoorLinks.Where(l => l.Id == linkId).SingleAsync();
                link.Synchronized = true;
                _context.PDFGanjoorLinks.Update(link);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        private async Task<string> GetGanjoorPageFirstVerseAsync(string ganjoorUrl)
        {
            ganjoorUrl = ganjoorUrl.Replace("https://ganjoor.net", "");
            using (HttpClient client = new HttpClient())
            {
                var pageQuery = await client.GetAsync($"https://api.ganjoor.net/api/ganjoor/page?url={ganjoorUrl}");
                if (!pageQuery.IsSuccessStatusCode)
                {
                    return "";
                }
                var ganjoorPage = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<GanjoorPageCompleteViewModel>();
                var verse = ganjoorPage.Poem.Verses.Where(v => v.VersePosition != VersePosition.Comment).OrderBy(v => v.VOrder).FirstOrDefault();
                if (verse != null)
                {
                    return verse.Text;
                }
            }
            return "";
        }

        /// <summary>
        /// Review Suggested Link
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="userId"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> ReviewSuggestedLinkAsync(Guid linkId, Guid userId, ReviewResult result)
        {
            try
            {
                PDFGanjoorLink link =
                await _context.PDFGanjoorLinks
                 .Where(l => l.Id == linkId)
                 .SingleAsync();

                link.ReviewResult = result;
                link.ReviewerId = userId;
                link.ReviewDate = DateTime.Now;


                _context.PDFGanjoorLinks.Update(link);

                if (link.ReviewResult == ReviewResult.Approved)
                {
                    string firstVerse = await GetGanjoorPageFirstVerseAsync(link.GanjoorUrl);
                    var pageInfo = await _context.PDFPages
                                            .Include(i => i.Tags)
                                            .ThenInclude(t => t.RTag)
                                            .Where(i => i.PDFBookId == link.PDFBookId && i.PageNumber == link.PageNumber).SingleAsync();



                    RTagValue tag = await TagHandler.PrepareAttribute(_context, "Ganjoor Link", link.GanjoorTitle, 1);
                    tag.ValueSupplement = link.GanjoorUrl;
                    pageInfo.Tags.Add(tag);
                    _context.PDFPages.Update(pageInfo);

                    if (!string.IsNullOrEmpty(firstVerse))
                    {
                        if (firstVerse.Length > 100)
                        {
                            firstVerse = firstVerse.Substring(0, 50);
                            int n = firstVerse.LastIndexOf(' ');
                            if (n >= 0)
                            {
                                firstVerse = firstVerse.Substring(0, n) + " ...";
                            }
                            else
                            {
                                firstVerse += "...";
                            }
                        }
                        link.GanjoorTitle = $"{link.GanjoorTitle}: {firstVerse}";
                    }


                    RTagValue toc = await TagHandler.PrepareAttribute(_context, "Title in TOC", link.GanjoorTitle, 1);
                    toc.ValueSupplement = "1";//font size
                    if (pageInfo.Tags.Where(t => t.RTag.Name == "Title in TOC" && t.Value == toc.Value).Count() == 0)
                    {
                        toc.Order = 1 + pageInfo.Tags.Where(t => t.RTag.NameInEnglish == "Title in TOC").Count();
                        pageInfo.Tags.Add(toc);
                        _context.PDFPages.Update(pageInfo);
                    }
                }

                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception e)
            {
                return new RServiceResult<bool>(false, e.ToString());
            }
            
        }

        /// <summary>
        /// get next un-ocred PDF Book and add it to a queue
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<PDFBook>> GetNextUnOCRedPDFBookAsync()
        {
            try
            {
                var maxOCRQueuedItemId =
                    await _context.OCRQueuedItems.AnyAsync() ?
                    await _context.OCRQueuedItems.AsNoTracking().MaxAsync(q => q.PDFBookId)
                    : 0;
                var pdfBook = await _context.PDFBooks.AsNoTracking()
                            .Include(b => b.PDFFile)
                            .Include(b => b.Pages)
                            .Where(b => b.Status == PublishStatus.Published && b.OCRed == false && b.Id > maxOCRQueuedItemId)
                            .OrderBy(b => b.Id)
                            .FirstOrDefaultAsync();
                if (pdfBook != null)
                {
                    _context.OCRQueuedItems.Add
                        (new OCRQueue()
                        {
                            PDFBookId = pdfBook.Id
                        });
                    await _context.SaveChangesAsync();
                }
                return new RServiceResult<PDFBook>(pdfBook);

            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFBook>(null, exp.ToString());
            }
        }

        /// <summary>
        /// reset OCR Queue (remove queued items)
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> ResetOCRQueueAsync()
        {
            try
            {
                var ocrQueuedItems = await _context.OCRQueuedItems.ToListAsync();
                if (ocrQueuedItems.Count > 0)
                {
                    _context.RemoveRange(ocrQueuedItems);
                    await _context.SaveChangesAsync();
                }
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
                throw;
            }
        }

        /// <summary>
        /// set pdf page ocr info (and if a book whole pages are ocred the book ocred flag is set to true)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> SetPDFPageOCRInfoAsync(PDFPageOCRDataViewModel model)
        {
            try
            {
                var dbPage = await _context.PDFPages.Where(p => p.Id == model.Id).SingleAsync();
                if (model.AIRevised && !dbPage.AIRevised)
                {
                    if (!string.IsNullOrEmpty(dbPage.PageText))
                    {
                        _context.Add
                            (
                            new PDFPageUnrevisedText()
                            {
                                PageId = dbPage.Id,
                                PageText = dbPage.PageText,
                            }
                            );
                        await _context.SaveChangesAsync();
                    }
                }
                if (model.FullResolutionImageWidth != 0 && model.FullResolutionImageHeight != 0)
                {
                    dbPage.FullResolutionImageWidth = model.FullResolutionImageWidth;
                    dbPage.FullResolutionImageHeight = model.FullResolutionImageHeight;
                }
                if (model.AIRevised)
                    dbPage.AIRevised = true;
                else
                {
                    dbPage.OCRed = model.OCRed;
                    dbPage.OCRTime = DateTime.Now;
                }



                dbPage.PageText = model.PageText;
                _context.Update(dbPage);
                await _context.SaveChangesAsync();

                var pdfBook = await _context.PDFBooks
                            .Include(b => b.Pages)
                            .Where(b => b.Id == dbPage.PDFBookId)
                            .SingleAsync();
                if (model.AIRevised)
                {
                    if (!pdfBook.Pages.Any(p => p.AIRevised == false))
                    {
                        pdfBook.AIRevised = true;
                        string oldBookText = pdfBook.BookText;
                        try
                        {
                            string bookText = "";
                            foreach (var page in pdfBook.Pages.OrderBy(p => p.PageNumber))
                            {
                                if (!string.IsNullOrEmpty(page.PageText))
                                {
                                    bookText += page.PageText;
                                    bookText += Environment.NewLine;
                                }
                            }
                            pdfBook.BookText = bookText;
                            _context.Update(pdfBook);
                            await _context.SaveChangesAsync();
                        }
                        catch //if book text exceeds field max length, is it possible?
                        {
                            pdfBook.BookText = oldBookText;
                            _context.Update(pdfBook);
                            await _context.SaveChangesAsync();
                        }

                    }
                }
                else
                if (!pdfBook.Pages.Any(p => p.OCRed == false))
                {
                    pdfBook.OCRed = true;
                    pdfBook.OCRTime = DateTime.Now;

                    try
                    {
                        string bookText = "";
                        foreach (var page in pdfBook.Pages.OrderBy(p => p.PageNumber))
                        {
                            if (!string.IsNullOrEmpty(page.PageText))
                            {
                                bookText += page.PageText;
                                bookText += Environment.NewLine;
                            }
                        }
                        pdfBook.BookText = bookText;
                        _context.Update(pdfBook);
                        await _context.SaveChangesAsync();
                    }
                    catch //if book text exceeds field max length, is it possible?
                    {
                        pdfBook.BookText = "";
                        _context.Update(pdfBook);
                        await _context.SaveChangesAsync();
                    }
                }
                else if (pdfBook.OCRed)
                {
                    pdfBook.OCRed = false;
                    _context.Update(pdfBook);
                    await _context.SaveChangesAsync();
                }
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get next un-aied PDF Book and add it to a queue
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<PDFBook>> GetNextUnAIedPDFBookAsync()
        {
            try
            {
                var maxOCRQueuedItemId =
                    await _context.AIQueuedItems.AnyAsync() ?
                    await _context.AIQueuedItems.AsNoTracking().MaxAsync(q => q.PDFBookId)
                    : 0;
                var pdfBook = await _context.PDFBooks.AsNoTracking()
                            .Include(b => b.PDFFile)
                            .Include(b => b.Pages)
                            .Where(b => b.Status == PublishStatus.Published && b.OCRed == true && b.AIRevised == false && b.Id > maxOCRQueuedItemId)
                            .OrderBy(b => b.Id)
                            .FirstOrDefaultAsync();
                if (pdfBook != null)
                {
                    _context.AIQueuedItems.Add
                        (new AIQueue()
                        {
                            PDFBookId = pdfBook.Id
                        });
                    await _context.SaveChangesAsync();
                }
                return new RServiceResult<PDFBook>(pdfBook);

            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFBook>(null, exp.ToString());
            }
        }

        /// <summary>
        /// reset AI Queue (remove queued items)
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> ResetAIQueueAsync()
        {
            try
            {
                var ocrQueuedItems = await _context.AIQueuedItems.ToListAsync();
                if (ocrQueuedItems.Count > 0)
                {
                    _context.RemoveRange(ocrQueuedItems);
                    await _context.SaveChangesAsync();
                }
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
                throw;
            }
        }

        /// <summary>
        /// search pdf books pages for a text
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> SearchPDFBookForPDFPagesTextAsync(PagingParameterModel paging, string term)
        {
            try
            {
                term = term.Trim().ApplyCorrectYeKe();
                term = term.Replace("‌", " ");//replace zwnj with space

                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Items)>((null, null), "خطای طول عبارت جستجو");
                }

                string searchConditions;
                if (term.IndexOf('"') == 0 && term.LastIndexOf('"') == (term.Length - 1))
                {
                    searchConditions = term.Replace("\"", "").Replace("'", "");
                    searchConditions = $"\"{searchConditions}\"";
                }
                else
                {
                    string[] words = term.Replace("\"", "").Replace("'", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    searchConditions = "";
                    string emptyOrAnd = "";
                    foreach (string word in words)
                    {
                        // same fix as SearchPDFBooksAsync - trailing wildcard only, see that
                        // method's comment on why "*word*" was never valid CONTAINS syntax
                        searchConditions += $" {emptyOrAnd} \"{word}*\" ";
                        emptyOrAnd = " AND ";
                    }
                }
                //full text catalogue should be created manually

                var source =
                    _context.PDFBooks.AsNoTracking()
                    .Where(p =>
                           p.Status == PublishStatus.Published
                           &&
                           EF.Functions.Contains(p.BookText, searchConditions)
                           )
                    // explicit projection, excluding BookText - it's what's being searched
                    // (via CONTAINS above, evaluated server-side against the indexed column,
                    // not needing it back in the result) and can be large per book; loading it
                    // for every row on every page only to immediately discard it a few lines
                    // down was pure wasted network/memory cost. Every other field mirrors what
                    // this query already returned before (no navigation properties were ever
                    // Include()'d here, so PDFFile/CoverImage/Contributers/Pages/Tags/PDFSource/
                    // Book/MultiVolumePDFCollection stay unset here too, same as before).
                    .Select(p => new PDFBook
                    {
                        Id = p.Id,
                        BookId = p.BookId,
                        Status = p.Status,
                        Title = p.Title,
                        SubTitle = p.SubTitle,
                        AuthorsLine = p.AuthorsLine,
                        ISBN = p.ISBN,
                        Description = p.Description,
                        Language = p.Language,
                        IsTranslation = p.IsTranslation,
                        TranslatorsLine = p.TranslatorsLine,
                        TitleInOriginalLanguage = p.TitleInOriginalLanguage,
                        PublisherLine = p.PublisherLine,
                        PublishingDate = p.PublishingDate,
                        PublishingLocation = p.PublishingLocation,
                        PublishingNumber = p.PublishingNumber,
                        ClaimedPageCount = p.ClaimedPageCount,
                        MultiVolumePDFCollectionId = p.MultiVolumePDFCollectionId,
                        VolumeOrder = p.VolumeOrder,
                        DateTime = p.DateTime,
                        LastModified = p.LastModified,
                        ExternalPDFFileUrl = p.ExternalPDFFileUrl,
                        CoverImageId = p.CoverImageId,
                        ExtenalCoverImageUrl = p.ExtenalCoverImageUrl,
                        OriginalSourceName = p.OriginalSourceName,
                        OriginalSourceUrl = p.OriginalSourceUrl,
                        OriginalFileUrl = p.OriginalFileUrl,
                        PageCount = p.PageCount,
                        FileMD5CheckSum = p.FileMD5CheckSum,
                        OriginalFileName = p.OriginalFileName,
                        StorageFolderName = p.StorageFolderName,
                        BookScriptType = p.BookScriptType,
                        PDFSourceId = p.PDFSourceId,
                        OCRed = p.OCRed,
                        OCRTime = p.OCRTime,
                        AIRevised = p.AIRevised,
                    })
                    .OrderBy(i => i.Title);


                (PaginationMetadata PagingMeta, PDFBook[] Books) paginatedResult =
                   await QueryablePaginator<PDFBook>.Paginate(source, paging);

                // no BookText-clearing loop needed anymore - the projection above never
                // loads it in the first place, so every returned book's BookText is already
                // its default (null)

                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// search pdf pages
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="bookId">0 for all pdf books</param>
        /// <param name="term"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFPage[] Items)>> SearchPDFPagesTextAsync(PagingParameterModel paging, int bookId, string term)
        {
            try
            {
                term = term.Trim().ApplyCorrectYeKe();
                term = term.Replace("‌", " ");//replace zwnj with space

                if (string.IsNullOrEmpty(term) || term.Length < 2)
                {
                    return new RServiceResult<(PaginationMetadata PagingMeta, PDFPage[] Items)>((null, null), "خطای طول عبارت جستجو");
                }

                string searchConditions;
                if (term.IndexOf('"') == 0 && term.LastIndexOf('"') == (term.Length - 1))
                {
                    searchConditions = term.Replace("\"", "").Replace("'", "");
                    searchConditions = $"\"{searchConditions}\"";
                }
                else
                {
                    string[] words = term.Replace("\"", "").Replace("'", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    searchConditions = "";
                    string emptyOrAnd = "";
                    foreach (string word in words)
                    {
                        // same fix as SearchPDFBooksAsync - trailing wildcard only, see that
                        // method's comment on why "*word*" was never valid CONTAINS syntax
                        searchConditions += $" {emptyOrAnd} \"{word}*\" ";
                        emptyOrAnd = " AND ";
                    }
                }
                //full text catalogue should be created manually

                var source =
                    _context.PDFPages.AsNoTracking()
                    .Where(p =>
                           (bookId == 0 || p.PDFBookId == bookId)
                           &&
                           EF.Functions.Contains(p.PageText, searchConditions)
                           ).OrderBy(i => i.PageNumber);


                (PaginationMetadata PagingMeta, PDFPage[] Items) paginatedResult =
                   await QueryablePaginator<PDFPage>.Paginate(source, paging);

                // PDFPage.PDFBook is [NotMapped] - not a real EF relationship, so there's
                // nothing to Include() here. The original code instead ran one extra query per
                // page, sequentially, to fill it in - a classic N+1: a page of 20-50 results
                // meant 20-50 extra round-trips, and since every page normally shares the same
                // PDFBookId (searching within one book is the common case, bookId != 0), most
                // of those were re-fetching the exact same book row over and over. Replaced with
                // exactly one follow-up query for every *distinct* book actually referenced in
                // this page of results (usually just one), then an in-memory lookup to attach
                // each page to its book - same projection (BookText excluded) as the other two
                // search methods, for the same reason.
                var distinctBookIds = paginatedResult.Items.Select(p => p.PDFBookId).Distinct().ToList();
                var booksById = await _context.PDFBooks.AsNoTracking()
                    .Where(b => distinctBookIds.Contains(b.Id))
                    .Select(b => new PDFBook
                    {
                        Id = b.Id,
                        BookId = b.BookId,
                        Status = b.Status,
                        Title = b.Title,
                        SubTitle = b.SubTitle,
                        AuthorsLine = b.AuthorsLine,
                        ISBN = b.ISBN,
                        Description = b.Description,
                        Language = b.Language,
                        IsTranslation = b.IsTranslation,
                        TranslatorsLine = b.TranslatorsLine,
                        TitleInOriginalLanguage = b.TitleInOriginalLanguage,
                        PublisherLine = b.PublisherLine,
                        PublishingDate = b.PublishingDate,
                        PublishingLocation = b.PublishingLocation,
                        PublishingNumber = b.PublishingNumber,
                        ClaimedPageCount = b.ClaimedPageCount,
                        MultiVolumePDFCollectionId = b.MultiVolumePDFCollectionId,
                        VolumeOrder = b.VolumeOrder,
                        DateTime = b.DateTime,
                        LastModified = b.LastModified,
                        ExternalPDFFileUrl = b.ExternalPDFFileUrl,
                        CoverImageId = b.CoverImageId,
                        ExtenalCoverImageUrl = b.ExtenalCoverImageUrl,
                        OriginalSourceName = b.OriginalSourceName,
                        OriginalSourceUrl = b.OriginalSourceUrl,
                        OriginalFileUrl = b.OriginalFileUrl,
                        PageCount = b.PageCount,
                        FileMD5CheckSum = b.FileMD5CheckSum,
                        OriginalFileName = b.OriginalFileName,
                        StorageFolderName = b.StorageFolderName,
                        BookScriptType = b.BookScriptType,
                        PDFSourceId = b.PDFSourceId,
                        OCRed = b.OCRed,
                        OCRTime = b.OCRTime,
                        AIRevised = b.AIRevised,
                        // BookText intentionally omitted, same as the other two search methods
                    })
                    .ToDictionaryAsync(b => b.Id);

                foreach (PDFPage page in paginatedResult.Items)
                {
                    page.PDFBook = booksById.TryGetValue(page.PDFBookId, out var book) ? book : null;
                }
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFPage[] Items)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFPage[] Items)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// fill missing book texts
        /// </summary>
        public void StartFillingMissingBookTextsAsync()
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
                                   (
                                       async token =>
                                       {
                                           using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                           {
                                               LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                               var job = (await jobProgressServiceEF.NewJob("StartFillingMissingBookTextsAsync", "Query data")).Result;

                                               try
                                               {
                                                   var books = await context.PDFBooks.Where(b => b.OCRed == true && string.IsNullOrEmpty(b.BookText)).ToListAsync();
                                                   foreach (var book in books)
                                                   {
                                                       var pages = await context.PDFPages.AsNoTracking().Where(p => p.PDFBookId == book.Id).OrderBy(p => p.PageText).ToArrayAsync();
                                                       string bookText = "";
                                                       foreach (var page in pages)
                                                       {
                                                           if (!string.IsNullOrEmpty(page.PageText))
                                                           {
                                                               bookText += page.PageText;
                                                               bookText += Environment.NewLine;
                                                           }
                                                       }
                                                       if (!string.IsNullOrEmpty(bookText))
                                                       {
                                                           book.BookText = bookText;
                                                           context.Update(book);
                                                           await context.SaveChangesAsync();
                                                       }


                                                   }

                                                   await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                               }
                                               catch (Exception exp)
                                               {
                                                   await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                               }

                                           }
                                       }
                                   );
        }


        /// <summary>
        /// get user last activity
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PDFVisistViewModel[]>> GetUserLastActivityAsync(Guid userId)
        {
            try
            {
                // derived from PDFStudyLogEntry (see that model's doc comment) rather than
                // PDFVisitRecord - the study log is the single, synced, user-manageable source
                // of "what was I last reading" now, shared with the Flutter client's own
                // history/resume feature. PDFVisitRecord itself is untouched and kept for its
                // own separate purpose (general API-hit/analytics logging, search terms,
                // anonymous visits) - it was never really the same concern as this method, it
                // just used to be the only data available to derive this from.
                var maxByBook = _context.PDFStudyLogEntries
                    .Where(e => e.RAppUserId == userId && !e.IsDeleted)
                    .GroupBy(e => e.PDFBookId)
                    .Select(g => new { BookId = g.Key, LastReadAt = g.Max(e => e.Timestamp) });

                var entries = _context.PDFStudyLogEntries.Where(e => e.RAppUserId == userId && !e.IsDeleted);

                var joined = await (
                    from m in maxByBook
                    join e in entries
                        on new { m.BookId, m.LastReadAt } equals new { BookId = e.PDFBookId, LastReadAt = e.Timestamp }
                    select new { m.BookId, m.LastReadAt, e.PageNumber })
                    .ToListAsync();

                // same rare-tie guard as PDFUserSyncService.GetReadingPositionsAsync
                var positions = joined
                    .GroupBy(p => p.BookId)
                    .Select(g => g.First())
                    .OrderByDescending(p => p.LastReadAt)
                    .Take(200)
                    .ToList();

                List<PDFVisistViewModel> visits = new List<PDFVisistViewModel>();

                foreach (var position in positions)
                {
                    var pdf = await _context.PDFBooks.AsNoTracking().Where(p => p.Id == position.BookId).FirstOrDefaultAsync();
                    if (pdf == null)
                        continue;

                    var page = await _context.PDFPages.AsNoTracking().Where(p => p.PDFBookId == position.BookId && p.PageNumber == position.PageNumber).FirstOrDefaultAsync();

                    visits.Add
                        (
                        new PDFVisistViewModel()
                        {
                            DateTime = position.LastReadAt,
                            PDFBookId = position.BookId,
                            PageNumber = position.PageNumber,
                            BookTitle = pdf.Title,
                            ExternalImageUrl = page != null ? page.ExtenalThumbnailImageUrl : pdf.ExtenalCoverImageUrl
                        }
                        );
                }

                return new RServiceResult<PDFVisistViewModel[]>(visits.ToArray());

            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFVisistViewModel[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get pdf bool table of contents
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<RTitleInContents[]>> GetPDFBookTableOfContentsAsync(int id)
        {
            try
            {
                var redirect = await _context.PDFBookRedirects.AsNoTracking().Where(r => r.OldPDFBookId == id).SingleOrDefaultAsync();
                if (redirect != null)
                {
                    id = redirect.SurvivorPDFBookId;
                }

                List<RTitleInContents> contents = new List<RTitleInContents>();

                var pages = await _context.PDFPages.AsNoTracking().Include(p => p.Tags).ThenInclude(p => p.RTag).Where(p => p.PDFBookId == id).ToListAsync();
                int orderOfContents = 0;
                foreach (var page in pages)
                {
                    foreach (var value in page.Tags.OrderBy(t => t.Id))
                    {
                        if (value.RTag.TagType == RTagType.TitleInContents)
                        {
                            if (!int.TryParse(value.ValueSupplement, out int level))
                            {
                                level = 1;
                            }
                            contents.Add
                            (
                            new RTitleInContents()
                            {
                                Title = value.Value,
                                Order = ++orderOfContents,
                                Level = level,
                                ItemFriendlyUrl = $"{id}/{page.PageNumber}"
                            }
                            );
                        }
                    }
                }

                return new RServiceResult<RTitleInContents[]>(contents.ToArray());
            }
            catch (Exception exp)
            {
                return new RServiceResult<RTitleInContents[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// put first verse to ganjoor toc titles
        /// </summary>
        public void StartCompletingGanjoorTOCVersesAsync()
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
                                   (
                                       async token =>
                                       {
                                           using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                           {
                                               LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                               var job = (await jobProgressServiceEF.NewJob("StartCompletingGanjoorTOCVersesAsync", "Query data")).Result;

                                               try
                                               {
                                                   var values = await context.TagValues.Include(t => t.RTag).Where(t => t.Value == t.ValueInEnglish && t.RTag.TagType == RTagType.TitleInContents).ToListAsync();
                                                   int i = 0;
                                                   int count = values.Count;
                                                   foreach (var value in values)
                                                   {
                                                       if(i % 1000 == 0)
                                                       {
                                                           await jobProgressServiceEF.UpdateJob(job.Id, i, $"{i} از {count}");
                                                       }
                                                       var linkTag = await context.TagValues.Include(t => t.RTag).AsNoTracking().Where(t => t.RTag.NameInEnglish == "Ganjoor Link" && t.Value == value.Value).FirstOrDefaultAsync();
                                                       if(linkTag != null)
                                                       {
                                                           var link = linkTag.ValueSupplement;
                                                           if(link.StartsWith("https://ganjoor.net"))
                                                           {
                                                               string firstVerse = await GetGanjoorPageFirstVerseAsync(link);
                                                               if (!string.IsNullOrEmpty(firstVerse))
                                                               {
                                                                   if (firstVerse.Length > 100)
                                                                   {
                                                                       firstVerse = firstVerse.Substring(0, 50);
                                                                       int n = firstVerse.LastIndexOf(' ');
                                                                       if (n >= 0)
                                                                       {
                                                                           firstVerse = firstVerse.Substring(0, n) + " ...";
                                                                       }
                                                                       else
                                                                       {
                                                                           firstVerse += "...";
                                                                       }
                                                                   }
                                                                   if(!value.Value.Contains(firstVerse))
                                                                   {
                                                                       value.Value = $"{value.Value}: {firstVerse}";
                                                                       context.Update(value);
                                                                   }
                                                               }

                                                           }
                                                           i++;
                                                       }
                                                   }

                                                   await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                               }
                                               catch (Exception exp)
                                               {
                                                   await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                               }

                                           }
                                       }
                                   );
        }

        /// <summary>
        /// Database Context
        /// </summary>
        protected readonly RMuseumDbContext _context;
        /// <summary>
        /// Background Task Queue Instance
        /// </summary>
        protected readonly IBackgroundTaskQueue _backgroundTaskQueue;
        /// <summary>
        /// image file service
        /// </summary>
        protected readonly IImageFileService _imageFileService;
        /// <summary>
        /// ftp service
        /// </summary>
        protected readonly IQueuedFTPUploadService _ftpService;

        /// <summary>
        /// options service
        /// </summary>

        protected readonly IRGenericOptionsService _optionsService;

        /// <summary>
        /// Configuration
        /// </summary>
        protected IConfiguration Configuration { get; }


        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="backgroundTaskQueue"></param>
        /// <param name="imageFileService"></param>
        /// <param name="configuration"></param>
        /// <param name="ftpService"></param>
        /// <param name="optionsService"></param>
        public PDFLibraryService(RMuseumDbContext context, IBackgroundTaskQueue backgroundTaskQueue, IImageFileService imageFileService, IConfiguration configuration, IQueuedFTPUploadService ftpService, IRGenericOptionsService optionsService)
        {
            _context = context;
            _backgroundTaskQueue = backgroundTaskQueue;
            _imageFileService = imageFileService;
            Configuration = configuration;
            _ftpService = ftpService;
            _optionsService = optionsService;
        }
    }
}
