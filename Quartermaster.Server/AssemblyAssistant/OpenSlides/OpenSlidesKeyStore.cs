using System.Collections.Generic;
using System.Text.Json;

namespace Quartermaster.Server.AssemblyAssistant.OpenSlides;

/// <summary>
/// The flat key map of an autoupdate stream ("poll/7/state" -> value). The first line is the full map, later lines carry changed
/// keys only, null removes a key and an empty object is a heartbeat.
/// </summary>
public class OpenSlidesKeyStore {
    private readonly Dictionary<string, JsonElement> _values = new();

    public bool Apply(JsonElement line) {
        if (line.ValueKind != JsonValueKind.Object)
            return false;

        var changed = false;
        foreach (var property in line.EnumerateObject()) {
            if (property.Value.ValueKind == JsonValueKind.Null) {
                changed |= _values.Remove(property.Name);
            } else {
                _values[property.Name] = property.Value.Clone();
                changed = true;
            }
        }

        return changed;
    }

    public JsonElement? Get(string key) {
        return _values.TryGetValue(key, out var value) ? value : null;
    }
}
