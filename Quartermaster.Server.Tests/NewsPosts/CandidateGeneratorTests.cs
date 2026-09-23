using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Generation;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class CandidateGeneratorTests {
	private static readonly ProgrammeSection Section = new() { HeadingPath = "P > Verkehr", Body = "ÖPNV fahrscheinfrei." };
	private static readonly NewsItem Item = new() { Title = "Tickets teurer", Teaser = "4 Euro", ArticleText = "Einzelfahrschein 4 Euro." };

	private static NewsPostsSettings Settings() => new() { GenerationModel = "medium", CandidateCount = 3, GenerationPrompt = "Sei pointiert." };

	[Test]
	public async Task Returns_valid_candidates_and_sends_rules_and_article() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue($$"""{"candidates":[{"text":"Bus fahrscheinfrei!","sectionId":"{{Section.Id}}"}]}""");
		var generator = new CandidateGenerator(mistral, Settings());

		var result = await generator.GenerateAsync(Item, [Section], CancellationToken.None);

		await Assert.That(result.Single()).IsEqualTo(new GeneratedCandidate("Bus fahrscheinfrei!", Section.Id));
		await Assert.That(mistral.ChatCalls[0].Model).IsEqualTo("medium");
		await Assert.That(mistral.ChatCalls[0].System).Contains("Sei pointiert.");
		await Assert.That(mistral.ChatCalls[0].User).Contains("Einzelfahrschein 4 Euro.");
		await Assert.That(mistral.ChatCalls[0].User).Contains(Section.Id.ToString());
	}

	[Test]
	public async Task Empty_candidate_list_is_valid() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue("""{"candidates":[]}""");
		var generator = new CandidateGenerator(mistral, Settings());

		await Assert.That(await generator.GenerateAsync(Item, [Section], CancellationToken.None)).IsEmpty();
	}

	[Test]
	public async Task Validator_drops_too_long_unknown_section_blank_and_extra_candidates() {
		var other = Guid.NewGuid();
		var raw = new List<GeneratedCandidate> {
			new("ok 1", Section.Id),
			new(new string('x', 281), Section.Id),
			new("fremde Stelle", other),
			new("   ", Section.Id),
			new("ok 2", Section.Id),
			new("ok 3", Section.Id),
			new("ok 4", Section.Id)
		};

		var valid = CandidateValidator.Validate(raw, [Section.Id], 3);

		await Assert.That(valid.Select(c => c.Text).ToList()).IsEquivalentTo(new List<string> { "ok 1", "ok 2", "ok 3" });
	}

	[Test]
	public async Task Wrong_typed_fields_and_non_object_entries_are_discarded() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue($$"""{"candidates":[{"text":123,"sectionId":"{{Section.Id}}"},{"text":"ok","sectionId":42},{"text":"gut","sectionId":"{{Section.Id}}"}, "x"]}""");
		var generator = new CandidateGenerator(mistral, Settings());

		var result = await generator.GenerateAsync(Item, [Section], CancellationToken.None);

		await Assert.That(result.Single()).IsEqualTo(new GeneratedCandidate("gut", Section.Id));
	}

	[Test]
	public async Task Missing_candidates_property_throws_MistralException() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue("""{"posts":[]}""");
		var generator = new CandidateGenerator(mistral, Settings());

		await Assert.ThrowsAsync<MistralException>(() => generator.GenerateAsync(Item, [Section], CancellationToken.None));
	}
}
