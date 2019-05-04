using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;
using Volo.Docs.Documents;
using Volo.Docs.GitHub.Projects;
using Volo.Docs.Projects;
using Newtonsoft.Json.Linq;
using Volo.Abp.Caching;
using Nito.AsyncEx;

namespace Volo.Docs.GitHub.Documents
{
    //TODO: Needs more refactoring

    public class GithubDocumentStore : DomainService, IDocumentStore
    {
        public const string Type = "GitHub";

        private readonly IDistributedCache<List<VersionInfo>> _versionInfoDistributedCache;
        private readonly IDistributedCache<string> _githubStringContentDistributedCache;
        private readonly IDistributedCache<byte[]> _githubByteContentDistributedCache;
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private readonly IGithubRepositoryManager _githubRepositoryManager;

        public GithubDocumentStore(
            IDistributedCache<List<VersionInfo>> versionInfoDistributedCache, 
            IDistributedCache<string> githubStringContentDistributedCache, 
            IDistributedCache<byte[]> githubByteContentDistributedCache,
            IGithubRepositoryManager githubRepositoryManager)
        {
            _versionInfoDistributedCache = versionInfoDistributedCache;
            _githubStringContentDistributedCache = githubStringContentDistributedCache;
            _githubByteContentDistributedCache = githubByteContentDistributedCache;
            _githubRepositoryManager = githubRepositoryManager;
        }
        
        public virtual async Task<Document> GetDocumentAsync(Project project, string documentName, string version)
        {
            var token = project.GetGitHubAccessTokenOrNull();
            var rootUrl = project.GetGitHubUrl(version);
            var rawRootUrl = CalculateRawRootUrl(rootUrl);
            var rawDocumentUrl = rawRootUrl + documentName;
            var commitHistoryUrl = project.GetGitHubUrlForCommitHistory() + documentName;
            var userAgent = project.GetGithubUserAgentOrNull();
            var isNavigationDocument = documentName == project.NavigationDocumentName;
            var editLink = rootUrl.ReplaceFirst("/tree/", "/blob/") + documentName;
            var localDirectory = "";
            var fileName = documentName;

            if (documentName.Contains("/"))
            {
                localDirectory = documentName.Substring(0, documentName.LastIndexOf('/'));
                fileName = documentName.Substring(documentName.LastIndexOf('/') + 1);
            }

            return new Document
            {
                Title = documentName,
                EditLink = editLink,
                RootUrl = rootUrl,
                RawRootUrl = rawRootUrl,
                Format = project.Format,
                LocalDirectory = localDirectory,
                FileName = fileName,
                //Contributors = new List<DocumentContributor>(),
                Contributors = !isNavigationDocument ? await GetContributors(commitHistoryUrl, token, userAgent): new List<DocumentContributor>(),
                Version = version,
                Content = await DownloadWebContentAsStringAsync(rawDocumentUrl, token, userAgent)
            };
        }

        public async Task<List<VersionInfo>> GetVersionsAsync(Project project)
        {
            using (await _asyncLock.LockAsync())
            {
                var versionInfoCacheKey = project.Id.ToString("N");

                var versionInfoCache = await _versionInfoDistributedCache.GetAsync(versionInfoCacheKey);
                if (versionInfoCache != null)
                {
                    return versionInfoCache;
                }

                try
                {
                    var versions = (await GetReleasesAsync(project))
                        .OrderByDescending(r => r.PublishedAt)
                        .Select(r => new VersionInfo
                        {
                            Name = r.TagName,
                            DisplayName = r.TagName
                        }).ToList();
                    versions.Insert(0, new VersionInfo
                    {
                        Name = "master",
                        DisplayName = "master"
                    });

                    await _versionInfoDistributedCache.SetAsync(versionInfoCacheKey, versions,
                        new DistributedCacheEntryOptions
                        {
                            SlidingExpiration = TimeSpan.FromDays(1)
                        });

                    return versions;
                }
                catch (Exception ex)
                {
                    //TODO: It may not be a good idea to hide the error!
                    Logger.LogError(ex.Message, ex);
                    return new List<VersionInfo>();
                }
            }
        }

        public async Task<DocumentResource> GetResource(Project project, string resourceName, string version)
        {
            var rawRootUrl = CalculateRawRootUrl(project.GetGitHubUrl(version));
            var content = await DownloadWebContentAsByteArrayAsync(
                rawRootUrl + resourceName,
                project.GetGitHubAccessTokenOrNull(),
                project.GetGithubUserAgentOrNull()
            );

            return new DocumentResource(content);
        }

        private async Task<IReadOnlyList<Octokit.Release>> GetReleasesAsync(Project project)
        {
            var url = project.GetGitHubUrl();
            var ownerName = GetOwnerNameFromUrl(url);
            var repositoryName = GetRepositoryNameFromUrl(url);
            return await _githubRepositoryManager.GetReleasesAsync(ownerName, repositoryName, project.GetGitHubAccessTokenOrNull());
        }

        protected virtual string GetOwnerNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash = url.Substring(url.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase) + "github.com/".Length);
                return urlStartingAfterFirstSlash.Substring(0, urlStartingAfterFirstSlash.IndexOf('/'));
            }
            catch (Exception)
            {
                throw new Exception($"Github url is not valid: {url}");
            }
        }

        protected virtual string GetRepositoryNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash = url.Substring(url.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase) + "github.com/".Length);
                var urlStartingAfterSecondSlash = urlStartingAfterFirstSlash.Substring(urlStartingAfterFirstSlash.IndexOf('/') + 1);
                return urlStartingAfterSecondSlash.Substring(0, urlStartingAfterSecondSlash.IndexOf('/'));
            }
            catch (Exception)
            {
                throw new Exception($"Github url is not valid: {url}");
            }
        }

        private async Task<string> DownloadWebContentAsStringAsync(string rawUrl, string token, string userAgent)
        {
            using (await _asyncLock.LockAsync())
            {
                var githubContentCacheKey = rawUrl;

                var githubContentCache = await _githubStringContentDistributedCache.GetAsync(githubContentCacheKey);
                if (githubContentCache != null)
                {
                    return githubContentCache;
                }

                var content = await _githubRepositoryManager.GetFileRawStringContentAsync(rawUrl, token, userAgent);
                
                await _githubStringContentDistributedCache.SetAsync(githubContentCacheKey, content,
                            new DistributedCacheEntryOptions
                            {
                                SlidingExpiration = TimeSpan.FromDays(1)
                            });

                return content;
            }
        }

        private async Task<byte[]> DownloadWebContentAsByteArrayAsync(string rawUrl, string token, string userAgent)
        {
            using (await _asyncLock.LockAsync())
            {
                var githubContentCacheKey = rawUrl;

                var githubContentCache = await _githubByteContentDistributedCache.GetAsync(githubContentCacheKey);
                if (githubContentCache != null)
                {
                    return githubContentCache;
                }

                var content = await _githubRepositoryManager.GetFileRawByteArrayContentAsync(rawUrl, token, userAgent);

                await _githubByteContentDistributedCache.SetAsync(githubContentCacheKey, content,
                            new DistributedCacheEntryOptions
                            {
                                SlidingExpiration = TimeSpan.FromDays(1)
                            });
                            
                return content;
            }
        }

        private async Task<List<DocumentContributor>> GetContributors(string url, string token, string userAgent)
        {
            var contributors = new List<DocumentContributor>();

            try
            {
                var commitsJsonAsString = await DownloadWebContentAsStringAsync(url, token, userAgent);

                var commits = JArray.Parse(commitsJsonAsString);

                foreach (var commit in commits)
                {
                    var author = commit["author"];

                    contributors.Add(new DocumentContributor
                    {
                        Username = (string)author["login"],
                        UserProfileUrl = (string)author["html_url"],
                        AvatarUrl = (string)author["avatar_url"]
                    });
                }

                contributors = contributors.GroupBy(c => c.Username).OrderByDescending(c=>c.Count())
                    .Select( c => c.FirstOrDefault()).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex.Message);
            }
            
            return contributors;
        }

        private static string CalculateRawRootUrl(string rootUrl)
        {
            return rootUrl
                .Replace("github.com", "raw.githubusercontent.com")
                .ReplaceFirst("/tree/", "/")
                .EnsureEndsWith('/');
        }
    }
}
