using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Chapters;

namespace Quartermaster.Blazor.Services;

/// <summary>Chapter names for the form's snapshot fields, fetched once per page.</summary>
public class ChapterNames {
    private readonly HttpClient _http;
    private Dictionary<Guid, string>? _names;

    public ChapterNames(HttpClient http) {
        _http = http;
    }

    public async Task<string> NameOfAsync(Guid chapterId) {
        if (_names == null) {
            try {
                var chapters = await _http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters") ?? [];
                _names = chapters.ToDictionary(c => c.Id, c => c.Name);
            } catch (HttpRequestException) {
                return "";
            }
        }
        return _names.TryGetValue(chapterId, out var name) ? name : "";
    }
}
