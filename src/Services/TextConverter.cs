using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Oops.Models;

namespace Oops.Services;

public sealed class TextConverter
{
    private readonly Dictionary<char, char> _arabicToEnglish = new();
    private readonly Dictionary<char, char> _englishToArabic = new();

    public TextConverter()
    {
        LoadMapping();
    }

    private void LoadMapping()
    {
        var json = TryReadMapJson();
        if (json is null)
            return;

        var data = JsonSerializer.Deserialize<KeyboardMapData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data is null)
            return;

        foreach (var (key, value) in data.ArabicToEnglish)
        {
            if (key.Length == 1 && value.Length == 1)
                _arabicToEnglish[key[0]] = value[0];
        }

        foreach (var (key, value) in data.EnglishToArabic)
        {
            if (key.Length == 1 && value.Length == 1)
                _englishToArabic[key[0]] = value[0];
        }
    }

    private static string? TryReadMapJson()
    {
        var mapPath = Path.Combine(AppContext.BaseDirectory, "Resources", "KeyboardMap.json");
        if (File.Exists(mapPath))
            return File.ReadAllText(mapPath);

        var assembly = typeof(TextConverter).Assembly;
        using var stream = assembly.GetManifestResourceStream("Oops.Resources.KeyboardMap.json");
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public string Convert(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var direction = DetectDirection(text);
        var result = new StringBuilder(text.Length + 8);

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            // Handle "لا" ligature when converting English 'b' -> Arabic
            if (direction == ConversionDirection.ToArabic &&
                ch == 'b' &&
                i + 1 < text.Length &&
                IsLatinLetter(text[i + 1]))
            {
                result.Append('ل');
                result.Append('ا');
                continue;
            }

            // Handle Arabic ligature لا when converting to English
            if (direction == ConversionDirection.ToEnglish &&
                ch == 'ل' &&
                i + 1 < text.Length &&
                (text[i + 1] == 'ا' || text[i + 1] == 'أ' || text[i + 1] == 'إ' || text[i + 1] == 'آ'))
            {
                result.Append('b');
                i++;
                continue;
            }

            var converted = direction switch
            {
                ConversionDirection.ToEnglish when _arabicToEnglish.TryGetValue(ch, out var mapped) => mapped,
                ConversionDirection.ToArabic when _englishToArabic.TryGetValue(ch, out var mapped) => mapped,
                _ => ch
            };

            result.Append(converted);
        }

        return result.ToString();
    }

    private static ConversionDirection DetectDirection(string text)
    {
        var arabicCount = 0;
        var latinCount = 0;

        foreach (var ch in text)
        {
            if (IsArabicChar(ch))
                arabicCount++;
            else if (IsLatinLetter(ch))
                latinCount++;
        }

        return arabicCount >= latinCount
            ? ConversionDirection.ToEnglish
            : ConversionDirection.ToArabic;
    }

    private static bool IsArabicChar(char ch) =>
        ch is >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F';

    private static bool IsLatinLetter(char ch) =>
        ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private enum ConversionDirection
    {
        ToEnglish,
        ToArabic
    }
}
