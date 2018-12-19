using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Octokit;
using Octokit.Internal;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Services;
using Volo.Docs.Documents;
using Volo.Docs.GitHub.Projects;
using Volo.Docs.Projects;
using ProductHeaderValue = Octokit.ProductHeaderValue;
using Project = Volo.Docs.Projects.Project;

namespace Volo.Docs.GitHub.Documents
{
    //TODO: Needs refactoring

    public class GithubDocumentStore : DomainService, IDocumentStore
    {
        private readonly IDistributedCache<List<VersionInfo>> _versionInfoDistributedCache;
        private readonly IDistributedCache<string> _githubStringContentDistributedCache;
        private readonly IDistributedCache<byte[]> _githubByteContentDistributedCache;
        private readonly AsyncLock _asyncLock = new AsyncLock();

        public GithubDocumentStore(
            IDistributedCache<List<VersionInfo>> versionInfoDistributedCache, 
            IDistributedCache<string> githubStringContentDistributedCache, 
            IDistributedCache<byte[]> githubByteContentDistributedCache)
        {
            _versionInfoDistributedCache = versionInfoDistributedCache;
            _githubStringContentDistributedCache = githubStringContentDistributedCache;
            _githubByteContentDistributedCache = githubByteContentDistributedCache;
        }

        public const string Type = "GitHub";

        public virtual async Task<Document> GetDocument(Project project, string documentName, string version)
        {
            var rootUrl = project.GetGitHubUrl(version);
            var rawRootUrl = CalculateRawRootUrl(rootUrl);
            var rawDocumentUrl = rawRootUrl + documentName;
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
                Version = version,
                Content = await DownloadWebContentAsync(rawDocumentUrl, project.GetGitHubAccessTokenOrNull())
            };
        }

        private static string CalculateRawRootUrl(string rootUrl)
        {
            return rootUrl
                .Replace("github.com", "raw.githubusercontent.com")
                .ReplaceFirst("/tree/", "/");
        }

        public async Task<List<VersionInfo>> GetVersions(Project project)
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
                project.GetGitHubAccessTokenOrNull()
            );

            return new DocumentResource(content);
        }

        private async Task<IReadOnlyList<Release>> GetReleasesAsync(Project project)
        {
            var url = project.GetGitHubUrl();
            var ownerName = GetOwnerNameFromUrl(url);
            var repositoryName = GetRepositoryNameFromUrl(url);
            var gitHubClient = CreateGitHubClient(project.GetGitHubAccessTokenOrNull());

            return await gitHubClient
                .Repository
                .Release
                .GetAll(ownerName, repositoryName);
        }

        private static GitHubClient CreateGitHubClient(string token = null)
        {
            //TODO: Why hard-coded "abpframework"? Should be configurable?
            return token.IsNullOrWhiteSpace()
                ? new GitHubClient(new ProductHeaderValue("abpframework"))
                : new GitHubClient(new ProductHeaderValue("abpframework"), new InMemoryCredentialStore(new Credentials(token)));
        }

        protected virtual string GetOwnerNameFromUrl(string url)
        {
            try
            {
                var urlStartingAfterFirstSlash = url.Substring(url.IndexOf("github.com/",StringComparison.OrdinalIgnoreCase) + "github.com/".Length);
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

        private async Task<string> DownloadWebContentAsync(string rawUrl, string token)
        {
            using (await _asyncLock.LockAsync())
            {
                var githubContentCacheKey = rawUrl;

                var githubContentCache = await _githubStringContentDistributedCache.GetAsync(githubContentCacheKey);
                if (githubContentCache != null)
                {
                    return githubContentCache;
                }

                try
                {
                    using (var webClient = new WebClient())
                    {
                        if (!token.IsNullOrWhiteSpace())
                        {
                            webClient.Headers.Add("Authorization", "token " + token);
                        }

                        var content = await webClient.DownloadStringTaskAsync(new Uri(rawUrl));

                        await _githubStringContentDistributedCache.SetAsync(githubContentCacheKey, content,
                            new DistributedCacheEntryOptions
                            {
                                SlidingExpiration = TimeSpan.FromDays(1)
                            });

                        return content;
                    }
                }
                catch (Exception ex)
                {
                    //TODO: Only handle when document is really not available
                    Logger.LogWarning(ex.Message, ex);
                    throw new DocumentNotFoundException(rawUrl);
                }
            }
        }

        private async Task<byte[]> DownloadWebContentAsByteArrayAsync(string rawUrl, string token)
        {
            using (await _asyncLock.LockAsync())
            {
                var githubContentCacheKey = rawUrl;

                var githubContentCache = await _githubByteContentDistributedCache.GetAsync(githubContentCacheKey);
                if (githubContentCache != null)
                {
                    return githubContentCache;
                }

                try
                {
                    using (var webClient = new WebClient())
                    {
                        if (!token.IsNullOrWhiteSpace())
                        {
                            webClient.Headers.Add("Authorization", "token " + token);
                        }

                        var content = await webClient.DownloadDataTaskAsync(new Uri(rawUrl));

                        await _githubByteContentDistributedCache.SetAsync(githubContentCacheKey, content,
                            new DistributedCacheEntryOptions
                            {
                                SlidingExpiration = TimeSpan.FromDays(1)
                            });

                        return content;
                    }
                }
                catch (Exception ex)
                {
                    //TODO: Only handle when resource is really not available
                    Logger.LogWarning(ex.Message, ex);
                    throw new ResourceNotFoundException(rawUrl);
                }
            }
        }
    }
}