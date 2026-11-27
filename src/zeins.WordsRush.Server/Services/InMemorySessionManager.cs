using System.Collections.Concurrent;
using VoiceWordApp.Server.Models;

namespace VoiceWordApp.Server.Services;

public sealed class InMemorySessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public SessionState CreateOrJoin(string sessionId, string connectionId, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var state = _sessions.GetOrAdd(sessionId.Trim(), static id => new SessionState { SessionId = id });
        lock (state)
        {
            state.ConnectionIds.Add(connectionId);
            state.DisplayNames.Add(displayName.Trim());
        }

        return state;
    }

    public SessionState? Leave(string sessionId, string connectionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            return null;

        lock (state)
        {
            state.ConnectionIds.Remove(connectionId);
            if (state.ConnectionIds.Count == 0)
                _sessions.TryRemove(sessionId, out _);
        }

        return state;
    }

    public bool TryGet(string sessionId, out SessionState session) =>
        _sessions.TryGetValue(sessionId, out session!);
}
