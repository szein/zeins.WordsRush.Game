using Microsoft.JSInterop;

namespace zeins.WordsRush.Client.Services;

public sealed class NAudioRecorder : IAudioRecorder
{
    private readonly IJSRuntime _js;
    private readonly ILogger<IAudioRecorder> _logger;
    private IJSObjectReference? _module;

    public NAudioRecorder(IJSRuntime js, ILogger<IAudioRecorder> logger) 
    {
        _js = js;
        _logger = logger ?? throw new ArgumentNullException("${ typeof(ILogger<IAudioRecorder>) }");
    }

    public async ValueTask StartAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import", "./audioCapture.js");
        await _module.InvokeVoidAsync("startRecording");
    }

    public async ValueTask<byte[]> StopAsync()
    {
        _logger.LogInformation("Stop Recording!");
        if (_module is null)
            return [];

        _logger.LogInformation("Module is not null: {0}", _module);

        var result = await _module.InvokeAsync<byte[]>("stopRecording");
        return result ?? [];
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("disposeRecording");
            await _module.DisposeAsync();
            _module = null;
        }
    }
}
