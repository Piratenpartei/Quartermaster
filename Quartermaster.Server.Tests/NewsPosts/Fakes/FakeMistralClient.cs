using Quartermaster.Server.NewsPosts.Mistral;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

/// <summary>Deterministic embeddings: each input maps to a 3-dimensional vector chosen by keyword, so tests control similarity. Chat answers are queued per test.</summary>
public class FakeMistralClient : IMistralClient {
    public int EmbedCalls { get; private set; }
    public List<string> EmbeddedInputs { get; } = [];
    public Queue<string> ChatAnswers { get; } = new();
    public Queue<Exception> ChatExceptions { get; } = new();
    public List<(string Model, string System, string User)> ChatCalls { get; } = [];

    /// <summary>Fires right before an answer is handed back, so a test can simulate another
    /// request changing the item's status while this call is "in flight".</summary>
    public Action? OnBeforeChatAnswer { get; set; }

    public Task<List<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct) {
        EmbedCalls++;
        EmbeddedInputs.AddRange(inputs);
        return Task.FromResult(inputs.Select(Vector).ToList());
    }

    public Task<string> CompleteJsonAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct) {
        ChatCalls.Add((model, systemPrompt, userPrompt));
        OnBeforeChatAnswer?.Invoke();
        if (ChatExceptions.Count > 0)
            throw ChatExceptions.Dequeue();
        if (ChatAnswers.Count == 0)
            throw new MistralException("no answer queued");
        return Task.FromResult(ChatAnswers.Dequeue());
    }

    public static float[] Vector(string text) {
        if (text.Contains("Jugendschutz") || text.Contains("Alterskontrolle"))
            return [1f, 0f, 0f];
        if (text.Contains("ÖPNV") || text.Contains("Fahrschein"))
            return [0f, 1f, 0f];
        return [0f, 0f, 1f];
    }
}
