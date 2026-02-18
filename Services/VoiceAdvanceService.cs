using WinPrompter.Helpers;
using WinPrompter.Models;

namespace WinPrompter.Services;

/// <summary>
/// Orchestrates voice-driven auto-advance: listens to speech, fuzzy-matches against
/// the script, and drives scroll position.
/// </summary>
public class VoiceAdvanceService : IDisposable
{
    private readonly SpeechService _speech = new();
    private FuzzyMatcher? _matcher;
    private ScriptWordIndex? _wordIndex;

    /// <summary>Fired when the matched position advances. Provides the character offset to scroll to.</summary>
    public event Action<int>? PositionAdvanced;

    /// <summary>Fired when listening state changes.</summary>
    public event Action<bool>? ListeningChanged;

    /// <summary>Fired on errors.</summary>
    public event Action<string>? ErrorOccurred;

    /// <summary>Fired with the last recognized text (for UI display).</summary>
    public event Action<string>? RecognizedText;

    public bool IsListening => _speech.IsListening;

    public VoiceAdvanceService()
    {
        _speech.WordsRecognized += OnWordsRecognized;
        _speech.ListeningChanged += listening => ListeningChanged?.Invoke(listening);
        _speech.ErrorOccurred += err => ErrorOccurred?.Invoke(err);
    }

    /// <summary>
    /// Load a script's word index for matching.
    /// </summary>
    public void LoadScript(ScriptWordIndex wordIndex)
    {
        _wordIndex = wordIndex;
        _matcher = new FuzzyMatcher(wordIndex.NormalizedWords);
    }

    public async Task<bool> StartAsync()
    {
        if (_wordIndex == null || _matcher == null)
        {
            ErrorOccurred?.Invoke("No script loaded for voice advance");
            return false;
        }

        _matcher.Reset();
        return await _speech.StartAsync();
    }

    public async Task StopAsync()
    {
        await _speech.StopAsync();
    }

    private void OnWordsRecognized(string text)
    {
        if (_matcher == null || _wordIndex == null) return;

        RecognizedText?.Invoke(text);

        // Split recognized text into individual words
        var spokenWords = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int newPosition = _matcher.ProcessSpokenWords(spokenWords);

        if (newPosition >= 0)
        {
            int charOffset = _wordIndex.GetCharOffset(newPosition);
            PositionAdvanced?.Invoke(charOffset);
        }
    }

    public void Reset()
    {
        _matcher?.Reset();
    }

    public void Dispose()
    {
        _speech.Dispose();
        GC.SuppressFinalize(this);
    }
}
