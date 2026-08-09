using DNTPersianUtils.Core;
using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Artifact;
using RMuseum.Models.PDFLibrary;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        // ----------------------------------------------------------------------------------
        // tunable parameters for the duplicate-detection algorithm.
        // if new sample cases show up that this misses (or wrongly flags), these are the
        // first knobs to look at before touching the scoring logic itself.
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// minimum normalized-title length to be considered for fuzzy comparison (very short
        /// titles produce unreliable similarity scores)
        /// </summary>
        private const int DupMinTitleLengthForFuzzyMatch = 4;

        /// <summary>
        /// how many leading characters of the normalized (space-stripped) title are used to
        /// bucket books together before doing the O(n^2) pairwise comparison. Lowering this
        /// finds more candidates but is slower; raising it is faster but can miss matches
        /// whose titles diverge early (e.g. one has a subtitle prefix the other doesn't)
        /// </summary>
        private const int DupTitleBucketKeyLength = 6;

        /// <summary>
        /// minimum overall score (0-100) for a title-similarity based match to be queued for review
        /// </summary>
        private const int DupMinCandidateScore = 65;

        /// <summary>
        /// the largest page-count difference tolerated between two candidates before they're
        /// disqualified outright, expressed as an absolute page count floor...
        /// </summary>
        private const int DupMaxPageCountDiffAbsolute = 5;

        /// <summary>
        /// ...and as a fraction of the larger book's page count - whichever allowance is larger
        /// wins, so short pamphlets aren't unfairly strict (5 pages is a lot for a 20-page book but
        /// the absolute floor still lets small scanning variance through) while long books get a
        /// proportionally generous allowance (5% of 900 pages is still a meaningful cross-check).
        /// Real duplicates (different scans of the same book) can differ slightly - a missing
        /// blank/cover page, an appendix included or not - but not by a large margin.
        /// </summary>
        private const double DupMaxPageCountDiffRatio = 0.05;

        /// <summary>
        /// the largest PDF file-size difference (in bytes) tolerated between two candidates before
        /// they're disqualified, expressed as an absolute floor (covers an added cover-page image,
        /// different PDF compression settings, etc. between two scans of the same book)...
        /// </summary>
        private const long DupMaxFileSizeDiffAbsoluteBytes = 2 * 1024 * 1024; // 2 MB

        /// <summary>
        /// ...and as a fraction of the larger file's size - whichever allowance is larger wins,
        /// same reasoning as the page-count tolerance above. Only compared when both PDFBooks have
        /// a locally-stored PDFFile (externally-hosted-only PDFs have no FileSizeInBytes to compare).
        /// </summary>
        private const double DupMaxFileSizeDiffRatio = 0.15;

        /// <summary>
        /// minimum normalized Levenshtein title similarity (0..1) even to be scored at all
        /// </summary>
        private const double DupMinTitleSimilarity = 0.55;

        /// <summary>
        /// how many title buckets to process between DB checkpoints. Lower = safer against
        /// abrupt interruption (process kill / app-pool recycle) but more DB round trips;
        /// higher = faster but risks redoing more work after an interruption.
        /// </summary>
        private const int DupCheckpointEveryNBuckets = 50;

        /// <summary>
        /// minimum fraction of shared words (relative to the shorter title's word count) required
        /// before the "leftover words" dissimilarity check (below) kicks in at all - guards against
        /// triggering on titles that only coincidentally share one or two common words
        /// </summary>
        private const double DupLeftoverWordsShareRatioMin = 0.5;

        /// <summary>
        /// after removing the words two titles have in common, if what's left on both sides is
        /// itself this dissimilar (0..1 normalized Levenshtein), the pair is treated as different
        /// volumes/issues/subtitles of the same series rather than a duplicate, and is skipped
        /// </summary>
        private const double DupLeftoverWordsMaxSimilarity = 0.55;

        /// <summary>
        /// Persian ordinal words recognized as volume/issue/part markers (جلد دوم, شماره سوم, ...).
        /// Character-level similarity is unreliable for these because they are short (e.g. "دوم"
        /// vs "سوم" is still ~67% similar letter-for-letter despite being different numbers), so
        /// they get their own exact-set-membership check instead of relying on Levenshtein.
        /// </summary>
        private static readonly HashSet<string> _dupOrdinalWords = new HashSet<string>(
            new[] { "اول", "یکم", "دوم", "سوم", "چهارم", "پنجم", "ششم", "هفتم", "هشتم", "نهم", "نخست" }
            .Concat(new[] { "دهم", "یازدهم", "دوازدهم", "سیزدهم", "چهاردهم", "پانزدهم", "شانزدهم", "هفدهم", "هجدهم", "نوزدهم" })
            .Concat(new[] { "بیستم", "سی ام", "سیام", "چهلم", "پنجاهم", "شصتم", "هفتادم", "هشتادم", "نودم", "صدم" })
            .Concat(new[] { "نخست", "دوم", "سوم", "چهارم", "پنجم", "ششم", "هفتم", "هشتم", "نهم", "دهم" }.Select(w => w + "ین"))
        );

        private static readonly HashSet<string> _dupOrdinalTensBase = new HashSet<string>
        {
            "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"
        };

        /// <summary>
        /// start the duplicate PDFBook detection background job
        /// </summary>
        /// <param name="forceRestart">
        /// if true, ignore any interrupted/in-progress run and start the title-comparison pass
        /// completely from scratch (the resume checkpoint is reset). Use this after manually
        /// clearing PDFBookDuplicateCandidates so old candidates don't block re-detection of the
        /// same pairs. If false (default) and a previous run was interrupted, it resumes from
        /// where it left off instead of rescanning everything.
        /// </param>
        public void StartDetectingDuplicatePDFBooksAsync(bool forceRestart = false)
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
                                   (
                                       async token =>
                                       {
                                           using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                           {
                                               LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                               var job = (await jobProgressServiceEF.NewJob("StartDetectingDuplicatePDFBooksAsync", "Query data")).Result;

                                               try
                                               {
                                                   await _DetectDuplicatePDFBooksAsync(context, jobProgressServiceEF, job.Id, forceRestart);

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
        /// lightweight projection used while scanning for duplicates (avoids loading full PDFBook
        /// rows - Pages, BookText, etc. - into memory for the whole table)
        /// </summary>
        private class _PDFBookDupInfo
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string NormalizedTitle { get; set; }
            public string[] TitleTokens { get; set; }
            public HashSet<string> DigitRuns { get; set; }
            public HashSet<string> OrdinalWords { get; set; }
            public string BucketKey { get; set; }
            public string AuthorsLine { get; set; }
            public string ISBN { get; set; }
            public int PageCount { get; set; }
            public long? FileSizeInBytes { get; set; }
            public int? ClaimedPageCount { get; set; }
            public int? PDFSourceId { get; set; }
            public int? MultiVolumePDFCollectionId { get; set; }
            public string FileMD5CheckSum { get; set; }
        }

        /// <summary>
        /// actual detection logic, isolated so it can be unit-tested/tuned independently of the
        /// background-job plumbing around it
        /// </summary>
        private async Task _DetectDuplicatePDFBooksAsync(RMuseumDbContext context, LongRunningJobProgressServiceEF jobProgressServiceEF, Guid jobId, bool forceRestart)
        {
            var rawBooks =
                await context.PDFBooks.AsNoTracking()
                .Where(b => b.Status != PublishStatus.Deleted)
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.AuthorsLine,
                    b.ISBN,
                    b.PageCount,
                    FileSizeInBytes = b.PDFFile == null ? (long?)null : b.PDFFile.FileSizeInBytes,
                    b.ClaimedPageCount,
                    b.PDFSourceId,
                    b.MultiVolumePDFCollectionId,
                    b.FileMD5CheckSum
                })
                .ToArrayAsync();

            var books = new List<_PDFBookDupInfo>(rawBooks.Length);
            foreach (var b in rawBooks)
            {
                var normalizedTitle = _NormalizeTitleForDupComparison(b.Title);
                var titleTokens = normalizedTitle.Length == 0 ? Array.Empty<string>() : normalizedTitle.Split(' ');
                books.Add(new _PDFBookDupInfo()
                {
                    Id = b.Id,
                    Title = b.Title,
                    NormalizedTitle = normalizedTitle,
                    TitleTokens = titleTokens,
                    DigitRuns = _ExtractDigitRuns(titleTokens),
                    OrdinalWords = _ExtractOrdinalWords(titleTokens),
                    BucketKey = normalizedTitle.Replace(" ", "").Length >= DupTitleBucketKeyLength
                                    ? normalizedTitle.Replace(" ", "").Substring(0, DupTitleBucketKeyLength)
                                    : normalizedTitle.Replace(" ", ""),
                    AuthorsLine = string.IsNullOrWhiteSpace(b.AuthorsLine) ? null : b.AuthorsLine.ToPersianNumbers().ApplyCorrectYeKe().Trim(),
                    ISBN = string.IsNullOrWhiteSpace(b.ISBN) ? null : Regex.Replace(b.ISBN, "[^0-9Xx]", "").ToUpperInvariant(),
                    PageCount = b.PageCount,
                    FileSizeInBytes = b.FileSizeInBytes,
                    ClaimedPageCount = b.ClaimedPageCount,
                    PDFSourceId = b.PDFSourceId,
                    MultiVolumePDFCollectionId = b.MultiVolumePDFCollectionId,
                    FileMD5CheckSum = string.IsNullOrWhiteSpace(b.FileMD5CheckSum) ? null : b.FileMD5CheckSum.Trim().ToLowerInvariant()
                });
            }

            await jobProgressServiceEF.UpdateJob(jobId, 1, $"scanning {books.Count} pdf books for duplicates");

            var existingPairs =
                new HashSet<(int, int)>(
                    (await context.PDFBookDuplicateCandidates.AsNoTracking().Select(c => new { c.PDFBookId1, c.PDFBookId2 }).ToArrayAsync())
                    .Select(c => (c.PDFBookId1, c.PDFBookId2))
                );

            var newCandidates = new List<PDFBookDuplicateCandidate>();

            void QueueCandidateIfNew(_PDFBookDupInfo a, _PDFBookDupInfo b, int score, string reasons)
            {
                if (a.Id == b.Id)
                    return;

                // two distinct volumes of the same multi-volume collection are not duplicates
                if (a.MultiVolumePDFCollectionId.HasValue && a.MultiVolumePDFCollectionId == b.MultiVolumePDFCollectionId)
                    return;

                int lowerId = Math.Min(a.Id, b.Id);
                int higherId = Math.Max(a.Id, b.Id);
                var key = (lowerId, higherId);
                if (existingPairs.Contains(key))
                    return;

                existingPairs.Add(key);

                var lower = a.Id == lowerId ? a : b;
                var higher = a.Id == lowerId ? b : a;

                newCandidates.Add
                (
                    new PDFBookDuplicateCandidate()
                    {
                        Id = Guid.NewGuid(),
                        PDFBookId1 = lowerId,
                        Title1 = lower.Title,
                        PDFBookId2 = higherId,
                        Title2 = higher.Title,
                        SurvivorPDFBookId = lowerId, // lowest id is the default candidate to be merged to, reviewer can change this
                        Score = Math.Min(score, 100),
                        MatchReasons = reasons,
                        Status = PDFBookDuplicateCandidateStatus.New,
                        QueueTime = DateTime.Now
                    }
                );
            }

            // --- pass 1 & 2 are cheap grouped scans over the whole table (well under a second even
            //     at ~30000 rows), so they always run in full on every start regardless of resume
            //     state; QueueCandidateIfNew is idempotent against both this run and prior runs ---

            // --- pass 1: identical file (exact checksum match) - as certain a duplicate as it gets ---
            foreach (var group in books.Where(b => b.FileMD5CheckSum != null).GroupBy(b => b.FileMD5CheckSum))
            {
                var arr = group.ToArray();
                for (int i = 0; i < arr.Length; i++)
                    for (int j = i + 1; j < arr.Length; j++)
                        QueueCandidateIfNew(arr[i], arr[j], 100, "identical file checksum (MD5)");
            }

            // --- pass 2: identical ISBN ---
            foreach (var group in books.Where(b => b.ISBN != null).GroupBy(b => b.ISBN))
            {
                var arr = group.ToArray();
                for (int i = 0; i < arr.Length; i++)
                    for (int j = i + 1; j < arr.Length; j++)
                        QueueCandidateIfNew(arr[i], arr[j], 100, "identical ISBN");
            }

            if (newCandidates.Count > 0)
            {
                context.PDFBookDuplicateCandidates.AddRange(newCandidates);
                await context.SaveChangesAsync();
                newCandidates.Clear();
            }

            await jobProgressServiceEF.UpdateJob(jobId, 20, $"checksum/ISBN passes done, {existingPairs.Count} candidates recorded so far");

            // --- pass 3: fuzzy title match, bucketed by leading normalized-title characters.
            //     buckets are processed in a fixed (ordinal) order so progress is resumable: we
            //     persist the last fully-completed bucket key and, on the next start, skip every
            //     bucket up to and including it. ---
            var bucketed = books
                .Where(b => b.NormalizedTitle.Replace(" ", "").Length >= DupMinTitleLengthForFuzzyMatch)
                .GroupBy(b => b.BucketKey)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToArray();

            var state = await context.PDFBookDuplicateDetectionStates.FirstOrDefaultAsync();
            if (state == null)
            {
                state = new PDFBookDuplicateDetectionState() { Id = Guid.NewGuid() };
                context.PDFBookDuplicateDetectionStates.Add(state);
            }

            string resumeAfterKey = (!forceRestart && !state.Completed && !string.IsNullOrEmpty(state.LastProcessedTitleBucketKey))
                                        ? state.LastProcessedTitleBucketKey
                                        : null;

            if (resumeAfterKey == null)
            {
                // fresh run: either first ever run, previous run completed fully, or forceRestart was requested
                state.LastRunStarted = DateTime.Now;
                state.LastProcessedTitleBucketKey = null;
            }
            state.Completed = false;
            state.TotalTitleBuckets = bucketed.Length;
            state.LastRunUpdated = DateTime.Now;
            await context.SaveChangesAsync();

            if (resumeAfterKey != null)
            {
                await jobProgressServiceEF.UpdateJob(jobId, 20, $"resuming title comparison after bucket '{resumeAfterKey}'");
            }
            else if (forceRestart)
            {
                await jobProgressServiceEF.UpdateJob(jobId, 20, "forceRestart requested: starting title comparison from scratch");
            }

            int bucketIndex = 0;
            foreach (var bucket in bucketed)
            {
                bucketIndex++;

                if (resumeAfterKey != null && string.CompareOrdinal(bucket.Key, resumeAfterKey) <= 0)
                    continue;

                var arr = bucket.ToArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    for (int j = i + 1; j < arr.Length; j++)
                    {
                        var a = arr[i];
                        var b = arr[j];

                        // --- disqualifiers: these are checked first and skip the pair outright,
                        //     regardless of how similar the rest of the title looks. They target
                        //     exactly the false-positive patterns seen in review data: different
                        //     volumes/parts of the same book, different issues of the same
                        //     magazine, different specific items in the same named series, and
                        //     books that just happen to have similar-looking titles but are
                        //     obviously not the same physical book by page count. ---

                        // wildly different page counts (e.g. 168 vs 499 pages) - two scans of the
                        // same book can differ slightly (different cover/blank-page handling,
                        // missing a page, etc.) but not by this much. Only compares actual scanned
                        // PageCount (not the self-reported ClaimedPageCount, which is less
                        // reliable) and only when both sides have one.
                        if (a.PageCount > 0 && b.PageCount > 0)
                        {
                            int pageDiff = Math.Abs(a.PageCount - b.PageCount);
                            int allowedPageDiff = Math.Max(DupMaxPageCountDiffAbsolute, (int)Math.Round(DupMaxPageCountDiffRatio * Math.Max(a.PageCount, b.PageCount)));
                            if (pageDiff > allowedPageDiff)
                                continue;
                        }

                        // wildly different PDF file sizes - real duplicates are often literally the
                        // same underlying file (or a re-scan of it) and land within a small
                        // tolerance of each other; a large difference means they're not the same
                        // file even if the titles look similar. Only compared when both sides have
                        // a locally-stored PDFFile (externally-hosted-only books have none).
                        if (a.FileSizeInBytes.HasValue && b.FileSizeInBytes.HasValue)
                        {
                            long sizeDiff = Math.Abs(a.FileSizeInBytes.Value - b.FileSizeInBytes.Value);
                            long allowedSizeDiff = Math.Max(DupMaxFileSizeDiffAbsoluteBytes, (long)Math.Round(DupMaxFileSizeDiffRatio * Math.Max(a.FileSizeInBytes.Value, b.FileSizeInBytes.Value)));
                            if (sizeDiff > allowedSizeDiff)
                                continue;
                        }

                        // different volume/issue/year numbers embedded in the titles (e.g. "ج ۳" vs
                        // "ج ۱۰", or a magazine issue's publication year)
                        if (a.DigitRuns.Count > 0 || b.DigitRuns.Count > 0)
                        {
                            if (!a.DigitRuns.SetEquals(b.DigitRuns))
                                continue;
                        }

                        // different Persian ordinal words used as volume/part/issue markers (e.g.
                        // "جلد دوم" vs "جلد سوم", "شماره اول" vs "شماره دوم"). Checked as an exact
                        // set (not Levenshtein) because these words are short enough that plain
                        // character similarity between them is misleadingly high.
                        if (a.OrdinalWords.Count > 0 && b.OrdinalWords.Count > 0)
                        {
                            if (!a.OrdinalWords.SetEquals(b.OrdinalWords))
                                continue;
                        }

                        double similarity = _ComputeTitleSimilarity(a.NormalizedTitle, b.NormalizedTitle);
                        if (similarity < DupMinTitleSimilarity)
                            continue;

                        // after removing the words the two titles have in common (in order), what's
                        // left on each side is the part that actually distinguishes them. If both
                        // sides have substantial leftover content and that leftover content is
                        // itself dissimilar, this is most likely a different subtitle/topic/person
                        // within the same series (e.g. "...؛ ادبیات ایران" vs "...؛ نسخه شناسی و
                        // کتاب شناسی") rather than a duplicate - even though the shared prefix makes
                        // the *overall* title similarity look high.
                        int minTokenCount = Math.Min(a.TitleTokens.Length, b.TitleTokens.Length);
                        if (minTokenCount > 0)
                        {
                            var (sharedCount, leftoverA, leftoverB) = _OrderPreservingTokenDiff(a.TitleTokens, b.TitleTokens);
                            if ((double)sharedCount / minTokenCount >= DupLeftoverWordsShareRatioMin && leftoverA.Count > 0 && leftoverB.Count > 0)
                            {
                                double leftoverSimilarity = _ComputeTitleSimilarity(string.Join(" ", leftoverA), string.Join(" ", leftoverB));
                                if (leftoverSimilarity < DupLeftoverWordsMaxSimilarity)
                                    continue;
                            }
                        }

                        var reasons = new List<string>
                        {
                            $"title similarity {(int)(similarity * 100)}%"
                        };
                        double score = similarity * 70;

                        if (a.PageCount > 0 && a.PageCount == b.PageCount)
                        {
                            score += 15;
                            reasons.Add($"same page count ({a.PageCount})");
                        }
                        else if (a.ClaimedPageCount.HasValue && a.ClaimedPageCount == b.ClaimedPageCount)
                        {
                            score += 8;
                            reasons.Add($"same claimed page count ({a.ClaimedPageCount})");
                        }

                        if (a.FileSizeInBytes.HasValue && b.FileSizeInBytes.HasValue)
                        {
                            long closeSizeDiff = Math.Abs(a.FileSizeInBytes.Value - b.FileSizeInBytes.Value);
                            long closeSizeAllowance = (long)Math.Round(0.02 * Math.Max(a.FileSizeInBytes.Value, b.FileSizeInBytes.Value));
                            if (closeSizeDiff <= closeSizeAllowance)
                            {
                                score += 10;
                                reasons.Add("very close file size");
                            }
                        }

                        if (a.PDFSourceId.HasValue && a.PDFSourceId == b.PDFSourceId)
                        {
                            score += 8;
                            reasons.Add("same source");
                        }

                        if (a.AuthorsLine != null && b.AuthorsLine != null && a.AuthorsLine == b.AuthorsLine)
                        {
                            score += 7;
                            reasons.Add("same authors line");
                        }

                        int finalScore = (int)Math.Round(Math.Min(score, 100));
                        if (finalScore < DupMinCandidateScore)
                            continue;

                        QueueCandidateIfNew(a, b, finalScore, string.Join("; ", reasons));
                    }
                }

                state.LastProcessedTitleBucketKey = bucket.Key;
                state.ProcessedTitleBuckets = bucketIndex;
                state.LastRunUpdated = DateTime.Now;

                if (bucketIndex % DupCheckpointEveryNBuckets == 0)
                {
                    // checkpoint: persist both the newly found candidates and the resume point
                    // together, so an abrupt interruption right after this never loses either
                    if (newCandidates.Count > 0)
                    {
                        context.PDFBookDuplicateCandidates.AddRange(newCandidates);
                        newCandidates.Clear();
                    }
                    await context.SaveChangesAsync();
                }

                if (bucketIndex % 200 == 0)
                {
                    int percent = 20 + (int)(75.0 * bucketIndex / bucketed.Length);
                    await jobProgressServiceEF.UpdateJob(jobId, Math.Min(percent, 95), $"comparing titles: bucket {bucketIndex} of {bucketed.Length}");
                }
            }

            // final flush of any candidates found since the last checkpoint
            if (newCandidates.Count > 0)
            {
                context.PDFBookDuplicateCandidates.AddRange(newCandidates);
            }

            state.Completed = true;
            state.LastProcessedTitleBucketKey = null; // next start does a fresh full rescan (catches newly-imported books)
            state.LastRunUpdated = DateTime.Now;

            await context.SaveChangesAsync();

            await jobProgressServiceEF.UpdateJob(jobId, 99, $"{existingPairs.Count} total duplicate candidates on file after this run");
        }

        /// <summary>
        /// normalize a (Persian) book title for duplicate comparison: unify Arabic/Persian
        /// letter variants and digits, strip zero-width/invisible characters and punctuation,
        /// collapse whitespace
        /// </summary>
        private static string _NormalizeTitleForDupComparison(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            string s = title.ToPersianNumbers().ApplyCorrectYeKe();

            s = s.Replace("\u200c", " ") // ZWNJ (نیم‌فاصله)
                 .Replace("\u200f", "")  // RLM
                 .Replace("\u200e", "")  // LRM
                 .Replace("\u064B", "").Replace("\u064C", "").Replace("\u064D", "") // tanvin
                 .Replace("\u064E", "").Replace("\u064F", "").Replace("\u0650", "") // fatha/damma/kasra
                 .Replace("\u0651", "").Replace("\u0652", "") // shadda/sukun
                 .Replace("\u0640", ""); // tatweel

            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
                else
                    sb.Append(' ');
            }

            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>
        /// extract the set of digit runs (e.g. volume numbers, issue numbers, years) present in an
        /// already-tokenized normalized title, with Persian digits normalized to plain ASCII digit
        /// strings so "۳" and "3" compare equal
        /// </summary>
        private static HashSet<string> _ExtractDigitRuns(string[] titleTokens)
        {
            var result = new HashSet<string>();
            foreach (var token in titleTokens)
            {
                if (token.Length == 0)
                    continue;

                bool allDigits = true;
                foreach (char ch in token)
                {
                    if (!char.IsDigit(ch))
                    {
                        allDigits = false;
                        break;
                    }
                }
                if (!allDigits)
                    continue;

                var sb = new StringBuilder(token.Length);
                foreach (char ch in token)
                {
                    // normalize any digit (Persian ۰-۹ or ASCII 0-9) to its ASCII digit value
                    int digitValue = (int)char.GetNumericValue(ch);
                    sb.Append(digitValue >= 0 && digitValue <= 9 ? (char)('0' + digitValue) : ch);
                }
                result.Add(sb.ToString());
            }
            return result;
        }

        /// <summary>
        /// extract the set of recognized Persian ordinal words (اول/دوم/سوم/... and بیست و یکم style
        /// compounds) present in an already-tokenized normalized title
        /// </summary>
        private static HashSet<string> _ExtractOrdinalWords(string[] titleTokens)
        {
            var result = new HashSet<string>();
            int i = 0;
            while (i < titleTokens.Length)
            {
                string tok = titleTokens[i];
                if (_dupOrdinalTensBase.Contains(tok) && i + 2 < titleTokens.Length && titleTokens[i + 1] == "و" && _dupOrdinalWords.Contains(titleTokens[i + 2]))
                {
                    result.Add(tok + " و " + titleTokens[i + 2]);
                    i += 3;
                    continue;
                }
                if (_dupOrdinalWords.Contains(tok))
                {
                    result.Add(tok);
                }
                i++;
            }
            return result;
        }

        /// <summary>
        /// order-preserving multiset diff between two token sequences: returns how many tokens they
        /// have in common (each occurrence matched at most once) plus, for each side, the tokens
        /// left over after removing the matched ones (in their original relative order)
        /// </summary>
        private static (int SharedCount, List<string> LeftoverA, List<string> LeftoverB) _OrderPreservingTokenDiff(string[] tokensA, string[] tokensB)
        {
            var remainingB = new Dictionary<string, int>();
            foreach (var t in tokensB)
            {
                remainingB.TryGetValue(t, out int c);
                remainingB[t] = c + 1;
            }

            var leftoverA = new List<string>();
            int sharedCount = 0;
            foreach (var t in tokensA)
            {
                if (remainingB.TryGetValue(t, out int c) && c > 0)
                {
                    remainingB[t] = c - 1;
                    sharedCount++;
                }
                else
                {
                    leftoverA.Add(t);
                }
            }

            var remainingA = new Dictionary<string, int>();
            foreach (var t in tokensA)
            {
                remainingA.TryGetValue(t, out int c);
                remainingA[t] = c + 1;
            }

            var leftoverB = new List<string>();
            foreach (var t in tokensB)
            {
                if (remainingA.TryGetValue(t, out int c) && c > 0)
                {
                    remainingA[t] = c - 1;
                }
                else
                {
                    leftoverB.Add(t);
                }
            }

            return (sharedCount, leftoverA, leftoverB);
        }

        /// <summary>
        /// normalized Levenshtein similarity between two already-normalized strings, in the 0..1 range
        /// </summary>
        private static double _ComputeTitleSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0;

            if (a == b)
                return 1;

            int maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0)
                return 1;

            int distance = _LevenshteinDistance(a, b);
            return 1.0 - ((double)distance / maxLen);
        }

        private static int _LevenshteinDistance(string a, string b)
        {
            int[,] d = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min
                    (
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[a.Length, b.Length];
        }

        /// <summary>
        /// current duplicate-detection progress/resume state (title-fuzzy-matching pass)
        /// </summary>
        public async Task<RServiceResult<PDFBookDuplicateDetectionState>> GetPDFBookDuplicateDetectionStateAsync()
        {
            try
            {
                var state = await _context.PDFBookDuplicateDetectionStates.AsNoTracking().FirstOrDefaultAsync();
                return new RServiceResult<PDFBookDuplicateDetectionState>(state);
            }
            catch (Exception exp)
            {
                return new RServiceResult<PDFBookDuplicateDetectionState>(null, exp.ToString());
            }
        }

        /// <summary>
        /// paginated list of duplicate candidates awaiting/undergone review
        /// </summary>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, PDFBookDuplicateCandidate[] Items)>> GetPDFBookDuplicateCandidatesAsync(PagingParameterModel paging, PDFBookDuplicateCandidateStatus[] statusArray)
        {
            try
            {
                var source =
                    _context.PDFBookDuplicateCandidates.AsNoTracking()
                    .Where(c => statusArray.Contains(c.Status))
                    .OrderByDescending(c => c.Score)
                    .ThenByDescending(c => c.QueueTime)
                    .AsQueryable();

                (PaginationMetadata PagingMeta, PDFBookDuplicateCandidate[] Items) paginatedResult =
                    await QueryablePaginator<PDFBookDuplicateCandidate>.Paginate(source, paging);

                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBookDuplicateCandidate[] Items)>(paginatedResult);
            }
            catch (Exception exp)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, PDFBookDuplicateCandidate[] Items)>((null, null), exp.ToString());
            }
        }

        /// <summary>
        /// update a duplicate candidate's review decision (survivor choice / status / note)
        /// </summary>
        public async Task<RServiceResult<bool>> UpdatePDFBookDuplicateCandidateAsync(PDFBookDuplicateCandidate model)
        {
            try
            {
                var dbModel = await _context.PDFBookDuplicateCandidates.Where(c => c.Id == model.Id).SingleAsync();

                if (model.SurvivorPDFBookId != dbModel.PDFBookId1 && model.SurvivorPDFBookId != dbModel.PDFBookId2)
                {
                    return new RServiceResult<bool>(false, $"SurvivorPDFBookId must be either {dbModel.PDFBookId1} or {dbModel.PDFBookId2}");
                }

                dbModel.SurvivorPDFBookId = model.SurvivorPDFBookId;
                dbModel.Status = model.Status;
                dbModel.ReviewNote = model.ReviewNote;
                dbModel.ReviewerId = model.ReviewerId;
                dbModel.ReviewDate = DateTime.Now;

                _context.Update(dbModel);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// delete a duplicate candidate row (e.g. a false positive the operator wants gone from the list)
        /// </summary>
        public async Task<RServiceResult<bool>> DeletePDFBookDuplicateCandidateAsync(Guid id)
        {
            try
            {
                var dbModel = await _context.PDFBookDuplicateCandidates.Where(c => c.Id == id).SingleAsync();
                _context.Remove(dbModel);
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
