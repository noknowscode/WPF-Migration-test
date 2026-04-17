using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ImageViewerUno.Services;

public sealed class NaturalStringComparer : IComparer<string?>
{
    public static NaturalStringComparer Instance { get; } = new();

    private static readonly Regex Chunker = new(@"\d+|\D+", RegexOptions.Compiled);

    public int Compare(string? a, string? b)
    {
        if (a is null) return b is null ? 0 : -1;
        if (b is null) return 1;

        var aChunks = Chunker.Matches(a);
        var bChunks = Chunker.Matches(b);
        int count = Math.Min(aChunks.Count, bChunks.Count);

        for (int i = 0; i < count; i++)
        {
            string ac = aChunks[i].Value;
            string bc = bChunks[i].Value;

            int cmp = char.IsDigit(ac[0]) && char.IsDigit(bc[0])
                ? CompareNumeric(ac, bc)
                : string.Compare(ac, bc, StringComparison.CurrentCultureIgnoreCase);

            if (cmp != 0) return cmp;
        }

        return aChunks.Count.CompareTo(bChunks.Count);
    }

    private static int CompareNumeric(string a, string b)
    {
        int ai = 0; while (ai < a.Length - 1 && a[ai] == '0') ai++;
        int bi = 0; while (bi < b.Length - 1 && b[bi] == '0') bi++;
        int aLen = a.Length - ai;
        int bLen = b.Length - bi;

        if (aLen != bLen) return aLen.CompareTo(bLen);

        int cmp = string.CompareOrdinal(a, ai, b, bi, aLen);
        return cmp != 0 ? cmp : a.Length.CompareTo(b.Length);
    }
}
