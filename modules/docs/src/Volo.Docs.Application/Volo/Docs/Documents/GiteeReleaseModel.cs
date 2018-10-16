using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Volo.Docs.Documents
{
    public class GiteeReleaseModel
    {
        public int Id { get; set; }

        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("target_commitish")]
        public string TargetCommitish { get; set; }

        public bool Prerelease { get; set; }

        public string Name { get; set; }

        public string Body { get; set; }

        public Author Author { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        public Asset[] Assets { get; set; }
    }

    public class Author
    {
        public int Id { get; set; }

        public string Login { get; set; }

        public string Name { get; set; }

        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        public string Url { get; set; }

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        [JsonProperty("followers_url")]
        public string FollowersUrl { get; set; }

        [JsonProperty("following_url")]
        public string FollowingUrl { get; set; }

        [JsonProperty("gists_url")]
        public string GistsUrl { get; set; }

        [JsonProperty("starred_url")]
        public string StarredUrl { get; set; }

        [JsonProperty("subscriptions_url")]
        public string SubscriptionsUrl { get; set; }

        [JsonProperty("organizations_url")]
        public string OrganizationsUrl { get; set; }

        [JsonProperty("repos_url")]
        public string ReposUrl { get; set; }

        [JsonProperty("events_url")]
        public string EventsUrl { get; set; }

        [JsonProperty("received_events_url")]
        public string ReceivedEventsUrl { get; set; }

        public string Type { get; set; }

        [JsonProperty("site_admin")]
        public bool SiteAdmin { get; set; }
    }

    public class Asset
    {
        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }
}
