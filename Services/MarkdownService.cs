using Markdig;
using System.Text.RegularExpressions;

namespace WinPrompter.Services;

public partial class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public string ConvertToHtml(string markdown)
    {
        var processed = ProcessCueMarkers(markdown);
        return Markdig.Markdown.ToHtml(processed, _pipeline);
    }

    public List<(string Word, int CharOffset)> ExtractWords(string markdown)
    {
        var plainText = Markdig.Markdown.ToPlainText(markdown, _pipeline);
        var words = new List<(string, int)>();
        int i = 0;
        while (i < plainText.Length)
        {
            if (char.IsLetterOrDigit(plainText[i]))
            {
                int start = i;
                while (i < plainText.Length && char.IsLetterOrDigit(plainText[i]))
                    i++;
                words.Add((plainText[start..i].ToLowerInvariant(), start));
            }
            else
            {
                i++;
            }
        }
        return words;
    }

    private static string ProcessCueMarkers(string markdown)
    {
        return CueMarkerRegex().Replace(markdown, match =>
        {
            var duration = match.Groups[1].Value;
            return $"<div class=\"cue-marker\" data-duration=\"{duration}\"></div>";
        });
    }

    [GeneratedRegex(@"<!--\s*pause\s+(\d+(?:\.\d+)?)\s*s?\s*-->")]
    private static partial Regex CueMarkerRegex();
}
