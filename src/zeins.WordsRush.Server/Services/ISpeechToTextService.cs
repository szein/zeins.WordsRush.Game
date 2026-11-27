namespace VoiceWordApp.Server.Services;

public interface ISpeechToTextService
{
    Task<string?> RecognizeWordAsync(byte[] wavData, CancellationToken cancellationToken);
}
