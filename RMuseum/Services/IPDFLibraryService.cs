using Microsoft.AspNetCore.Http;
using RMuseum.Models.Artifact;
using RMuseum.Models.Artifact.ViewModels;
using RMuseum.Models.GanjoorIntegration;
using RMuseum.Models.GanjoorIntegration.ViewModels;
using RMuseum.Models.ImportJob;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RMuseum.Models.PDFUserTracking;
using RMuseum.Models.PDFUserTracking.ViewModels;
using RSecurityBackend.Models.Generic;
using System;
using System.Threading.Tasks;

namespace RMuseum.Services
{
    /// <summary>
    /// PDF Library Services
    /// </summary>
    public interface IPDFLibraryService
    {
        /// <summary>
        /// import from known sources
        /// </summary>
        /// <param name="srcUrl"></param>
        /// <returns></returns>
        Task<RServiceResult<int>> StartImportingKnownSourceAsync(string srcUrl);

        /// <summary>
        /// import from known source
        /// </summary>
        /// <param name="srcUrl"></param>
        /// <param name="finalizeDownload"></param>
        /// <returns></returns>
        Task<RServiceResult<int>> ImportfFromKnownSourceAsync(string srcUrl, bool finalizeDownload);

        /// <summary>
        /// get pdf book by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="statusArray"></param>
        /// <param name="includePages"></param>
        /// <param name="includeBookText"></param>
        /// <param name="includePageText"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFBook>> GetPDFBookByIdAsync(int id, PublishStatus[] statusArray, bool includePages, bool includeBookText, bool includePageText);

        /// <summary>
        /// get all pdfbooks (including CoverImage info but not pages or tagibutes info)
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="statusArray"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> GetAllPDFBooksAsync(PagingParameterModel paging, PublishStatus[] statusArray);

        /// <summary>
        /// an incomplete prototype for removing PDF books
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> RemovePDFBookAsync(int pdfBookId);

        /// <summary>
        /// add pdf book tag value
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="rTag"></param>
        /// <returns></returns>
        Task<RServiceResult<RTagValue>> TagPDFBookAsync(int pdfBookId, RTag rTag);

        /// <summary>
        /// remove pdf book tag value
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="tagValueId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> UnTagPDFBookAsync(int pdfBookId, Guid tagValueId);

        /// <summary>
        /// edit pdf book tag value
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="edited"></param>
        /// <param name="global">apply on all same value tags</param>
        /// <returns></returns>
        Task<RServiceResult<RTagValue>> EditPDFBookTagValueAsync(int pdfBookId, RTagValue edited, bool global);

        /// <summary>
        /// get tagged publish pdfbooks (including CoverImage info but not pages or tagibutes info) 
        /// </summary>
        /// <param name="tagUrl"></param>
        /// <param name="valueUrl"></param>
        /// <param name="statusArray"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFBook[]>> GetPDFBookByTagValueAsync(string tagUrl, string valueUrl, PublishStatus[] statusArray);

        /// <summary>
        /// add author
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        Task<RServiceResult<Author>> AddAuthorAsync(Author author);

        /// <summary>
        /// get author by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<Author>> GetAuthorByIdAsync(int id);

        /// <summary>
        /// get authors
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorName"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, Author[] Authors)>> GetAuthorsAsync(PagingParameterModel paging, string authorName);

        /// <summary>
        /// list authors (optionally filtered to a single AuthorRole.Role and/or by part of their
        /// name), each with a computed count of distinct books they're credited on, sortable by
        /// name (ascending) or by that book count (descending), paginated.
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="role">exact AuthorRole.Role to filter to; null/empty for all roles combined</param>
        /// <param name="sortByBookCountDesc">true: sort by book count descending; false: sort by name ascending</param>
        /// <param name="authorName">part of the author's name; null/empty for no name filter</param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, AuthorWithBookCount[] Authors)>> GetAuthorsWithBookCountAsync(PagingParameterModel paging, string role, bool sortByBookCountDesc, string authorName = null);

        /// <summary>
        /// update author
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<Author>> UpdateAuthorAsync(Author model);

        /// <summary>
        /// delete author by id - removes every AuthorRole/PDFPinnedAuthor referencing this
        /// author first. Deliberately does not touch any PDFBook's AuthorsLine/TranslatorsLine -
        /// a generic/placeholder credit (e.g. "جمعی از نویسندگان") is worth removing as a
        /// browsable/searchable author, but its exact wording is still worth keeping as plain
        /// text on whichever books already carry it.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeleteAuthorAsync(int id);

        /// <summary>
        /// submit a report against a book - see PDFLibraryService-Report.cs's own doc comments
        /// on this and the two methods below
        /// </summary>
        /// <param name="reporterId"></param>
        /// <param name="pdfBookId"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<Guid>> SubmitPDFBookReportAsync(Guid reporterId, int pdfBookId, PDFBookReportSubmitViewModel model);

        /// <summary>
        /// paginated list of still-open book reports, for reviewers
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBookReportViewModel[] Items)>> GetOpenPDFBookReportsAsync(PagingParameterModel paging);

        /// <summary>
        /// respond to and close a book report
        /// </summary>
        /// <param name="reviewerId"></param>
        /// <param name="reportId"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> ClosePDFBookReportAsync(Guid reviewerId, Guid reportId, string response);

        /// <summary>
        /// submit a page comment or reply - see PDFLibraryService-Comment.cs's own doc
        /// comments on this and the two methods below
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="pdfPageId"></param>
        /// <param name="model"></param>
        /// <param name="image">Phase 2: optional highlighted-region snippet - required together with model's HighlightX/Y/Width/Height, omitted for a plain page-level comment</param>
        /// <returns></returns>
        Task<RServiceResult<Guid>> SubmitPDFPageCommentAsync(Guid userId, int pdfPageId, PDFPageCommentPostViewModel model, IFormFile image);

        /// <summary>
        /// every published comment on a page, flat
        /// </summary>
        /// <param name="pdfPageId"></param>
        /// <param name="requestingUserId">optional - only affects MyComment</param>
        /// <returns></returns>
        Task<RServiceResult<PDFPageCommentViewModel[]>> GetPDFPageCommentsAsync(int pdfPageId, Guid? requestingUserId);

        /// <summary>
        /// count of published comments on a single page - keyed by (pdfBookId, pageNumber),
        /// see GetPDFPageCommentCountAsync's own doc comment on why
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        Task<RServiceResult<int>> GetPDFPageCommentCountAsync(int pdfBookId, int pageNumber);

        /// <summary>
        /// every published comment across every page of a book, paginated, newest first - the
        /// book-wide comment hub
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="paging"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFPageCommentViewModel[] Items)>> GetPDFBookCommentsAsync(int pdfBookId, PagingParameterModel paging);

        /// <summary>
        /// delete a page comment - own author always, others need pdfcomment:moderate
        /// </summary>
        /// <param name="requestingUserId"></param>
        /// <param name="commentId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeletePDFPageCommentAsync(Guid requestingUserId, Guid commentId);

        /// <summary>
        /// add pdf book contributer
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> AddPDFBookContributerAsync(int pdfBookId, int authorId, string role);

        /// <summary>
        /// remove contribution from pdf book
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="contributionId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeletePDFBookContributerAsync(int pdfBookId, int contributionId);

        /// <summary>
        /// get published pdf books by author
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> GetPublishedPDFBooksByAuthorAsync(PagingParameterModel paging, int authorId, string role);

        /// <summary>
        /// get published pdf books by author stats (group by role)
        /// </summary>
        /// <param name="authorId"></param>
        /// <returns></returns>

        Task<RServiceResult<AuthorRoleCount[]>> GetPublishedPDFBookbyAuthorGroupedByRoleAsync(int authorId);

        /// <summary>
        /// get all books
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, Book[] Books)>> GetAllBooksAsync(PagingParameterModel paging);

        /// <summary>
        /// add book
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        Task<RServiceResult<Book>> AddBookAsync(Book book);

        /// <summary>
        /// update book
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<Book>> UpdateBookAsync(Book model);

        /// <summary>
        /// delete book
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeleteBookAsync(int id);

        /// <summary>
        /// add book author
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> AddBookAuthorAsync(int bookId, int authorId, string role);

        /// <summary>
        /// remove author from book
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="contributionId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeleteBookAuthorAsync(int bookId, int contributionId);

        /// <summary>
        /// book by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<Book>> GetBookByIdAsync(int id);

        /// <summary>
        /// get books by author
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="authorId"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, Book[] Books)>> GetBooksByAuthorAsync(PagingParameterModel paging, int authorId, string role);

        /// <summary>
        /// get books by author stats (group by role)
        /// </summary>
        /// <param name="authorId"></param>
        /// <returns></returns>

        Task<RServiceResult<AuthorRoleCount[]>> GetBookbyAuthorGroupedByRoleAsync(int authorId);

        /// <summary>
        /// get book related pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="bookId"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> GetBookRelatedPDFBooksAsync(PagingParameterModel paging, int bookId);

        /// <summary>
        /// add multi volume pdf collection
        /// </summary>
        /// <param name="multiVolumePDFCollection"></param>
        /// <returns></returns>
        Task<RServiceResult<MultiVolumePDFCollection>> AddMultiVolumePDFCollectionAsync(MultiVolumePDFCollection multiVolumePDFCollection);

        /// <summary>
        /// update multi volume pdf collection
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<MultiVolumePDFCollection>> UpdateMultiVolumePDFCollectionAsync(MultiVolumePDFCollection model);

        /// <summary>
        /// delete multi volume pdf collection
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeleteMultiVolumePDFCollectionAsync(int id);


        /// <summary>
        /// start importing local pdf file
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> StartImportingLocalPDFAsync(NewPDFBookViewModel model);

        /// <summary>
        /// edit pdf book master record
        /// </summary>
        /// <param name="model"></param>
        /// <param name="canChangeStatusToAwaiting"></param>
        /// <param name="canPublish"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFBook>> EditPDFBookMasterRecordAsync(PDFBook model, bool canChangeStatusToAwaiting, bool canPublish);

        /// <summary>
        /// Copy PDF Book Cover Image From Page Thumbnail image
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="pdfpageId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> SetPDFBookCoverImageFromPageAsync(int pdfBookId, int pdfpageId);

        /// <summary>
        /// get volumes pdf books
        /// </summary>
        /// <param name="volumeId"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFBook[]>> GetVolumesPDFBooks(int volumeId);

        /// <summary>
        /// get volumes by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<MultiVolumePDFCollection>> GetMultiVolumePDFCollectionByIdAsync(int id);

        /// <summary>
        /// get pdf source by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFSource>> GetPDFSourceByIdAsync(int id);

        /// <summary>
        /// Get All PDF Sources
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<PDFSource[]>> GetPDFSourcesAsync();

        /// <summary>
        /// Add PDF Source
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFSource>> AddPDFSourceAsync(PDFSource source);

        /// <summary>
        /// update PDF Source
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFSource>> UpdatePDFSourceAsync(PDFSource model);

        /// <summary>
        /// delete PDF Source
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeletePDFSourceAsync(int id);

        /// <summary>
        /// get source pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="sourceId"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> GetSourceRelatedPDFBooksAsync(PagingParameterModel paging, int sourceId);

        /// <summary>
        /// batch import soha library
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="finalizeDownload"></param>
        void BatchImportSohaLibraryAsync(int start, int end, bool finalizeDownload);

        /// <summary>
        /// batch import eliteraturebook.com library
        /// </summary>
        /// <param name="ajaxPageIndexStart">from 0</param>
        /// <param name="ajaxPageIndexEnd"></param>
        /// <param name="finalizeDownload"></param>
        void BatchImportELiteratureBookLibraryAsync(int ajaxPageIndexStart, int ajaxPageIndexEnd, bool finalizeDownload);

        /// <summary>
        /// import jobs
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, ImportJob[] Jobs)>> GetImportJobs(PagingParameterModel paging);

        /// <summary>
        /// search pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Items)>> SearchPDFBooksAsync(PagingParameterModel paging, string term);

        /// <summary>
        /// check to see if book is related to poem
        /// </summary>
        /// <param name="bookId"></param>
        /// <param name="poemId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> IsBookRelatedToPoemAsync(int bookId, int poemId);

        /// <summary>
        /// suggest ganjoor link
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="link"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> SuggestGanjoorLinkAsync(Guid userId, PDFGanjoorLinkSuggestion link);

        /// <summary>
        /// finds what the method name suggests
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="onlyMachineSuggested"></param>
        /// <returns></returns>
        Task<RServiceResult<GanjoorLinkViewModel>> GetNextUnreviewedGanjoorLinkAsync(int skip, bool onlyMachineSuggested);

        /// <summary>
        /// get unreviewed image count
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<int>> GetUnreviewedGanjoorLinksCountAsync();

        /// <summary>
        /// Review Suggested Link
        /// </summary>
        /// <param name="linkId"></param>
        /// <param name="userId"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> ReviewSuggestedLinkAsync(Guid linkId, Guid userId, ReviewResult result);

        /// <summary>
        /// get unsynced approved pdf ganjoor links
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<PDFGanjoorLink[]>> GetUnsyncedPDFGanjoorLinksAsync();

        /// <summary>
        /// synchronize ganjoor link
        /// </summary>
        /// <param name="linkId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> SynchronizePDFGanjoorLinkAsync(Guid linkId);

        /// <summary>
        /// get next un-ocred PDF Book
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<PDFBook>> GetNextUnOCRedPDFBookAsync();

        /// <summary>
        /// reset OCR Queue (remove queued items)
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<bool>> ResetOCRQueueAsync();

        /// <summary>
        /// set pdf page ocr info (and if a book whole pages are ocred the book ocred flag is set to true)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> SetPDFPageOCRInfoAsync(PDFPageOCRDataViewModel model);

        /// <summary>
        /// get next un-aied PDF Book and add it to a queue
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<PDFBook>> GetNextUnAIedPDFBookAsync();

        /// <summary>
        /// reset AI Queue (remove queued items)
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<bool>> ResetAIQueueAsync();

        /// <summary>
        /// search pdf books pages for a text
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBook[] Books)>> SearchPDFBookForPDFPagesTextAsync(PagingParameterModel paging, string term);

        /// <summary>
        /// search pdf pages
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="bookId">0 for all pdf books</param>
        /// <param name="term"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFPage[] Items)>> SearchPDFPagesTextAsync(PagingParameterModel paging, int bookId, string term);

        /// <summary>
        /// get page by page number
        /// </summary>
        /// <param name="pdfBookId"></param>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFPage>> GetPDFPageAsync(int pdfBookId, int pageNumber);

        /// <summary>
        /// fill missing book texts
        /// </summary>
        void StartFillingMissingBookTextsAsync();

        /// <summary>
        /// queued downloding pdf books
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, QueuedPDFBook[] Books)>> GetQueuedPDFBooksAsync(PagingParameterModel paging);

        /// <summary>
        /// delete queued books
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeleteQueuedPDFBookAsync(Guid id);

        /// <summary>
        /// mix queued pdf books 
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> MixQueuedPDFBooksAsync(int step);

        /// <summary>
        /// start processing queue pdf books
        /// </summary>
        /// <param name="count"></param>
        void StartProcessingQueuedPDFBooks(int count);


        /// <summary>
        /// get user last activity
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        Task<RServiceResult<PDFVisistViewModel[]>> GetUserLastActivityAsync(Guid userId);


        /// <summary>
        /// queue ganjoor poem match finding
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> QueueGanjoorPoemMatchAsync(GanjoorPoemMatchViewModel model);

        /// <summary>
        /// ganjoor poem match finding queue
        /// </summary>
        /// <param name="notStarted"></param>
        /// <param name="notFinished"></param>
        /// <returns></returns>
        Task<RServiceResult<GanjoorPoemMatchFinding[]>> GetGanjoorPoemMatchQueueAsync(bool notStarted = false, bool notFinished = true);

        /// <summary>
        /// update a ganjoor poem match finding
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> UpdateGanjoorPoemMatchFindingAsync(GanjoorPoemMatchFinding model);

        /// <summary>
        /// pdf book table of contents
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<RTitleInContents[]>> GetPDFBookTableOfContentsAsync(int id);

        /// <summary>
        /// put first verse to ganjoor toc titles
        /// </summary>
        void StartCompletingGanjoorTOCVersesAsync();

        /// <summary>
        /// start scanning PDFBooks for possible duplicates and queue the findings for human review
        /// </summary>
        /// <param name="forceRestart">
        /// if true, ignore any interrupted/in-progress run and restart the title-comparison pass
        /// from scratch instead of resuming. Use after manually clearing
        /// PDFBookDuplicateCandidates.
        /// </param>
        void StartDetectingDuplicatePDFBooksAsync(bool forceRestart = false);

        /// <summary>
        /// current duplicate-detection progress/resume state (title-fuzzy-matching pass)
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<PDFBookDuplicateDetectionState>> GetPDFBookDuplicateDetectionStateAsync();

        /// <summary>
        /// paginated list of duplicate candidates awaiting/undergone review
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="statusArray"></param>
        /// <returns></returns>
        Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBookDuplicateCandidate[] Items)>> GetPDFBookDuplicateCandidatesAsync(PagingParameterModel paging, PDFBookDuplicateCandidateStatus[] statusArray);

        /// <summary>
        /// update a duplicate candidate's review decision (survivor choice / status / note)
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> UpdatePDFBookDuplicateCandidateAsync(PDFBookDuplicateCandidate model);

        /// <summary>
        /// execute a Confirmed duplicate-candidate merge: fills metadata gaps, repoints
        /// references, creates a redirect from the merged-away duplicate's id to the survivor,
        /// and removes the duplicate's PDFBook row (queuing its storage for cleanup).
        /// </summary>
        /// <param name="candidateId"></param>
        /// <param name="reviewerUserId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> MergePDFBookDuplicateAsync(Guid candidateId, Guid reviewerUserId);

        /// <summary>
        /// manually merge two PDFBooks by id directly, without needing a pre-existing duplicate-
        /// candidate row - for an operator who spots a duplicate directly (e.g. while browsing)
        /// rather than through the automated detection queue.
        /// </summary>
        /// <param name="survivorPDFBookId">the PDFBook id that stays and receives the merged data</param>
        /// <param name="duplicatePDFBookId">the PDFBook id that gets merged away and removed</param>
        /// <param name="reviewerUserId"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> MergePDFBooksByIdAsync(int survivorPDFBookId, int duplicatePDFBookId, Guid reviewerUserId);

        /// <summary>
        /// merge two Author records by id - repoints/dedupes every PDFBook contribution and pin
        /// referencing the duplicate onto the survivor, fixes up affected books' free-text
        /// AuthorsLine/TranslatorsLine, then removes the duplicate. No redirect is created,
        /// unlike PDFBook merge - nothing external references an author by a stable id/url the
        /// way a book's PDF file does.
        /// </summary>
        /// <param name="survivorAuthorId">the Author id that stays</param>
        /// <param name="duplicateAuthorId">the Author id that gets merged away and removed</param>
        /// <returns></returns>
        Task<RServiceResult<bool>> MergeAuthorsByIdAsync(int survivorAuthorId, int duplicateAuthorId);


        /// <summary>
        /// start merging EVERY Confirmed duplicate candidate in a single background job, instead
        /// of one at a time.
        /// </summary>
        void StartMergingConfirmedPDFBookDuplicatesAsync();

        /// <summary>
        /// delete a duplicate candidate row (e.g. a false positive)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeletePDFBookDuplicateCandidateAsync(Guid id);

        /// <summary>
        /// start physically cleaning up storage folders (FTP + local disk) queued by
        /// RemovePDFBookAsync. Safe to call repeatedly / after an interruption.
        /// </summary>
        void StartCleaningUpPendingPDFStorageAsync();
    }
}
