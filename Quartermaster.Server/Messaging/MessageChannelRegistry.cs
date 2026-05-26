using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Server.Messaging;

/// <summary>Lookup for registered <see cref="IMessageChannel"/> implementations by id.</summary>
public class MessageChannelRegistry {
    private readonly Dictionary<string, IMessageChannel> _byId;

    public MessageChannelRegistry(IEnumerable<IMessageChannel> channels) {
        _byId = channels.ToDictionary(c => c.Id, c => c);
    }

    public IReadOnlyCollection<IMessageChannel> All => _byId.Values;

    public IMessageChannel? Get(string id) {
        return _byId.TryGetValue(id, out var channel) ? channel : null;
    }
}
