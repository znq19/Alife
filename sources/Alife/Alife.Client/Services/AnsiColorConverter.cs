using System.Text;

namespace Alife.Components.Services;

public static class AnsiColorConverter
{
    static readonly Dictionary<int, string> ForegroundColors = new()
    {
        [30] = "#000000",
        [31] = "#cd3131",
        [32] = "#0dbc79",
        [33] = "#e5e510",
        [34] = "#2472c8",
        [35] = "#bc3fbc",
        [36] = "#11a8cd",
        [37] = "#e5e5e5",
        [90] = "#666666",
        [91] = "#f14c4c",
        [92] = "#23d18b",
        [93] = "#f5f543",
        [94] = "#3b8eea",
        [95] = "#d670d6",
        [96] = "#29b8db",
        [97] = "#e5e5e5",
    };

    public static IReadOnlyList<AnsiSegment> Parse(string text)
    {
        List<AnsiSegment> segments = new();
        StringBuilder sb = new();
        string currentColor = "";
        int i = 0;

        while (i < text.Length)
        {
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                if (sb.Length > 0)
                {
                    segments.Add(new AnsiSegment(sb.ToString(), currentColor));
                    sb.Clear();
                }

                int end = text.IndexOf('m', i + 2);
                if (end < 0) break;

                string seq = text.Substring(i + 2, end - (i + 2));
                foreach (string part in seq.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part, out int code))
                    {
                        if (code == 0)
                            currentColor = "";
                        else if (code == 39)
                            currentColor = "";
                        else if (ForegroundColors.TryGetValue(code, out string? color))
                            currentColor = color;
                    }
                }

                i = end + 1;
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }

        if (sb.Length > 0)
            segments.Add(new AnsiSegment(sb.ToString(), currentColor));

        if (segments.Count == 0)
            segments.Add(new AnsiSegment(text, ""));

        return segments;
    }

    public static string StripAnsi(string text)
    {
        StringBuilder sb = new(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                int end = text.IndexOf('m', i + 2);
                if (end < 0) break;
                i = end + 1;
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }
        return sb.ToString();
    }
}

public readonly record struct AnsiSegment(string Text, string CssColor);