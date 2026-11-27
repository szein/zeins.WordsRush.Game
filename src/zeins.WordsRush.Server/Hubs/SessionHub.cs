using Microsoft.AspNetCore.SignalR;
using zeins.WordsRush.Server.Models;
using zeins.WordsRush.Shared.Contracts;
using zeins.WordsRush.Shared.Models;
using zeins.WordsRush.Server.Services;

namespace zeins.WordsRush.Server.Hubs;

public sealed class SessionHub : Hub<ISessionClient>
{
    private const int MaxAudioBytes = 2 * 1024 * 1024;
    private readonly ISessionManager _sessions;
    private readonly ISpeechToTextService _speech;
    private readonly ILogger<SessionHub> _logger;

    public SessionHub(ISessionManager sessions, ISpeechToTextService speech, ILogger<SessionHub> logger)
    {
        _sessions = sessions;
        _speech = speech;
        _logger = logger;
    }

    public async Task JoinSession(string sessionId, string displayName)
    {
        _logger.LogInformation("JoinSession {0} {1}", sessionId, displayName);
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(displayName))
        {
            await Clients.Caller.SessionError("Session ID and display name are required.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.Trim(), Context.ConnectionAborted);
        var state = _sessions.CreateOrJoin(sessionId, Context.ConnectionId, displayName);
        Context.Items["session"] = state;
        Context.Items["displayName"] = displayName.Trim();
        await Clients.Group(sessionId.Trim()).Joined(state.SessionId, displayName.Trim());
    }

    public async Task SendAudio(AudioChunkDto chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (string.IsNullOrWhiteSpace(chunk.SessionId) || chunk.Data.Length == 0)
        {
            await Clients.Caller.SessionError("Invalid audio chunk.");
            return;
        }
        if (chunk.Data.Length > MaxAudioBytes)
        {
            await Clients.Caller.SessionError("Audio chunk is too large.");
            return;
        }
        if (!_sessions.TryGet(chunk.SessionId, out var session) ||
            !session.ConnectionIds.Contains(Context.ConnectionId))
        {
            await Clients.Caller.SessionError("Join the session before sending audio.");
            return;
        }

        try
        {
            _logger.LogInformation("Start RecognizeWordAsync");
            var word = await _speech.RecognizeWordAsync(chunk.Data, Context.ConnectionAborted);
            if (string.IsNullOrWhiteSpace(word))
            {
                await Clients.Caller.SessionError("No recognized speech was detected.");
                return;
            }
            _logger.LogInformation("Word is not Null!");

            var wordEvent = new WordEventDto(
                session.SessionId,
                chunk.SpeakerName.Trim(),
                word.Trim(),
                1,
                DateTimeOffset.UtcNow);

            await Clients.Group(session.SessionId).WordRecognized(wordEvent);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process audio for session {SessionId}", chunk.SessionId);
            await Clients.Caller.SessionError("Audio processing failed.");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var session in Context.Items.Values.OfType<SessionState>().ToArray())
        {
            _sessions.Leave(session.SessionId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
