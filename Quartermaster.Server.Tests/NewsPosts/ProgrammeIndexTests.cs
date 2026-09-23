using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.Tests.NewsPosts;

public class ProgrammeIndexTests {
    private static ProgrammeSection Section(float[] vector) => new() { Embedding = EmbeddingCodec.Encode(vector) };

    [Test]
    public async Task Codec_round_trips() {
        var vector = new[] { 0.25f, -1.5f, 3f };
        await Assert.That(EmbeddingCodec.Decode(EmbeddingCodec.Encode(vector))).IsEquivalentTo(vector);
    }

    [Test]
    public async Task TopMatches_orders_by_cosine_and_limits_count() {
        var a = Section([1f, 0f]);
        var b = Section([0.7f, 0.7f]);
        var c = Section([0f, 1f]);
        var index = new ProgrammeIndex();
        index.Reload([a, b, c]);

        var matches = index.TopMatches([1f, 0.1f], 2);

        await Assert.That(matches.Select(m => m.SectionId).ToList()).IsEquivalentTo(new List<Guid> { a.Id, b.Id });
        await Assert.That(matches[0].Similarity).IsGreaterThan(matches[1].Similarity);
    }

    [Test]
    public async Task Empty_index_returns_no_matches() {
        await Assert.That(new ProgrammeIndex().TopMatches([1f], 8)).IsEmpty();
    }
}
