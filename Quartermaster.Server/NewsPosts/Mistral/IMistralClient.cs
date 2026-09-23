using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Mistral;

public interface IMistralClient {
    /// <summary>Returns one vector per input, in input order.</summary>
    Task<List<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken ct);

    /// <summary>Runs a chat completion in JSON mode and returns the raw message content.</summary>
    Task<string> CompleteJsonAsync(string model, string systemPrompt, string userPrompt, CancellationToken ct);
}
