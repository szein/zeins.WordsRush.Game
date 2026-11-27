using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using VoiceWordApp.Client.Services;
using VoiceWordApp.Shared.Models;

public sealed class DynamicAudioCapture : IAsyncDisposable
{
    private readonly IAudioRecorder _recorder;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SessionHubClient _sessionHub;
    private readonly ILogger<DynamicAudioCapture> _logger;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _isCapturing;
    private string _sessionId = "";
    private string _userName = "";

    public DynamicAudioCapture(IAudioRecorder recorder, SessionHubClient sessionHub, ILogger<DynamicAudioCapture> logger)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _sessionHub = sessionHub ?? throw new ArgumentNullException(nameof(sessionHub));
        _logger = logger ??throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartDynamicRecordingAsync(string sessionId, string userName, int intervalMs = 1500, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StartDynamicRecordingAsync");
        if (intervalMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalMs));
        }
        _sessionId = sessionId;
        _userName = userName;

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isCapturing)
            {
                return;
            }

            _isCapturing = true;
            await _recorder.StartAsync();
            _logger.LogInformation("Recording Started");

            _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
            _loopTask = RunCaptureLoopAsync(_timer, _loopCts.Token);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopRecordingAndSendAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? ctsToCancel;
        Task? loopTaskToAwait;
        _logger.LogInformation("StopRecordingAndSendAsync");
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ctsToCancel = _loopCts;
            loopTaskToAwait = _loopTask;
            _loopCts = null;
            _loopTask = null;
        }
        finally
        {
            _stateLock.Release();
        }

        if (ctsToCancel is not null)
        {
            await ctsToCancel.CancelAsync().ConfigureAwait(false);
            if (loopTaskToAwait is not null)
            {
                try
                {
                    await loopTaskToAwait.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            ctsToCancel.Dispose();
        }

        byte[]? finalChunk = null;

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _timer?.Dispose();
            _timer = null;

            if (!_isCapturing)
            {
                return;
            }

            _isCapturing = false;
            finalChunk = await _recorder.StopAsync();
            _logger.LogInformation("Stoped");
        }
        finally
        {
            _stateLock.Release();
        }
        _logger.LogInformation("Send audio");
        await SendAudioChunkAsync(finalChunk, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunCaptureLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                byte[]? chunk = null;

                await _stateLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (!_isCapturing)
                    {
                        return;
                    }

                    chunk = await _recorder.StopAsync();
                    await _recorder.StartAsync();
                }
                finally
                {
                    _stateLock.Release();
                }

                await SendAudioChunkAsync(chunk, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SendAudioChunkAsync(byte[]? audioData, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SendAudioChunkAsync audioLength {0}", audioData?.Length);
        if (audioData is null || audioData.Length == 0)
        {
            return;
        }

        try
        {
           await _sessionHub.SendAudioAsync(new AudioChunkDto(
                _sessionId.Trim(),
                _userName.Trim(),
                audioData));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Audio upload failed: {ex.Message}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
{
    CancellationTokenSource? ctsToCancel;
    Task? loopTaskToAwait;
    PeriodicTimer? timerToDispose;

    await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        if (!_isCapturing && _loopTask is null)
        {
            return;
        }

        _isCapturing = false;

        ctsToCancel = _loopCts;
        loopTaskToAwait = _loopTask;
        timerToDispose = _timer;

        _loopCts = null;
        _loopTask = null;
        _timer = null;
    }
    finally
    {
        _stateLock.Release();
    }

    // Cancel loop and any in-flight uploads using _loopCts.Token
    if (ctsToCancel is not null)
    {
        await ctsToCancel.CancelAsync().ConfigureAwait(false);
    }

    if (loopTaskToAwait is not null)
    {
        try
        {
            await loopTaskToAwait.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
    }

    timerToDispose?.Dispose();
    ctsToCancel?.Dispose();

    // Stop recorder and discard remaining buffered audio
    await _stateLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
    try
    {
        await _recorder.StopAsync();
    }
    finally
    {
        _stateLock.Release();
    }
}
    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopRecordingAndSendAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _stateLock.Dispose();
        }
    }
}