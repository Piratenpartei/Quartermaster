using System;
using System.Collections.Generic;

namespace Quartermaster.Api.NewsPosts;

/// <summary>Mirrors Quartermaster.Data.NewsPosts.NewsItemStatus (same int values).</summary>
public enum NewsItemStatusDTO {
    New,
    Irrelevant,
    Relevant,
    Generated,
    Posting,
    Posted,
    Dismissed,
    Failed
}

public class NewsItemDTO {
    public Guid Id { get; set; }
    public string FeedName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Teaser { get; set; } = "";
    public bool HasFullText { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public NewsItemStatusDTO Status { get; set; }
    public string? TriageReason { get; set; }
    public string? LastError { get; set; }
    public List<PostCandidateDTO> Candidates { get; set; } = [];
}

public class PostCandidateDTO {
    public Guid Id { get; set; }
    public string Text { get; set; } = "";
    public string SectionHeadingPath { get; set; } = "";
    public string SectionBody { get; set; } = "";
    public string SectionUrl { get; set; } = "";
}

public class PostizChannelDTO {
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string? Profile { get; set; }
    public string? Picture { get; set; }
    public bool Disabled { get; set; }
}

public class PublishRequest {
    public Guid? CandidateId { get; set; }
    public string Text { get; set; } = "";
    public List<string> ChannelIds { get; set; } = [];

    /// <summary>Null publishes now. Must carry an offset so the server can convert it to UTC unambiguously.</summary>
    public DateTimeOffset? ScheduledFor { get; set; }
}

public class PostPublicationDTO {
    public Guid Id { get; set; }
    public Guid NewsItemId { get; set; }
    public string NewsTitle { get; set; } = "";
    public string FinalText { get; set; } = "";
    public List<string> ChannelIds { get; set; } = [];
    public DateTimeOffset? ScheduledFor { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public string ApprovedBy { get; set; } = "";
    public DateTimeOffset ApprovedAt { get; set; }
}

public class NewsFeedDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";

    /// <summary>0 = RSS/Atom, 1 = tagesschau API.</summary>
    public int Kind { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? LastPolledAt { get; set; }
    public string? LastError { get; set; }
}

public class ProgrammeSourceDTO {
    public Guid Id { get; set; }
    public string WikiPageTitle { get; set; } = "";
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastError { get; set; }
    public int SectionCount { get; set; }
}

public class ProgrammeSourceCreateRequest {
    public string WikiPageTitle { get; set; } = "";
}

public class NewsPostsStatusDTO {
    public bool Enabled { get; set; }
    public bool MistralConfigured { get; set; }
    public bool PostizConfigured { get; set; }

    /// <summary>True when a live channel listing succeeded; Mistral is only checked for a configured key, since a probe costs tokens.</summary>
    public bool PostizReachable { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public string? LastRunError { get; set; }
    public int IndexedSections { get; set; }
    public Dictionary<string, int> ItemsByStatus { get; set; } = [];
}
