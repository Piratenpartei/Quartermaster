using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

/// <summary>A member-facing list: a table from medium width up, one stacked row per entry below it, so nothing scrolls sideways on a phone.</summary>
public partial class ResponsiveList<TItem> {
    [Parameter, EditorRequired]
    public IReadOnlyList<TItem> Items { get; set; } = [];

    /// <summary>The table's header row (<c>tr</c> with <c>th</c> cells).</summary>
    [Parameter, EditorRequired]
    public RenderFragment Header { get; set; } = default!;

    /// <summary>One table row (<c>tr</c>) per entry.</summary>
    [Parameter, EditorRequired]
    public RenderFragment<TItem> Row { get; set; } = default!;

    /// <summary>The content of one stacked row on a phone: the title first, details below it.</summary>
    [Parameter, EditorRequired]
    public RenderFragment<TItem> Stacked { get; set; } = default!;
}
