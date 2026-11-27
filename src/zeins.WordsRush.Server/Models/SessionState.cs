namespace zeins.WordsRush.Server.Models;

public sealed class SessionState
{
    public required string SessionId { get; init; }
    public HashSet<string> ConnectionIds { get; } = [];
    public HashSet<string> DisplayNames { get; } = [];
}
