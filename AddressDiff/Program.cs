// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

using System.Globalization;

namespace AddressDiff;

// Entry point: show the banner, parse the command, and dispatch to the matching
// workflow.
static class Program
{
    static async Task<int> Main(string[] args)
    {
        AppInfo.PrintBanner();

        var (parse, error) = CommandLineOptions.Parse(args);
        if (parse is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        try
        {
            return parse.Command switch
            {
                Command.OsmBan => await RunOsmBanAsync(parse.OsmBan!),
                Command.BanBan => RunBanBan(parse.BanBan!),
                _ => 1
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine($"Failed to fetch OSM data: {ex.Message}");
            Console.Error.WriteLine("The Overpass instance may be busy. Retry, or set OVERPASS_URL to a mirror.");
            return 2;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    // Compare OSM addresses against a BAN export for one commune.
    static async Task<int> RunOsmBanAsync(OsmBanOptions o)
    {
        if (!File.Exists(o.BanPath))
        {
            Console.Error.WriteLine($"BAN file not found: {o.BanPath}");
            return 1;
        }

        Directory.CreateDirectory(o.OutputDir);

        Console.WriteLine($"Parsing BAN file: {o.BanPath}");
        // Distinct by key so BAL position rows (several rows per address) are
        // counted once.
        var ban = BanReader.Load(o.BanPath, o.Insee).Select(b => b.ToAddress()).DistinctBy(a => a.Key).ToList();
        Console.WriteLine($"BAN file parsed ({ban.Count} addresses).");

        Console.WriteLine($"Fetching OSM data for INSEE {o.Insee}...");
        var osm = await OverpassClient.FetchAddressesAsync(o.Insee);
        Console.WriteLine($"OSM data fetched ({osm.Count} objects).");

        var result = AddressComparison.Compare(ban, osm);
        ReportWriter.WriteReports(o.OutputDir, result);

        var coverage = AddressComparison.StreetCoverage(ban, osm);
        ReportWriter.WriteStreetCoverage(o.OutputDir, coverage);

        if (o.CheckSpelling)
        {
            var issues = AddressComparison.SpellingIssues(ban, osm);
            ReportWriter.WriteSpelling(o.OutputDir, issues);
            Console.WriteLine($"Street spelling audit written ({issues.Count} issues).");
        }

        var coveredPct = coverage.BanStreetCount == 0
            ? 0.0
            : 100.0 * coverage.CoveredCount / coverage.BanStreetCount;

        Console.WriteLine();
        // OsmObjectCount counts every tagged object, OsmDistinctCount counts
        // unique addresses; the gap plus the duplicates report explains a total
        // that exceeds the BAN count.
        Console.WriteLine($"BAN addresses          : {result.BanCount}");
        Console.WriteLine($"OSM objects (addr)     : {result.OsmObjectCount}");
        Console.WriteLine($"OSM distinct addresses : {result.OsmDistinctCount}");
        Console.WriteLine($"Missing in OSM         : {result.MissingInOsm.Count}");
        Console.WriteLine($"Extra in OSM (not BAN) : {result.ExtraInOsm.Count}");
        Console.WriteLine($"Duplicate addresses    : {result.Duplicates.Count}");
        Console.WriteLine($"BAN street names       : {coverage.BanStreetCount}");
        Console.WriteLine($"  with OSM addresses   : {coverage.CoveredCount} ({coveredPct.ToString("F1", CultureInfo.InvariantCulture)}%)");
        Console.WriteLine($"  missing from OSM     : {coverage.MissingInOsm.Count}");
        PrintOutputFolder(o.OutputDir);
        return 0;
    }

    // Compare two BAN exports to isolate the commune changes between them.
    static int RunBanBan(BanBanOptions o)
    {
        if (!File.Exists(o.PreviousPath))
        {
            Console.Error.WriteLine($"Previous BAN file not found: {o.PreviousPath}");
            return 1;
        }
        if (!File.Exists(o.CurrentPath))
        {
            Console.Error.WriteLine($"Current BAN file not found: {o.CurrentPath}");
            return 1;
        }

        Directory.CreateDirectory(o.OutputDir);

        Console.WriteLine($"Parsing previous BAN file: {o.PreviousPath}");
        var previous = BanReader.Load(o.PreviousPath, null);
        Console.WriteLine($"Previous BAN file parsed ({previous.Count} rows).");

        Console.WriteLine($"Parsing current BAN file: {o.CurrentPath}");
        var current = BanReader.Load(o.CurrentPath, null);
        Console.WriteLine($"Current BAN file parsed ({current.Count} rows).");

        Console.WriteLine("Comparing exports...");
        var result = BanBanComparison.Compare(previous, current);
        ReportWriter.WriteBanBan(o.OutputDir, result);

        Console.WriteLine();
        Console.WriteLine($"Previous addresses : {result.PreviousCount}");
        Console.WriteLine($"Current addresses  : {result.CurrentCount}");
        Console.WriteLine($"Added              : {result.Added.Count}");
        Console.WriteLine($"Removed            : {result.Removed.Count}");
        Console.WriteLine($"Modified           : {result.Modified.Count}");
        PrintOutputFolder(o.OutputDir);
        return 0;
    }

    // Show where the reports went: the plain path for reading, then the same
    // folder as a file URI, which Windows Terminal turns into a link
    // (Ctrl+click opens it in Explorer). The URI escapes spaces and non-ASCII
    // characters, so the link is never cut short.
    static void PrintOutputFolder(string outputDir)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDir));
        Console.WriteLine();
        Console.WriteLine("Reports written to:");
        Console.WriteLine($"  {full}");
        Console.WriteLine($"  {new Uri(full + Path.DirectorySeparatorChar).AbsoluteUri}");
    }
}
