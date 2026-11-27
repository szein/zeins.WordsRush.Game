namespace zeins.WordsRush.Client.Services;

public interface IAudioRecorder
{
    ValueTask StartAsync();
    ValueTask<byte[]> StopAsync();
    ValueTask DisposeAsync();
}
