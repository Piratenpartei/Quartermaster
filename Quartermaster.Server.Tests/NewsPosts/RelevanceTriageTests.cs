using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts;
using Quartermaster.Server.NewsPosts.Generation;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

public class RelevanceTriageTests {
	private static readonly NewsItem Item = new() { Title = "Alterskontrolle für alle", Teaser = "EU plant Ausweispflicht" };
	private static readonly List<ProgrammeSection> Passages = [new ProgrammeSection { HeadingPath = "P > Jugendschutz", Body = "Aufklärung statt Verbote." }];

	[Test]
	public async Task Parses_verdict_and_sends_passages_to_triage_model() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue("""{"relevant": true, "reason": "Betrifft Jugendschutz"}""");
		var triage = new RelevanceTriage(mistral, new NewsPostsSettings { TriageModel = "small" });

		var result = await triage.RunAsync(Item, Passages, CancellationToken.None);

		await Assert.That(result).IsEqualTo(new TriageResult(true, "Betrifft Jugendschutz"));
		await Assert.That(mistral.ChatCalls[0].Model).IsEqualTo("small");
		await Assert.That(mistral.ChatCalls[0].User).Contains("Aufklärung statt Verbote.");
	}

	[Test]
	public async Task Fenced_json_is_accepted() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue("```json\n{\"relevant\": false, \"reason\": \"Sport\"}\n```");
		var triage = new RelevanceTriage(mistral, new NewsPostsSettings());

		var result = await triage.RunAsync(Item, Passages, CancellationToken.None);

		await Assert.That(result.Relevant).IsFalse();
	}

	[Test]
	public async Task Non_json_answer_throws_MistralException() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue("Ja, das ist relevant.");
		var triage = new RelevanceTriage(mistral, new NewsPostsSettings());

		await Assert.ThrowsAsync<MistralException>(() => triage.RunAsync(Item, Passages, CancellationToken.None));
	}

	[Test]
	public async Task Non_string_reason_becomes_empty_reason() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue("""{"relevant": true, "reason": 5}""");
		var triage = new RelevanceTriage(mistral, new NewsPostsSettings());

		var result = await triage.RunAsync(Item, Passages, CancellationToken.None);

		await Assert.That(result).IsEqualTo(new TriageResult(true, ""));
	}

	[Test]
	public async Task Array_root_throws_MistralException() {
		var mistral = new FakeMistralClient();
		mistral.ChatAnswers.Enqueue("[]");
		var triage = new RelevanceTriage(mistral, new NewsPostsSettings());

		await Assert.ThrowsAsync<MistralException>(() => triage.RunAsync(Item, Passages, CancellationToken.None));
	}
}
