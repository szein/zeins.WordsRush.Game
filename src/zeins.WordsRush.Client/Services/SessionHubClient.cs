using Microsoft.AspNetCore.SignalR.Client;
using VoiceWordApp.Shared.Models;

namespace VoiceWordApp.Client.Services;

public sealed class SessionHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public SessionHubClient(string serverUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl.TrimEnd('/')}/hubs/session")
            .WithAutomaticReconnect()
            .Build();
    }

    public event Action<string, string>? Joined;
    public event Action<WordEventDto>? WordRecognized;
    public event Action<string>? Error;

    public async Task StartAsync()
    {
        _connection.On<string, string>("Joined", (sessionId, name) => Joined?.Invoke(sessionId, name));
        _connection.On<WordEventDto>("WordRecognized", word => WordRecognized?.Invoke(word));
        _connection.On<string>("SessionError", message => Error?.Invoke(message));
        await _connection.StartAsync();
    }

    public Task JoinSessionAsync(string sessionId, string displayName) =>
        _connection.InvokeAsync("JoinSession", sessionId, displayName);

    public Task SendAudioAsync(AudioChunkDto chunk) =>
        _connection.InvokeAsync("SendAudio", chunk);

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
