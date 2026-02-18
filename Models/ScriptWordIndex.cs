namespace WinPrompter.Models;

/// <summary>
/// A word extracted from the script with its position info for voice matching.
/// </summary>
public record ScriptWord(string Text, int CharOffset, int WordIndex);

/// <summary>
/// Pre-processed script word sequence for voice-driven scroll sync.
/// </summary>
public class ScriptWordIndex
{
    public List<ScriptWord> Words { get; } = [];
    public string[] NormalizedWords { get; private set; } = [];

    public void Build(List<(string Word, int CharOffset)> words)
    {
        Words.Clear();
        for (int i = 0; i < words.Count; i++)
            Words.Add(new ScriptWord(words[i].Word, words[i].CharOffset, i));

        NormalizedWords = Words.Select(w => w.Text).ToArray();
    }

    /// <summary>
    /// Given a word index, return the character offset for scroll positioning.
    /// </summary>
    public int GetCharOffset(int wordIndex)
    {
        if (wordIndex < 0 || wordIndex >= Words.Count)
            return 0;
        return Words[wordIndex].CharOffset;
    }

    public int WordCount => Words.Count;
}
