using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.NewsPosts.Postiz;

public interface IPostizClient {
    Task<List<PostizChannel>> GetChannelsAsync(CancellationToken ct);

    /// <summary>Creates one post per channel with the same text. <paramref name="scheduledForUtc"/> null means publish now. Returns the Postiz post ids.</summary>
    Task<List<string>> CreatePostAsync(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, CancellationToken ct);
}
