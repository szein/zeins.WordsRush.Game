using Microsoft.Extensions.Options;
using Whisper.net;

namespace zeins.WordsRush.Server.Services;

public sealed class WhisperSttOptions
{
    public string? ModelPath { get; init; }
}

public sealed class WhisperSttService : ISpeechToTextService
{
    private readonly ILogger<WhisperSttService> _logger;
    private readonly string? _modelPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private WhisperFactory? _factory;

    public WhisperSttService(IOptions<WhisperSttOptions> options, ILogger<WhisperSttService> logger)
    {
        _modelPath = options.Value.ModelPath;
        _logger = logger;
    }

    public async Task<string?> RecognizeWordAsync(byte[] wavData, CancellationToken cancellationToken)
    {
        _logger.LogInformation("RecognizeWordAsync Byte Length {0}", wavData?.Length);

        if (string.IsNullOrWhiteSpace(_modelPath))
        {
            _logger.LogWarning("Speech recognition is unavailable because Whisper:ModelPath is not configured");
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            _factory ??= WhisperFactory.FromPath(_modelPath);
            await using var processor = _factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            await using var stream = new MemoryStream(wavData, writable: false);
            var segments = processor.ProcessAsync(stream, cancellationToken);
            var words = new List<string>();

            await foreach (var segment in segments.WithCancellation(cancellationToken))
            {
                words.Add(segment.Text);
            }

            var result = string.Concat(words).Trim();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
