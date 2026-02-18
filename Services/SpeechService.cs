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
            _recognizer = new SpeechRecognizer();

            // Use free-form dictation (on-device when speech pack is installed)
            var constraint = new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation, "teleprompter");
            _recognizer.Constraints.Add(constraint);

            var result = await _recognizer.CompileConstraintsAsync();
            if (result.Status != SpeechRecognitionResultStatus.Success)
            {
                ErrorOccurred?.Invoke($"Failed to compile constraints: {result.Status}");
                return false;
            }

            // Wire up continuous recognition events
            _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
            _recognizer.ContinuousRecognitionSession.Completed += OnCompleted;
            _recognizer.HypothesisGenerated += OnHypothesisGenerated;

            await _recognizer.ContinuousRecognitionSession.StartAsync();
            _isListening = true;
            ListeningChanged?.Invoke(true);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Speech recognition error: {ex.Message}");
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
