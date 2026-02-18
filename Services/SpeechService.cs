using Windows.Media.SpeechRecognition;

namespace WinPrompter.Services;

/// <summary>
/// Wraps Windows on-device speech recognition for continuous dictation.
/// </summary>
public class SpeechService : IDisposable
{
    private SpeechRecognizer? _recognizer;
    private bool _isListening;

    /// <summary>Fired when words are recognized (may be partial).</summary>
    public event Action<string>? WordsRecognized;

    /// <summary>Fired when recognition status changes.</summary>
    public event Action<bool>? ListeningChanged;

    /// <summary>Fired when an error occurs.</summary>
    public event Action<string>? ErrorOccurred;

    public bool IsListening => _isListening;

    public async Task<bool> StartAsync()
    {
        if (_isListening) return true;

        try
        {
            App.LogCrash("SpeechDebug", new Exception("Creating SpeechRecognizer..."));
            _recognizer = new SpeechRecognizer();

            var constraint = new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation, "teleprompter");
            _recognizer.Constraints.Add(constraint);

            App.LogCrash("SpeechDebug", new Exception("Compiling constraints..."));
            var result = await _recognizer.CompileConstraintsAsync();
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                var msg = $"Speech compile failed: {result.Status}";
                App.LogCrash("SpeechCompile", new Exception(msg));
                ErrorOccurred?.Invoke(msg);
                return false;
            }

            // Wire up continuous recognition events
            _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
            _recognizer.ContinuousRecognitionSession.Completed += OnCompleted;
            _recognizer.HypothesisGenerated += OnHypothesisGenerated;

            App.LogCrash("SpeechDebug", new Exception("Starting continuous session..."));
            await _recognizer.ContinuousRecognitionSession.StartAsync();
            _isListening = true;
            ListeningChanged?.Invoke(true);
            App.LogCrash("SpeechDebug", new Exception("Voice recognition started successfully"));
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            var msg = "Microphone access denied — enable in Windows Settings > Privacy > Microphone";
            App.LogCrash("SpeechAuth", ex);
            ErrorOccurred?.Invoke(msg);
            return false;
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80045509))
        {
            App.LogCrash("SpeechPrivacy", ex);
            ErrorOccurred?.Invoke("Speech recognition not available — enable in Settings > Privacy > Speech");
            return false;
        }
        catch (Exception ex)
        {
            App.LogCrash("SpeechStart", ex);
            ErrorOccurred?.Invoke($"Speech error (0x{ex.HResult:X}): {ex.Message}");
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (!_isListening || _recognizer == null) return;

        try
        {
            await _recognizer.ContinuousRecognitionSession.StopAsync();
        }
        catch { }

        _isListening = false;
        ListeningChanged?.Invoke(false);
    }

    private void OnResultGenerated(SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (args.Result.Status == SpeechRecognitionResultStatus.Success &&
            !string.IsNullOrWhiteSpace(args.Result.Text))
        {
            WordsRecognized?.Invoke(args.Result.Text);
        }
    }

    private void OnHypothesisGenerated(SpeechRecognizer sender,
        SpeechRecognitionHypothesisGeneratedEventArgs args)
    {
        // Hypotheses give faster (but less accurate) partial results
        if (!string.IsNullOrWhiteSpace(args.Hypothesis.Text))
        {
            WordsRecognized?.Invoke(args.Hypothesis.Text);
        }
    }

    private void OnCompleted(SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionCompletedEventArgs args)
    {
        _isListening = false;
        ListeningChanged?.Invoke(false);
        App.LogCrash("SpeechCompleted", new Exception($"Session ended: Status={args.Status}"));

        if (args.Status != SpeechRecognitionResultStatus.Success)
        {
            ErrorOccurred?.Invoke($"Recognition ended: {args.Status}");
        }
    }

    public void Dispose()
    {
        if (_isListening && _recognizer != null)
        {
            try { _recognizer.ContinuousRecognitionSession.StopAsync().AsTask().Wait(2000); }
            catch { }
        }
        _recognizer?.Dispose();
        _recognizer = null;
        GC.SuppressFinalize(this);
    }
}
