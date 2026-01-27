using System.Collections.Concurrent;

namespace Application.Services.AiFlow;

/// <summary>
/// In-memory store for tool outputs too large to round-trip through the model.
/// Stores objects by handle (Guid). Scoped globally (singleton), separated per chat.
/// </summary>
public sealed class AiMemoryStore
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<Guid, object>> _byChat = new();

    public Guid Put(long chatId, object value)
    {
        var bag = _byChat.GetOrAdd(chatId, _ => new ConcurrentDictionary<Guid, object>());
        var id = Guid.NewGuid();
        bag[id] = value;
        return id;
    }

    public bool TryGet<T>(long chatId, Guid handle, out T? value)
    {
        value = default;
        if (!_byChat.TryGetValue(chatId, out var bag)) return false;
        if (!bag.TryGetValue(handle, out var obj)) return false;
        if (obj is not T t) return false;
        value = t;
        return true;
    }

    public void Remove(long chatId, Guid handle)
    {
        if (!_byChat.TryGetValue(chatId, out var bag)) return;
        bag.TryRemove(handle, out _);
    }
}
