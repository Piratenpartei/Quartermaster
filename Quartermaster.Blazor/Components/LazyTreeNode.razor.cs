using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

/// <summary>
/// Generic lazy-load tree node: collapses/expands on click, lazily fetches children
/// via the consumer-supplied <see cref="LoadChildren"/> on first expand, and renders
/// the per-row content via the consumer's <see cref="ItemContent"/> fragment.
/// <para>
/// Replaces the per-domain <c>ChapterTreeNode</c> / <c>TreeNode</c> components — every
/// difference between them now lives in the consumer's render fragment, not duplicated
/// toggle/spinner/chevron markup.
/// </para>
/// </summary>
public partial class LazyTreeNode<T> : ComponentBase {
    [Parameter]
    public required LazyTreeNodeModel<T> Node { get; set; }

    [Parameter]
    public required Func<T, Task<List<T>>> LoadChildren { get; set; }

    [Parameter]
    public required RenderFragment<T> ItemContent { get; set; }

    private async Task Toggle() {
        if (Node.Loading)
            return;

        if (!Node.Expanded && Node.Children == null) {
            Node.Loading = true;
            StateHasChanged();
            var children = await LoadChildren(Node.Value);
            Node.Children = children.Select(c => new LazyTreeNodeModel<T>(c)).ToList();
            Node.Loading = false;
            Node.IsLeaf = Node.Children.Count == 0;
        }

        if (!Node.IsLeaf)
            Node.Expanded = !Node.Expanded;
        StateHasChanged();
    }
}

public class LazyTreeNodeModel<T> {
    public T Value { get; }
    public List<LazyTreeNodeModel<T>>? Children { get; set; }
    public bool Expanded { get; set; }
    public bool Loading { get; set; }
    public bool IsLeaf { get; set; }

    public LazyTreeNodeModel(T value) {
        Value = value;
    }
}
