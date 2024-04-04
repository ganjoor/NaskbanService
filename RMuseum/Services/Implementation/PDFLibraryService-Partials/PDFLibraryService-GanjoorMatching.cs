using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Cmp;
using RMuseum.DbContext;
using RMuseum.Models.Artifact;
using RMuseum.Models.Ganjoor.ViewModels;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.PDFLibrary.ViewModels;
using RSecurityBackend.Models.Generic;
using System;
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
    }
}