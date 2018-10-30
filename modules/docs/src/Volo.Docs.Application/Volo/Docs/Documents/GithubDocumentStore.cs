using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using ProductHeaderValue = Octokit.ProductHeaderValue;

namespace Volo.Docs.Documents
{
    public class GithubDocumentStore : DomainService, IDocumentStore
    {
        public const string Type = "Github"; //TODO: Convert to "github"

        public const int DocumentNotFoundExceptionCode = 20181001;

        public async Task<Document> FindDocumentByNameAsync(Dictionary<string, object> projectExtraProperties, string projectFormat, string documentName, string version)
        {
            var rootUrl = projectExtraProperties["GithubRootUrl"].ToString().Replace("_version_/", version + "/").Replace("www.", "");

            var token = projectExtraProperties["GithubAccessToken"]?.ToString();

            var rawRootUrl = rootUrl.Replace("github.com", "raw.githubusercontent.com").Replace("/tree/", "/");
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
                SuccessfullyRetrieved = TryDownloadWebContent(rawUrl, token, out var content),
                Content = content
            };

            return await Task.FromResult(document);
        }

        private bool TryDownloadWebContent(string rawUrl, string token, out string content)
        {
            using (var webClient = new WebClient())
            {
                try
                {
                    if (!token.IsNullOrWhiteSpace())
                    {
                        webClient.Headers.Add("Authorization", "token " + token);
                    }

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

        public async Task<List<VersionInfoDto>> GetVersions(Dictionary<string, object> projectExtraProperties, string documentName)
        {
            try
            {
                var token = projectExtraProperties["GithubAccessToken"]?.ToString();

                var gitHubClient = token.IsNullOrWhiteSpace()
                    ? new GitHubClient(new ProductHeaderValue("AbpWebSite"))
                    : new GitHubClient(new ProductHeaderValue("AbpWebSite"), new InMemoryCredentialStore(new Credentials(token)));

                var url = projectExtraProperties["GithubRootUrl"].ToString();
                var releases = await gitHubClient.Repository.Release.GetAll(
                    GetGithubOrganizationNameFromUrl(url),
                    GetGithubRepositoryNameFromUrl(url)
                );

                return releases.OrderByDescending(r => r.PublishedAt).Select(r => new VersionInfoDto { Name = r.TagName, DisplayName = r.TagName }).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message, ex);
                return new List<VersionInfoDto>();
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