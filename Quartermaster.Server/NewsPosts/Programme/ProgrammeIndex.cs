using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Quartermaster.Data.NewsPosts;

namespace Quartermaster.Server.NewsPosts.Programme;

/// <summary>In-memory cosine index over all programme section embeddings. A few hundred vectors make a linear scan cheaper than any vector store.</summary>
public class ProgrammeIndex {
    private readonly Lock _lock = new();
    private List<(Guid Id, float[] Vector)> entries = [];

    public int Count {
        get {
            lock (_lock) {
                return entries.Count;
            }
        }
    }

    public void Reload(IEnumerable<ProgrammeSection> sections) {
        var loaded = sections
            .Where(s => s.Embedding.Length > 0)
            .Select(s => (s.Id, EmbeddingCodec.Decode(s.Embedding)))
            .ToList();
        lock (_lock) {
            entries = loaded;
        }
    }

    public List<SectionMatch> TopMatches(float[] query, int count) {
        List<(Guid Id, float[] Vector)> snapshot;
        lock (_lock) {
            snapshot = entries;
        }
        return snapshot
            .Select(e => new SectionMatch(e.Id, Cosine(query, e.Vector)))
            .OrderByDescending(m => m.Similarity)
            .Take(count)
            .ToList();
    }

    public static double Cosine(float[] a, float[] b) {
        var length = Math.Min(a.Length, b.Length);
        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < length; i++) {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        if (normA == 0 || normB == 0)
            return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
