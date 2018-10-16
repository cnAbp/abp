using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace Volo.Docs.Documents
{
    public class GiteeDocumentStore : DomainService, IDocumentStore
    {
        public const string Type = "Gitee";

        public async Task<Document> FindDocumentByNameAsync(Dictionary<string, object> projectExtraProperties, string projectFormat, string documentName, string version)
        {
            var rootUrl = projectExtraProperties["GiteeRootUrl"].ToString().Replace("_version_/", version + "/").Replace("www.", "");
            var rawRootUrl = rootUrl.Replace("/tree/", "/raw/");
            var rawUrl = rawRootUrl + documentName;
            var editLink = rootUrl.Replace("/tree/", "/blob/") + documentName;
            string localDirectory = "";
            string fileName = documentName;

            if (documentName.Contains("/"))
            {
                localDirectory = documentName.Substring(0, documentName.LastIndexOf('/'));
                fileName = documentName.Substring(documentName.LastIndexOf('/') + 1,
                    documentName.Length - documentName.LastIndexOf('/') - 1);
            }

            var document = new Document
            {
                Title = documentName,
                EditLink = editLink,
                RootUrl = rootUrl,
                RawRootUrl = rawRootUrl,
                Format = projectFormat,
                LocalDirectory = localDirectory,
                FileName = fileName,
                Version = version,
                SuccessfullyRetrieved = TryDownloadWebContent(rawUrl, out var content),
                Content = content
            };

            return await Task.FromResult(document);
        }

        private bool TryDownloadWebContent(string rawUrl, out string content)
        {
            using (var webClient = new WebClient())
            {
                try
                {
                    content = webClient.DownloadString(rawUrl);
                    return true;
                }
                catch (Exception ex)
                {
                    content = null;
                    Logger.LogError(ex, ex.Message);
                    return false;
                }
            }
        }

        public async Task<List<string>> GetVersions(Dictionary<string, object> projectExtraProperties, string documentName)
        {
            try
            {
                var giteeClient = new HttpClient();
                var url = projectExtraProperties["GiteeRootUrl"].ToString();
                var token = projectExtraProperties["GiteeAccessToken"]?.ToString();

                var releasesResponseMessage = await giteeClient.GetAsync(
                    "https://gitee.com/api/v5/repos/" +
                    GetGiteeOrganizationNameFromUrl(url) + "/" +
                    GetGiteeRepositoryNameFromUrl(url) +
                    $"/releases?access_token={token}&page=1&per_page=10000");

                if(releasesResponseMessage.StatusCode != HttpStatusCode.OK)
                {
                    this.Logger.LogError(await releasesResponseMessage.Content.ReadAsStringAsync());
                }

                var releases =
                    JsonConvert.DeserializeObject<List<GiteeReleaseModel>>(await releasesResponseMessage.Content
                        .ReadAsStringAsync());
                var versions = releases.OrderByDescending(r => r.CreatedAt).Select(r => r.TagName).ToList();
                versions.Insert(0, "master");
                return versions.ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message, ex);
                return new List<string>();
            }
        }

        private static string GetGiteeOrganizationNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash =
                    url.Substring(url.IndexOf("gitee.com/", StringComparison.OrdinalIgnoreCase) + "gitee.com/".Length);
                return urlStartingAfterFirstSlash.Substring(0, urlStartingAfterFirstSlash.IndexOf('/'));
            }
            catch (Exception)
            {
                throw new Exception($"Gitee url is not valid: {url}");
            }
        }

        private string GetGiteeRepositoryNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash =
                    url.Substring(url.IndexOf("gitee.com/", StringComparison.OrdinalIgnoreCase) + "gitee.com/".Length);
                var urlStartingAfterSecondSlash =
                    urlStartingAfterFirstSlash.Substring(urlStartingAfterFirstSlash.IndexOf('/') + 1);
                return urlStartingAfterSecondSlash.Substring(0, urlStartingAfterSecondSlash.IndexOf('/'));
            }
            catch (Exception)
            {
                throw new Exception($"Gitee url is not valid: {url}");
            }
        }
    }
}
