using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Nito.AsyncEx;
using Volo.Abp.AutoMapper;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Services;
using ProductHeaderValue = Octokit.ProductHeaderValue;
using Project = Volo.Docs.Projects.Project;

namespace Volo.Docs.Documents
{
    public class GithubDocumentStore : DomainService, IDocumentStore
    {
        public const string Type = "Github"; //TODO: Convert to "github"

        private readonly IDistributedCache<DocumentDto> _documentDistributedCache;
        private readonly IDistributedCache<List<VersionInfoDto>> _versionInfoDtoDistributedCache;
        private readonly AsyncLock _asyncLock = new AsyncLock();

        public GithubDocumentStore(IDistributedCache<DocumentDto> documentDistributedCache,
            IDistributedCache<List<VersionInfoDto>> versionInfoDtoDistributedCache)
        {
            _documentDistributedCache = documentDistributedCache;
            _versionInfoDtoDistributedCache = versionInfoDtoDistributedCache;
        }

        public async Task<Document> Find(
            Project project,
            string documentName,
            string version)
        {
            var documentCacheKey = project.Id.ToString("N") + documentName + version;

            var document = await _documentDistributedCache.GetAsync(documentCacheKey);
            if (document != null)
            {
                return document.MapTo<Document>();
            }

            using (await _asyncLock.LockAsync())
            {
                document = await _documentDistributedCache.GetAsync(documentCacheKey);
                if (document != null)
                {
                    return document.MapTo<Document>();
                }

                var rootUrl = project.GetGithubUrl()
                    .Replace("_version_/", version + "/")
                    .Replace("www.", ""); //TODO: Can be a problem?

                var rawRootUrl = rootUrl
                    .Replace("github.com", "raw.githubusercontent.com")
                    .Replace("/tree/", "/"); //TODO: Replacing this can be a problem if I have a tree folder inside the repository

                var rawUrl = rawRootUrl + documentName;
                var editLink = rootUrl.Replace("/tree/", "/blob/") + documentName;
                var localDirectory = "";
                var fileName = documentName;

                if (documentName.Contains("/"))
                {
                    localDirectory = documentName.Substring(0, documentName.LastIndexOf('/'));
                    fileName = documentName.Substring(
                        documentName.LastIndexOf('/') + 1,
                        documentName.Length - documentName.LastIndexOf('/') - 1
                    );
                }

                var token = project.ExtraProperties["GithubAccessToken"]?.ToString(); //TODO: Define GetGithubAccessToken extension method

                document = new DocumentDto
                {
                    Title = documentName,
                    EditLink = editLink,
                    RootUrl = rootUrl,
                    RawRootUrl = rawRootUrl,
                    Format = project.Format,
                    LocalDirectory = localDirectory,
                    FileName = fileName,
                    Version = version,
                    Content = DownloadWebContent(rawUrl, token)
                };

                await _documentDistributedCache.SetAsync(documentCacheKey, document, new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromDays(1)
                });
            }

            return document.MapTo<Document>();
        }

        private string DownloadWebContent(string rawUrl, string token)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    if (!token.IsNullOrWhiteSpace())
                    {
                        webClient.Headers.Add("Authorization", "token " + token);
                    }

                    return webClient.DownloadString(rawUrl);
                }
            }
            catch (Exception ex) //TODO: Only handle when document is really not available
            {
                Logger.LogWarning(ex.Message, ex);
                throw new DocumentNotFoundException(rawUrl);
            }
        }

        public async Task<List<VersionInfoDto>> GetVersions(Project project)
        {
            var versionInfoListCacheKey = project.Id.ToString("N");

            var versionInfoList = await _versionInfoDtoDistributedCache.GetAsync(versionInfoListCacheKey);
            if (versionInfoList != null)
            {
                return versionInfoList;
            }

            using (await _asyncLock.LockAsync())
            {
                versionInfoList = await _versionInfoDtoDistributedCache.GetAsync(versionInfoListCacheKey);
                if (versionInfoList != null)
                {
                    return versionInfoList;
                }

                try
                {
                    var token = project.ExtraProperties["GithubAccessToken"]?.ToString();

                    var gitHubClient = token.IsNullOrWhiteSpace()
                        ? new GitHubClient(new ProductHeaderValue("AbpWebSite"))
                        : new GitHubClient(new ProductHeaderValue("AbpWebSite"),
                            new InMemoryCredentialStore(new Credentials(token)));

                    var url = project.ExtraProperties["GithubRootUrl"].ToString();
                    var releases = await gitHubClient.Repository.Release.GetAll(
                        GetGithubOrganizationNameFromUrl(url),
                        GetGithubRepositoryNameFromUrl(url)
                    );

                    versionInfoList = releases.OrderByDescending(r => r.PublishedAt).Select(r => new VersionInfoDto
                    {
                        Name = r.TagName, DisplayName = r.TagName
                    }).ToList();

                    versionInfoList.Insert(0, new VersionInfoDto
                    {
                        Name = "master", DisplayName = "master"
                    });
                    await _versionInfoDtoDistributedCache.SetAsync(versionInfoListCacheKey, versionInfoList,
                        new DistributedCacheEntryOptions
                        {
                            SlidingExpiration = TimeSpan.FromDays(1)
                        });

                    return versionInfoList;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.Message, ex);
                    return new List<VersionInfoDto>();
                }
            }
        }

        private static string GetGithubOrganizationNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash =
                    url.Substring(url.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase) + "github.com/".Length);
                return urlStartingAfterFirstSlash.Substring(0, urlStartingAfterFirstSlash.IndexOf('/'));
            }
            catch (Exception)
            {
                throw new Exception($"Github url is not valid: {url}");
            }
        }

        private string GetGithubRepositoryNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash =
                    url.Substring(url.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase) + "github.com/".Length);
                var urlStartingAfterSecondSlash =
                    urlStartingAfterFirstSlash.Substring(urlStartingAfterFirstSlash.IndexOf('/') + 1);
                return urlStartingAfterSecondSlash.Substring(0, urlStartingAfterSecondSlash.IndexOf('/'));
            }
            catch (Exception)
            {
                throw new Exception($"Github url is not valid: {url}");
            }
        }
    }
}