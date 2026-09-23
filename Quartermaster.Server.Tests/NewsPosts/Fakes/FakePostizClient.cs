using Quartermaster.Server.NewsPosts.Postiz;

namespace Quartermaster.Server.Tests.NewsPosts.Fakes;

public class FakePostizClient : IPostizClient {
    public static List<PostizChannel> Channels { get; } = [];
    public static List<(string Text, List<string> ChannelIds, DateTime? At)> Created { get; } = [];
    public static bool FailNext { get; set; }
    public static Exception? ThrowNext { get; set; }

    public static void Reset() {
        Channels.Clear();
        Created.Clear();
        FailNext = false;
        ThrowNext = null;
    }

    public Task<List<PostizChannel>> GetChannelsAsync(CancellationToken ct) => Task.FromResult(Channels.ToList());

    public Task<List<string>> CreatePostAsync(string text, IReadOnlyList<PostizChannel> channels, DateTime? scheduledForUtc, CancellationToken ct) {
        if (FailNext) {
            FailNext = false;
            throw new PostizException("Postiz /public/v1/posts returned 500: boom");
        }
        if (ThrowNext != null) {
            var ex = ThrowNext;
            ThrowNext = null;
            throw ex;
        }
        Created.Add((text, channels.Select(c => c.Id).ToList(), scheduledForUtc));
        return Task.FromResult(channels.Select(c => "post-" + c.Id).ToList());
    }
}
