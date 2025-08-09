using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace SpaceEditor.Services;

public class VersionChecker
{
    public class VersionInfo
    {
        public DateTime Published { get; set; }
        public string DisplayName { get; set; }
        public string WebURL { get; set; }
        public string DownloadURL { get; set; }
    }

    public static async Task<VersionInfo> GetLatestVersionInfo()
    {
        var queryURL = $"https://api.github.com/repos/InflexCZE/SpaceEditor/releases/latest";

        var latestReleaseJSON = await DownloadStringAsync(queryURL).ConfigureAwait(false);
        
        var data = JsonDocument.Parse(latestReleaseJSON).RootElement;
        var assets = data.GetProperty("assets").EnumerateArray();
        return new()
        {
            DisplayName = data.GetProperty("name").GetString()!,
            Published = data.GetProperty("published_at").GetDateTime(),
            WebURL = data.GetProperty("html_url").GetString()!,
            DownloadURL = assets.Select(x => x.GetProperty("browser_download_url").GetString()!).First(x => x.ToLowerInvariant().EndsWith("spaceeditor.zip"))
        };
    }

    private static async Task<string> DownloadStringAsync(string url)
    {
        const string AgentIdentifier = "github.com_InflexCZE_SpaceEditor";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.TryParseAdd(AgentIdentifier);

        return await client.GetStringAsync(url).ConfigureAwait(false);
    }
}