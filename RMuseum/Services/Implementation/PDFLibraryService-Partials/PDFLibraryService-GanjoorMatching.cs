using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Artifact;
using RMuseum.Models.Ganjoor.ViewModels;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RSecurityBackend.Models.Generic;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    public partial class PDFLibraryService
    {
        /// <summary>
        /// queue ganjoor poem match finding
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> QueueGanjoorPoemMatchAsync(GanjoorPoemMatchViewModel model)
        {
			try
			{
                if(await _context.GanjoorPoemMatchFindings.Where(m => m.GanjoorCatId == model.GanjoorCatId && m.BookId == model.BookId).AnyAsync())
                {
                    return new RServiceResult<bool>(false, $"Duplicated item - CatId = {model.GanjoorCatId}, BookId = {model.BookId}");
                }

                GanjoorPoemMatchFinding matchFinding = new GanjoorPoemMatchFinding()
                {
                    GanjoorCatId = model.GanjoorCatId,
                    GanjoorPoemId = model.GanjoorPoemId,
                    BookId = model.BookId,
                    PageNumber = model.PageNumber,
                    QueueTime = DateTime.Now,
                    Started = false,
                    Progress = 0,
                    Finished = false,
                    CurrentPoemId = model.GanjoorPoemId,
                    CurrentPageNumber = model.PageNumber,
                };
                using (HttpClient httpClient = new HttpClient())
                {
                    var catResponse = await httpClient.GetAsync($"https://api.ganjoor.net/api/ganjoor/cat/{model.GanjoorCatId}?poems=false&mainSections=false");
                    catResponse.EnsureSuccessStatusCode();
                    var cat = JsonConvert.DeserializeObject<GanjoorPoetCompleteViewModel>(await catResponse.Content.ReadAsStringAsync());
                    matchFinding.GanjoorCatFullUrl = cat.Cat.FullUrl;

                    var pageResponse = await httpClient.GetAsync($"https://api.ganjoor.net/api/ganjoor/page?url={cat.Cat.FullUrl}");
                    var pageInformation = JObject.Parse(await pageResponse.Content.ReadAsStringAsync()).ToObject<GanjoorPageCompleteViewModel>();
                    matchFinding.GanjoorCatFullTitle = pageInformation.FullTitle;
                }

                var book = (await GetPDFBookByIdAsync(model.BookId, [PublishStatus.Published], false, false, false)).Result;
                matchFinding.BookTitle = book.Title;

                _context.Add(matchFinding);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
			}
			catch (Exception exp)
			{
                return new RServiceResult<bool>(false, exp.ToString());
			}
        }

        /// <summary>
        /// ganjoor poem match finding queue
        /// </summary>
        /// <param name="notStarted"></param>
        /// <param name="notFinished"></param>
        /// <returns></returns>
        public async Task<RServiceResult<GanjoorPoemMatchFinding[]>> GetGanjoorPoemMatchQueueAsync(bool notStarted = false, bool notFinished = true)
        {
            try
            {
                return new RServiceResult<GanjoorPoemMatchFinding[]>
                    (
                    await _context.GanjoorPoemMatchFindings.AsNoTracking()
                        .Where
                        (
                        m => 
                            (notStarted == false || m.Started == false)
                            &&
                            (notFinished == false || m.Finished == false)
                        )
                        .ToArrayAsync()
                    );
            }
            catch (Exception exp)
            {
                return new RServiceResult<GanjoorPoemMatchFinding[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// update a ganjoor poem match finding
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> UpdateGanjoorPoemMatchFindingAsync(GanjoorPoemMatchFinding model)
        {
            try
            {
                var dbModel = await _context.GanjoorPoemMatchFindings.Where(m => m.Id == model.Id).SingleAsync();
                dbModel.LastUpdate = DateTime.Now;
                dbModel.LastUpdatedByUserId = model.LastUpdatedByUserId;
                if(dbModel.Started == false && model.Started == true)
                {
                    dbModel.StartTime = DateTime.Now;
                    dbModel.Started = model.Started;
                }
                dbModel.CurrentPoemId = model.CurrentPoemId;
                dbModel.CurrentPageNumber = model.CurrentPageNumber;
                dbModel.Progress = model.Progress;
                if(model.Finished && dbModel.Finished == false)
                {
                    dbModel.Finished = true;
                    dbModel.FinishTime = model.FinishTime;
                }
                _context.Update(dbModel);
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