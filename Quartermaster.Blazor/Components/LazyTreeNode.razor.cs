using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components;

/// <summary>
/// Generic lazy-load tree node: lazy-fetches children via <see cref="LoadChildren"/> on
/// first expand, renders each row through the consumer's <see cref="ItemContent"/> fragment.
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
