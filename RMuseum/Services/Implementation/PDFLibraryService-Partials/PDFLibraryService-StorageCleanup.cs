using FluentFTP;
using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.PDFLibrary;
using RSecurityBackend.Services.Implementation;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// start physically cleaning up storage folders queued in PendingPDFStorageCleanup
        /// (FTP + local disk). Safe to call repeatedly / after an interruption - each row is only
        /// removed from the queue once its cleanup actually succeeds, so a crash mid-run just
        /// leaves fewer rows for the next invocation to pick up. One failing item never blocks the
        /// rest of the batch.
        /// </summary>
        public void StartCleaningUpPendingPDFStorageAsync()
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
                                   (
                                       async token =>
                                       {
                                           using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                                           {
                                               LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                               var job = (await jobProgressServiceEF.NewJob("StartCleaningUpPendingPDFStorageAsync", "Query data")).Result;

                                               try
                                               {
                                                   await _CleanUpPendingPDFStorageAsync(context, jobProgressServiceEF, job.Id);

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

        private async Task _CleanUpPendingPDFStorageAsync(RMuseumDbContext context, LongRunningJobProgressServiceEF jobProgressServiceEF, Guid jobId)
        {
            var pending = await context.PendingPDFStorageCleanups.OrderBy(p => p.QueueTime).ToArrayAsync();

            await jobProgressServiceEF.UpdateJob(jobId, 1, $"{pending.Length} storage folders pending cleanup");

            if (pending.Length == 0)
                return;

            AsyncFtpClient ftpClient = null;
            bool ftpAvailable = false;
            string ftpConnectError = null;
            if (pending.Any(p => p.NeedsFtpDelete))
            {
                try
                {
                    ftpClient = new AsyncFtpClient
                    (
                        Configuration.GetSection("ExternalFTPServer")["Host"],
                        Configuration.GetSection("ExternalFTPServer")["Username"],
                        Configuration.GetSection("ExternalFTPServer")["Password"]
                    );
                    ftpClient.ValidateCertificate += FtpClient_ValidateCertificate;
                    await ftpClient.AutoConnect();
                    ftpClient.Config.RetryAttempts = 3;
                    // some already-uploaded files in this corpus have odd names containing ".."
                    // (e.g. "00010922-eliteraturebook..pdf") that FluentFTP's path sanitizer
                    // otherwise refuses to touch, treating any ".." as a possible directory-
                    // traversal attempt. Safe to disable here specifically: this path is built
                    // entirely from our own DB's StorageFolderName, never from request input, so
                    // there's no injection risk - we're just deleting a folder we already own.
                    ftpClient.Config.SanitizeTraversal = false;
                    ftpAvailable = true;
                }
                catch (Exception exp)
                {
                    // couldn't connect at all this run - every FTP-needing item will be skipped and
                    // retried on the next invocation; items that only need local cleanup still
                    // proceed below. Record the reason on every affected row (not just the job log,
                    // which would otherwise get overwritten by the final summary below and lose it)
                    // so it's diagnosable from the row itself.
                    ftpConnectError = exp.Message;
                    await jobProgressServiceEF.UpdateJob(jobId, 2, $"could not connect to FTP this run, will retry FTP items later: {ftpConnectError}");
                }
            }

            int done = 0;
            int skippedNoFtp = 0;
            for (int i = 0; i < pending.Length; i++)
            {
                var item = pending[i];
                try
                {
                    if (item.NeedsFtpDelete)
                    {
                        if (!ftpAvailable)
                        {
                            skippedNoFtp++;
                            item.AttemptCount++;
                            item.LastAttempt = DateTime.Now;
                            item.LastError = $"FTP server unreachable this run: {ftpConnectError}";
                            context.Update(item);
                            await context.SaveChangesAsync();
                            continue; // leave for next run
                        }

                        string remoteDir = $"{Configuration.GetSection("ExternalFTPServer")["RootPath"]}/pdf/{item.StorageFolderName}";
                        if (await ftpClient.DirectoryExists(remoteDir))
                        {
                            await ftpClient.DeleteDirectory(remoteDir);
                        }
                    }

                    string localDir = Path.Combine(_imageFileService.ImageStoragePath, item.StorageFolderName);
                    if (!string.IsNullOrEmpty(item.StorageFolderName) && Directory.Exists(localDir))
                    {
                        Directory.Delete(localDir, true);
                    }

                    context.PendingPDFStorageCleanups.Remove(item);
                    await context.SaveChangesAsync();
                    done++;
                }
                catch (Exception exp)
                {
                    item.AttemptCount++;
                    item.LastAttempt = DateTime.Now;
                    item.LastError = exp.Message;
                    context.Update(item);
                    await context.SaveChangesAsync();
                    // continue to the next item - one failure never blocks the rest of the batch
                }

                if ((i + 1) % 20 == 0)
                {
                    int percent = 2 + (int)(95.0 * (i + 1) / pending.Length);
                    await jobProgressServiceEF.UpdateJob(jobId, Math.Min(percent, 97), $"cleaned up {done} of {pending.Length} so far");
                }
            }

            if (ftpClient != null && ftpAvailable)
            {
                await ftpClient.Disconnect();
            }

            string summary = $"{done} of {pending.Length} storage folders cleaned up this run";
            if (skippedNoFtp > 0)
            {
                summary += $" ({skippedNoFtp} skipped - FTP unreachable: {ftpConnectError})";
            }
            await jobProgressServiceEF.UpdateJob(jobId, 99, summary);
        }
    }
}
