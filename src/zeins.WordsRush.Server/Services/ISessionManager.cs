using zeins.WordsRush.Server.Models;

namespace zeins.WordsRush.Server.Services;

public interface ISessionManager
{
    SessionState CreateOrJoin(string sessionId, string connectionId, string displayName);
    SessionState? Leave(string sessionId, string connectionId);
    bool TryGet(string sessionId, out SessionState session);
}
