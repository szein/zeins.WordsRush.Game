namespace zeins.WordsRush.Shared.Models;

public sealed record AudioChunkDto(
    string SessionId,
    string SpeakerName,
    byte[] Data,
    string ContentType = "audio/wav");
