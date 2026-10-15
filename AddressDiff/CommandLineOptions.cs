// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

namespace AddressDiff;

enum Command { OsmBan, BanBan }

record OsmBanOptions(string Insee, string BanPath, string OutputDir, bool CheckSpelling);

record BanBanOptions(string PreviousPath, string CurrentPath, string OutputDir);

record CliParse(Command Command, OsmBanOptions? OsmBan, BanBanOptions? BanBan);

// Parses the command verb and its arguments into a typed result. The command is
// optional and defaults to osm-ban; boolean options accept a single or a double
// dash (for example -s or --spelling), and an unrecognised option is an error
// rather than being silently ignored.
static class CommandLineOptions
{
    static readonly HashSet<string> OsmBanFlags = ["-s", "--spelling"];
    static readonly HashSet<string> BanBanFlags = [];

    public const string Usage =
        "Usage:\n" +
        "  AddressDiff [osm-ban] <insee> <ban-csv> [output-dir] [-s|--spelling]\n" +
        "  AddressDiff ban-ban <old-ban-csv> <new-ban-csv> [output-dir]";

    public static (CliParse? Parse, string? Error) Parse(string[] args)
    {
        // Resolve the command; when the first token is not a known verb it is
        // treated as an argument and the command defaults to osm-ban.
        var head = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        var known = head is "osm-ban" or "ban-ban";
        var command = known ? head : "osm-ban";
        var rest = known ? [.. args.Skip(1)] : args;

        var flags = rest.Where(IsFlag).Select(a => a.ToLowerInvariant()).ToHashSet();
        var positional = rest.Where(a => !IsFlag(a)).ToList();

        var validFlags = command == "ban-ban" ? BanBanFlags : OsmBanFlags;
        var unknown = flags.FirstOrDefault(f => !validFlags.Contains(f));
        if (unknown is not null)
            return (null, $"Unknown option: {unknown}\n{Usage}");

        switch (command)
        {
            case "ban-ban":
                if (positional.Count < 2) return (null, Usage);
                return (new CliParse(Command.BanBan, null, new BanBanOptions(
                    positional[0], positional[1],
                    positional.Count > 2 ? positional[2] : Directory.GetCurrentDirectory())), null);

            default:
                if (positional.Count < 2) return (null, Usage);
                return (new CliParse(Command.OsmBan, new OsmBanOptions(
                    positional[0].Trim(), positional[1],
                    positional.Count > 2 ? positional[2] : Directory.GetCurrentDirectory(),
                    flags.Contains("--spelling") || flags.Contains("-s")), null), null);
        }
    }

    static bool IsFlag(string arg) => arg.Length > 1 && arg[0] == '-';
}
