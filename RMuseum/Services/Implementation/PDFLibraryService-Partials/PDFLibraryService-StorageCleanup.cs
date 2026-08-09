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
                    ftpAvailable = true;
                }
                catch (Exception exp)
                {
                    // couldn't connect at all this run - every FTP-needing item will be skipped and
                    // retried on the next invocation; items that only need local cleanup still proceed below
                    await jobProgressServiceEF.UpdateJob(jobId, 2, $"could not connect to FTP this run, will retry FTP items later: {exp.Message}");
                }
            }

            int done = 0;
            for (int i = 0; i < pending.Length; i++)
            {
                var item = pending[i];
                try
                {
                    if (item.NeedsFtpDelete)
                    {
                        if (!ftpAvailable)
                            continue; // leave for next run

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

            await jobProgressServiceEF.UpdateJob(jobId, 99, $"{done} of {pending.Length} storage folders cleaned up this run");
        }
    }
}
