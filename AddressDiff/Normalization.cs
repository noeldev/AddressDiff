// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

using System.Globalization;
using System.Text;

namespace AddressDiff;

// Text rules shared by both sides: comparison keys and spelling classification.
static class Normalization
{
    // Comparison key: folded street name, then folded number without spaces so
    // that "12 bis" and "12bis" collapse to the same value.
    public static string Key(string street, string number) =>
        $"{Fold(street)}#{Fold(number).Replace(" ", "")}";

    // Lowercase, strip accents, collapse whitespace. Keeps matching tolerant to
    // casing and diacritics differences between BAN and OSM.
    public static string Fold(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";

        var decomposed = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsWhiteSpace(ch) ? ' ' : ch);
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Leading integer of a house number, used only to sort reports naturally.
    public static int LeadingNumber(string number)
    {
        var digits = new string([.. number.TakeWhile(char.IsDigit)]);
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    // Classify how two otherwise-equivalent street names differ: spacing only,
    // letter case only, or diacritics. The diacritics case covers accented
    // capitals, for example a plain capital vowel where the reference carries an
    // accent on it.
    public static string SpellingDiff(string osm, string ban)
    {
        var a = CollapseSpaces(osm);
        var b = CollapseSpaces(ban);
        if (string.Equals(a, b, StringComparison.Ordinal)) return "spacing";
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return "case";
        return "accent";
    }

    static string CollapseSpaces(string s) =>
        string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
