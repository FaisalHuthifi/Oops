using System.Windows.Automation;

namespace Oops.Services;

internal static class AutomationHelper
{
    internal static string? ReadSelection(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject))
            return null;

        var textPattern = (TextPattern)patternObject;
        var ranges = textPattern.GetSelection();
        if (ranges == null || ranges.Length == 0)
            return null;

        var text = ranges[0].GetText(-1);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    internal static bool IsReadOnly(AutomationElement element)
    {
        try
        {
            return element.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObject) &&
                   ((ValuePattern)patternObject).Current.IsReadOnly;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Confirms whether <paramref name="text"/> is present in the target. Returns
    /// <c>null</c> when the target exposes no readable text to check against.
    /// </summary>
    internal static bool? TryConfirmText(AutomationElement element, string text)
    {
        try
        {
            var readable = false;

            foreach (var candidate in WalkSelfAndParents(element))
            {
                if (!candidate.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObject))
                    continue;

                var value = ((ValuePattern)patternObject).Current.Value;
                if (value is null)
                    continue;

                readable = true;
                if (value.Contains(text, StringComparison.Ordinal))
                    return true;
            }

            return readable ? false : null;
        }
        catch
        {
            return null;
        }
    }

    internal static bool TryReplaceSelection(AutomationElement element, string originalText, string newText)
    {
        if (string.IsNullOrEmpty(newText))
            return false;

        try
        {
            foreach (var candidate in WalkSelfAndParents(element))
            {
                if (!candidate.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObject))
                    continue;

                var valuePattern = (ValuePattern)patternObject;
                if (valuePattern.Current.IsReadOnly)
                    continue;

                var current = valuePattern.Current.Value ?? string.Empty;
                var index = current.IndexOf(originalText, StringComparison.Ordinal);
                if (index < 0)
                    continue;

                var updated = string.Concat(
                    current.AsSpan(0, index),
                    newText,
                    current.AsSpan(index + originalText.Length));

                valuePattern.SetValue(updated);

                var after = valuePattern.Current.Value ?? string.Empty;
                return after.Contains(newText, StringComparison.Ordinal);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static IEnumerable<AutomationElement> WalkSelfAndParents(AutomationElement start)
    {
        yield return start;

        var walker = TreeWalker.ControlViewWalker;
        for (var parent = walker.GetParent(start); parent != null; parent = walker.GetParent(parent))
            yield return parent;
    }
}
