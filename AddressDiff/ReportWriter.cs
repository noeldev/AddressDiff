// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

using System.Globalization;

namespace AddressDiff;

// Writes the comparison results as CSV files in the output directory.
static class ReportWriter
{
    public static void WriteReports(string outputDir, ComparisonResult result)
    {
        Csv.Write(Path.Combine(outputDir, "missing-in-osm.csv"),
            ["numero", "rep", "nom_voie", "lon", "lat"],
            result.MissingInOsm.Select(a => new[] { a.Number, a.Rep, a.Street, a.Lon, a.Lat }));

        Csv.Write(Path.Combine(outputDir, "extra-in-osm.csv"),
            ["osm_type", "osm_id", "housenumber", "street", "lon", "lat"],
            result.ExtraInOsm.Select(a => new[] { a.OsmType, a.OsmId, a.Number, a.Street, a.Lon, a.Lat }));

        Csv.Write(Path.Combine(outputDir, "duplicates-in-osm.csv"),
            ["key", "count", "objects"],
            result.Duplicates.Select(d => new[]
            {
                d.Key,
                d.Objects.Count.ToString(CultureInfo.InvariantCulture),
                string.Join(' ', d.Objects.Select(o => $"{o.OsmType}/{o.OsmId}"))
            }));
    }

    public static void WriteSpelling(string outputDir, List<SpellingIssue> issues)
    {
        Csv.Write(Path.Combine(outputDir, "street-spelling.csv"),
            ["diff", "osm_street", "ban_street", "count", "sample_type", "sample_id", "lon", "lat"],
            issues.Select(x => new[]
            {
                x.Diff, x.OsmStreet, x.BanStreet, x.Count.ToString(CultureInfo.InvariantCulture),
                x.Sample.OsmType, x.Sample.OsmId, x.Sample.Lon, x.Sample.Lat
            }));
    }

    public static void WriteStreetCoverage(string outputDir, StreetCoverage coverage)
    {
        Csv.Write(Path.Combine(outputDir, "streets-missing-in-osm.csv"),
            ["nom_voie"],
            coverage.MissingInOsm.Select(s => new[] { s }));
    }

    public static void WriteBanBan(string outputDir, BanBanResult result)
    {
        string[] rowHeader = ["identity", "numero", "rep", "nom_voie", "lon", "lat"];

        Csv.Write(Path.Combine(outputDir, "ban-address-added.csv"), rowHeader,
            result.Added.Select(b => new[] { b.Identity, b.Number, b.Rep, b.Street, b.Lon, b.Lat }));

        Csv.Write(Path.Combine(outputDir, "ban-address-removed.csv"), rowHeader,
            result.Removed.Select(b => new[] { b.Identity, b.Number, b.Rep, b.Street, b.Lon, b.Lat }));

        Csv.Write(Path.Combine(outputDir, "ban-address-modified.csv"),
            ["identity", "numero", "nom_voie", "field", "old", "new"],
            result.Modified.SelectMany(m => m.Changes.Select(c =>
                new[] { m.Identity, m.Number, m.Street, c.Field, c.Old, c.New })));
    }
}
