using System;
using System.Collections.Generic;

namespace LlamaForge.Models
{
    public class GitHubRelease
    {
        public string TagName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public List<GitHubAsset> Assets { get; set; } = new();
        public string Body { get; set; } = string.Empty;
    }

    public class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}
