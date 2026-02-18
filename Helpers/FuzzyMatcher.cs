namespace WinPrompter.Helpers;

/// <summary>
/// Sliding-window fuzzy matcher using Levenshtein distance.
/// Matches spoken words against the script word sequence.
/// </summary>
public class FuzzyMatcher
{
    private readonly string[] _scriptWords;
    private int _currentPosition;
    private int _consecutiveMatches;
    private readonly int _maxDistance;
    private readonly int _requiredConsecutive;
    private readonly int _lookAheadWindow;

    public int CurrentPosition => _currentPosition;

    /// <param name="scriptWords">Normalized (lowercase) words from the script</param>
    /// <param name="maxDistance">Max Levenshtein distance for a word to be considered a match (default 2)</param>
    /// <param name="requiredConsecutive">Required consecutive matches before advancing (default 2)</param>
    /// <param name="lookAheadWindow">How far ahead to search from current position (default 15)</param>
    public FuzzyMatcher(string[] scriptWords, int maxDistance = 2, int requiredConsecutive = 2, int lookAheadWindow = 15)
    {
        _scriptWords = scriptWords;
        _maxDistance = maxDistance;
        _requiredConsecutive = requiredConsecutive;
        _lookAheadWindow = lookAheadWindow;
    }

    /// <summary>
    /// Process a batch of recognized words and return the new script position if advanced.
    /// Returns -1 if no advancement.
    /// </summary>
    public int ProcessSpokenWords(string[] spokenWords)
    {
        int bestMatchPos = -1;

        foreach (var spoken in spokenWords)
        {
            var normalized = spoken.ToLowerInvariant().Trim();
            if (string.IsNullOrEmpty(normalized) || normalized.Length < 2)
                continue;

            int matchIndex = FindBestMatch(normalized);
            if (matchIndex >= 0)
            {
                _consecutiveMatches++;
                if (_consecutiveMatches >= _requiredConsecutive && matchIndex > _currentPosition)
                {
                    _currentPosition = matchIndex;
                    bestMatchPos = matchIndex;
                    _consecutiveMatches = 0;
                }
            }
            else
            {
                // Allow some tolerance — don't reset on single miss
                if (_consecutiveMatches > 0)
                    _consecutiveMatches--;
            }
        }

        return bestMatchPos;
    }

    /// <summary>
    /// Find the best matching word in the look-ahead window.
    /// </summary>
    private int FindBestMatch(string spoken)
    {
        int searchStart = Math.Max(0, _currentPosition - 2);
        int searchEnd = Math.Min(_scriptWords.Length, _currentPosition + _lookAheadWindow);

        int bestIndex = -1;
        int bestDistance = int.MaxValue;

        for (int i = searchStart; i < searchEnd; i++)
        {
            int distance = LevenshteinDistance(spoken, _scriptWords[i]);
            if (distance <= _maxDistance && distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Reset the matcher to the beginning of the script.
    /// </summary>
    public void Reset()
    {
        _currentPosition = 0;
        _consecutiveMatches = 0;
    }

    /// <summary>
    /// Compute Levenshtein edit distance between two strings.
    /// </summary>
    public static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        // Quick rejection for very different lengths
        int lenDiff = Math.Abs(a.Length - b.Length);
        if (lenDiff > 3) return lenDiff;

        int m = a.Length, n = b.Length;
        Span<int> prev = stackalloc int[n + 1];
        Span<int> curr = stackalloc int[n + 1];

        for (int j = 0; j <= n; j++) prev[j] = j;

        for (int i = 1; i <= m; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= n; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            var temp = prev;
            prev = curr;
            curr = temp;
        }

        return prev[n];
    }
}
