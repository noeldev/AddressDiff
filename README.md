# AddressDiff

Tools for maintaining French BAN ([Base Adresse Nationale](https://adresse.data.gouv.fr/)) addresses in
[OpenStreetMap](https://www.openstreetmap.org/). Two comparisons are available:

- `osm-ban` - cross-check OSM against a BAN export for a commune and report the
  differences in both directions;
- `ban-ban` - compare one BAN export against a later one to see what the commune
  changed (additions, removals, and field-level modifications).

## Purpose

When maintaining BAN addresses in OSM, it is easy to lose track of which
addresses are integrated, which are missing, and which OSM objects no longer
match the reference. A raw object count is misleading: the same address can be
carried by several OSM objects (for instance a node and its building), so the
count can exceed the number of distinct addresses.

AddressDiff fetches every OSM object carrying `addr:housenumber` inside the
commune boundary, builds a normalized address key for both sides, and reports:

- addresses present in the BAN but missing from OSM;
- OSM addresses that do not match any BAN address;
- addresses carried by more than one OSM object (duplicates).

## How osm-ban works

1. The commune boundary is resolved from its [INSEE](https://www.insee.fr/) code via [Overpass](https://overpass-turbo.eu/).
2. Every `addr:housenumber` object inside it is fetched with `out center`, so
   nodes keep their own position and ways or relations report a center.
3. `associatedStreet` relations referencing those objects are fetched as well.
   When an object has no `addr:street` or `addr:place`, the street name is taken
   from the relation, transparently.
4. Each side is reduced to a comparison key: normalized street name plus
   normalized house number (accents folded, case lowered, `12 bis` and `12bis`
   treated as equal).
5. The two key sets are compared and the results are written as CSV.

## Requirements

- .NET SDK 8.0 or later (the code uses C# 12 features).
- Network access to an Overpass endpoint. The default is overpass-api.de; set
  the `OVERPASS_URL` environment variable to use a mirror. Transient failures
  (429/502/503/504 and timeouts) are retried, and if the endpoint stays
  unreachable the run ends with a clear message rather than a crash.

## Build and run

The repository is a self-contained .NET console project. Clone it and run one
of the two commands (`osm-ban` is the default and its verb may be omitted; 
boolean options accept a single or double dash):

```
dotnet run -- [osm-ban] <insee> <ban-csv> [output-dir] [-s|--spelling]
dotnet run -- ban-ban <old-ban-csv> <new-ban-csv> [output-dir]
```

Examples:

```
dotnet run -- 12345 adresses-12345.csv ./reports -s
dotnet run -- ban-ban adresses-2025.csv adresses-2026.csv ./changes
```

## Project layout

- `Program.cs` - entry point, command dispatch, and the two workflows.
- `CommandLineOptions.cs` - command verb and argument parsing.
- `Address.cs` - the address record used by the OSM comparison.
- `BanAddress.cs` - a BAN row with its stable identifier.
- `Normalization.cs` - comparison keys and spelling classification.
- `Csv.cs` - CSV parsing, header indexing, and semicolon output.
- `BanReader.cs` - loads a BAN export into rows.
- `OverpassClient.cs` - fetches OSM addresses and resolves street names.
- `AddressComparison.cs` - the osm-ban logic and its result records.
- `BanBanComparison.cs` - the ban-ban logic and its result records.
- `ReportWriter.cs` - writes the CSV reports.

## Input

The BAN file is a semicolon-separated export. Both the adresse.data.gouv.fr
"adresses" CSV (usage classique) and the BAL 1.4 format published by communes
are accepted: columns are located by name with aliases, so `nom_voie` /
`voie_nom` / `toponyme`, `rep` / `suffixe`, `code_insee` / `commune_insee`,
`lon` / `long`, and the identifier `id_ban_adresse` / `cle_interop` are all
recognised. Fictive rows (`numero` 99999) are skipped, and a BAL address spread
over several position rows is counted once. Input line endings (CRLF, LF, or
CR) are handled transparently; report files are written with the OS newline.

## osm-ban output

A console summary and four CSV files in the output directory:

- `missing-in-osm.csv` - BAN addresses with no match in OSM.
- `extra-in-osm.csv` - OSM addresses with no match in the BAN.
- `duplicates-in-osm.csv` - addresses carried by more than one OSM object.
- `streets-missing-in-osm.csv` - BAN street names with no OSM address at all.

Alongside the address counts, the summary reports the per-voie coverage: how
many distinct BAN street names have at least one OSM address, as a count and a
percentage.

In the summary, `OSM objects` counts every tagged object while
`OSM distinct addresses` counts unique addresses. The gap between the two,
together with the duplicates report, explains a total that exceeds the BAN
count.

With `--spelling`, a fourth file is written:

- `street-spelling.csv` - for each matched address whose street name is not
  written identically on both sides, the OSM and BAN spellings side by side,
  classified as `case`, `accent` (diacritics, including accented capitals) or
  `spacing`, with an occurrence count and a sample object.

Since matched addresses already agree on street and number up to case, accents
and spacing, the audit reliably surfaces casing and accent divergences without
guessing. It cannot flag a name that is written the same, but wrongly, on both
sides, as it has no dictionary of correct odonyms beyond the BAN itself.

## Matching notes

- House number and repetition indicator (`bis`, `ter`, ...) are combined and
  compared without spaces.
- Street names are compared with accents folded and case lowered; abbreviations
  are not expanded, so a genuine spelling divergence between BAN and OSM shows
  up as a difference for manual review.
- Addresses are matched on street and number, not on coordinates.

## Comparing two BAN exports (ban-ban)

`ban-ban` joins the two exports on the stable BAN identifier - `id_ban_adresse`
(the BAL UUID) when present, otherwise the cle_interop, otherwise the folded
street-and-number key - and writes three files:

- `ban-address-added.csv` - addresses present only in the newer export.
- `ban-address-removed.csv` - addresses present only in the older export.
- `ban-address-modified.csv` - one row per changed field (numero, rep, nom_voie,
  certifie, or position) for an address present in both, with its old and new
  value.

A position change is reported only beyond a small tolerance, so a mere
difference in coordinate precision between two exports is not flagged.

Note: when only the cle_interop is available it encodes the house number and the
voie code, so a renumbering or a change of voie changes the identifier and
appears as a removal plus an addition rather than a modification. The BAL
`id_ban_adresse` (a UUID) is stable across such moves and is preferred when
present.

## License

Released under the GNU General Public License v3.0 or later
(`GPL-3.0-or-later`). Each source file carries a two-line SPDX header, and the
full license text belongs in a `LICENSE` file at the repository root.
