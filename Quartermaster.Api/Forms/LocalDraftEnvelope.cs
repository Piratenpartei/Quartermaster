using System;

namespace Quartermaster.Api.Forms;

public enum LocalDraftStatus {
    Draft = 0,
    Submitted = 1
}

/// <summary>
/// Wrapper around a form's input DTO when serialised to <c>localStorage</c>. Mirrors the server-side
/// lifecycle: an <see cref="Id"/> distinguishes drafts of the same form type, a <see cref="Status"/>
/// flips from Draft to Submitted on PDF generation, and a small <see cref="Summary"/> lets the
/// "Meine Formulare" listing render a preview without unpacking the full <see cref="Data"/>.
/// Storage key: <c>qm-forms-{formKey}-{Id}</c>.
/// </summary>
public class LocalDraftEnvelope<T> {
    public Guid Id { get; set; }
    public LocalDraftStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public LocalDraftSummary Summary { get; set; } = new();
    public T? Data { get; set; }
}

public class LocalDraftMetadata {
    public Guid Id { get; set; }
    public LocalDraftStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public LocalDraftSummary Summary { get; set; } = new();
}

public class LocalDraftSummary {
    public string Chapter { get; set; } = "";
    public decimal Total { get; set; }
}

public static class LocalDraftKeys {
    public const string Prefix = "qm-forms-";

    public static string For(string formKey, Guid id) => $"{Prefix}{formKey}-{id}";
    public static string PrefixFor(string formKey) => $"{Prefix}{formKey}-";
}
