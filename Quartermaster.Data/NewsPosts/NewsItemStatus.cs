namespace Quartermaster.Data.NewsPosts;

/// <summary>Persisted as int; append new values only.</summary>
public enum NewsItemStatus {
    New,
    Irrelevant,
    Relevant,
    Generated,
    Posting,
    Posted,
    Dismissed,
    Failed
}
