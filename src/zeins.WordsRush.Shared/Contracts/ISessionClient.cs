namespace VoiceWordApp.Shared.Contracts;

public interface ISessionClient
{
    Task Joined(string sessionId, string displayName);
    Task Left(string sessionId, string displayName);
    Task WordRecognized(Models.WordEventDto wordEvent);
    Task SessionError(string message);
}
