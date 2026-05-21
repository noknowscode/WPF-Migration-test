using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace EventLookUno.Models;

public static class TextHelper
{
    public static string FormatXml(string xmlString)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlString);

        var sb = new StringBuilder();
        using var tr = new StringWriter(sb);
        using var wr = new XmlTextWriter(tr) { Formatting = Formatting.Indented };
        doc.Save(wr);
        return sb.ToString();
    }

    /// <summary>
    /// Splits a string into tokens respecting double-quoted segments. When fewer than two
    /// unescaped quotes are present, falls back to a plain whitespace split.
    /// </summary>
    public static IEnumerable<string> SplitQuotedText(string input)
    {
        return input.Count(x => x == '"') >= 2
            ? Regex.Matches(input, "(?<match>[^\\s\"]+)|\"(?<match>[^\"]*)\"")
                   .Cast<Match>()
                   .Select(m => m.Groups["match"].Value)
                   .Where(x => x.Length > 0)
            : input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// AND-search (case-insensitive): target must contain every included word and no excluded word.
    /// </summary>
    public static bool IsTextMatched(string target, AndSearchMaterial andSearch)
    {
        return andSearch.WordsToInclude.All(x => target.Contains(x, StringComparison.OrdinalIgnoreCase))
            && andSearch.WordsToExclude.All(x => !target.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    public static string EscapeCsvValue(string value)
    {
        if (value.Contains('"'))
            value = value.Replace("\"", "\"\"");

        if (value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            value = $"\"{value}\"";

        return value;
    }
}

public class AndSearchMaterial
{
    public AndSearchMaterial(string str)
    {
        var words = TextHelper.SplitQuotedText(str).ToList();
        WordsToInclude = words.Where(x => !x.StartsWith('-')).ToList();
        WordsToExclude = words.Where(x => Regex.IsMatch(x, @"^-\S+")).Select(x => x[1..]).ToList();
    }

    public IReadOnlyCollection<string> WordsToInclude { get; }
    public IReadOnlyCollection<string> WordsToExclude { get; }
}
