using System.Net;
using System.Security.Claims;
using QuoteHub.Api.Dtos;

namespace QuoteHub.Api.Services;

public static class CardGenerator
{
    public static string GenerateSvg(QuoteResponse quote, string theme = "dark")
    {
        var isDark = !string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);

        var bg = isDark ? "#0f172a" : "#f8fafc";
        var border = isDark ? "#334155" : "#e2e8f0";
        var quoteTextCol = isDark ? "#f1f5f9" : "#0f172a";
        var authorCol = isDark ? "#38bdf8" : "#0284c7";
        var categoryCol = isDark ? "#94a3b8" : "#64748b";
        var badgeBg = isDark ? "#1e293b" : "#e2e8f0";
        var badgeText = isDark ? "#38bdf8" : "#0369a1";

        // HTML encode safe text
        var encodedText = WebUtility.HtmlEncode(quote.Text);
        var encodedAuthor = WebUtility.HtmlEncode(quote.Author);
        var encodedCategory = WebUtility.HtmlEncode(quote.Category);

        // Word-wrap SVG text lines (~50 chars per line)
        var lines = WrapText(encodedText, 52);
        var textElements = string.Join("\n", lines.Select((line, idx) =>
            $"    <tspan x=\"40\" dy=\"{(idx == 0 ? 0 : 28)}\">{line}</tspan>"));

        var tagsXml = string.Join(" ", quote.Tags.Take(3).Select((t, i) =>
            $"<rect x=\"{200 + i * 85}\" y=\"230\" width=\"75\" height=\"22\" rx=\"11\" fill=\"{badgeBg}\"/>" +
            $"<text x=\"{237 + i * 85}\" y=\"245\" font-size=\"11\" font-family=\"system-ui, sans-serif\" fill=\"{badgeText}\" text-anchor=\"middle\">#{WebUtility.HtmlEncode(t)}</text>"));

        return $"""
        <svg width="680" height="280" viewBox="0 0 680 280" fill="none" xmlns="http://www.w3.org/2000/svg">
          <rect width="680" height="280" rx="16" fill="{bg}" stroke="{border}" stroke-width="2"/>
          
          <!-- Category badge -->
          <rect x="40" y="32" width="90" height="24" rx="12" fill="{badgeBg}"/>
          <text x="85" y="48" font-size="11" font-weight="600" font-family=\"system-ui, sans-serif\" fill="{badgeText}" text-anchor="middle">{encodedCategory}</text>

          <!-- Quote symbol -->
          <text x="610" y="70" font-size="70" font-family="Georgia, serif" fill="{badgeBg}" opacity="0.6">“</text>

          <!-- Quote text -->
          <text x="40" y="105" font-size="19" font-family="system-ui, -apple-system, sans-serif" font-weight="500" fill="{quoteTextCol}">
        {textElements}
          </text>

          <!-- Author -->
          <text x="40" y="246" font-size="15" font-family="system-ui, sans-serif" font-weight="bold" fill="{authorCol}">— {encodedAuthor}</text>

          <!-- Tags -->
          {tagsXml}

          <!-- QuoteHub Brand footer -->
          <text x="640" y="246" font-size="11" font-family="system-ui, sans-serif" fill="{categoryCol}" text-anchor="end">QuoteHub API</text>
        </svg>
        """;
    }

    private static List<string> WrapText(string text, int maxLineLen)
    {
        var words = text.Split(' ');
        var lines = new List<string>();
        var current = "";

        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxLineLen)
            {
                if (!string.IsNullOrEmpty(current)) lines.Add(current);
                current = word;
            }
            else
            {
                current = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            }
        }

        if (!string.IsNullOrEmpty(current)) lines.Add(current);
        return lines.Take(4).ToList(); // Max 4 lines
    }
}
