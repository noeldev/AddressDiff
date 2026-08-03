// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

namespace AddressDiff;

// Core comparison logic: turns the two address lists into the reported sets.
static class AddressComparison
{
    public static ComparisonResult Compare(List<Address> ban, List<Address> osm)
    {
        var banKeys = ban.Select(a => a.Key).ToHashSet();
        var osmByKey = osm.GroupBy(a => a.Key).ToDictionary(g => g.Key, g => g.ToList());
        var osmKeys = osmByKey.Keys.ToHashSet();

        var missingInOsm = ban.Where(a => !osmKeys.Contains(a.Key))
                              .OrderBy(a => a.Street).ThenBy(a => a.NumberSort)
                              .ToList();

        var extraInOsm = osm.DistinctBy(a => a.Key)
                            .Where(a => !banKeys.Contains(a.Key))
                            .OrderBy(a => a.Street).ThenBy(a => a.NumberSort)
                            .ToList();

        var duplicates = osmByKey.Where(kv => kv.Value.Count > 1)
                                 .OrderBy(kv => kv.Key)
                                 .Select(kv => new DuplicateGroup(kv.Key, kv.Value))
                                 .ToList();

        return new ComparisonResult(ban.Count, osm.Count, osmKeys.Count,
            missingInOsm, extraInOsm, duplicates);
    }

    // Odonym spelling audit: for every matched address, compare the OSM street to
    // the BAN reference and group the divergences by distinct spelling pair.
    // Matched keys already agree up to case, accents and spacing, so any
    // remaining difference is exactly one of those.
    public static List<SpellingIssue> SpellingIssues(List<Address> ban, List<Address> osm)
    {
        var banStreetByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in ban)
            banStreetByKey.TryAdd(a.Key, a.Street);

        return [.. osm.Where(a => banStreetByKey.TryGetValue(a.Key, out var s)
                              && !string.Equals(a.Street, s, StringComparison.Ordinal))
                  .GroupBy(a => (Osm: a.Street, Ban: banStreetByKey[a.Key]))
                  .Select(g => new SpellingIssue(
                      Normalization.SpellingDiff(g.Key.Osm, g.Key.Ban),
                      g.Key.Osm, g.Key.Ban, g.Count(), g.First()))
                  .OrderBy(x => x.Diff).ThenBy(x => x.OsmStreet)];
    }

    // Street-name coverage: how many distinct BAN street names have at least one
    // matching OSM address, and which have none. This is the per-voie view,
    // complementary to the per-address comparison.
    public static StreetCoverage StreetCoverage(List<Address> ban, List<Address> osm)
    {
        var osmStreets = osm.Select(a => Normalization.Fold(a.Street))
                            .Where(s => s.Length > 0)
                            .ToHashSet();

        // One display name per folded BAN street (first seen).
        var banStreets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in ban)
        {
            var folded = Normalization.Fold(a.Street);
            if (folded.Length > 0) banStreets.TryAdd(folded, a.Street);
        }

        var missing = banStreets.Where(kv => !osmStreets.Contains(kv.Key))
                                .Select(kv => kv.Value)
                                .OrderBy(s => s)
                                .ToList();

        return new StreetCoverage(banStreets.Count, banStreets.Count - missing.Count, missing);
    }
}

record ComparisonResult(
    int BanCount,
    int OsmObjectCount,
    int OsmDistinctCount,
    List<Address> MissingInOsm,
    List<Address> ExtraInOsm,
    List<DuplicateGroup> Duplicates);

record DuplicateGroup(string Key, List<Address> Objects);

record SpellingIssue(string Diff, string OsmStreet, string BanStreet, int Count, Address Sample);

record StreetCoverage(int BanStreetCount, int CoveredCount, List<string> MissingInOsm);
