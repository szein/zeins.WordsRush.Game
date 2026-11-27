namespace VoiceWordApp.Shared.Models;

public sealed record WordEventDto(
    string SessionId,
    string SpeakerName,
    string Word,
    double Confidence,
    DateTimeOffset RecognizedAt);
