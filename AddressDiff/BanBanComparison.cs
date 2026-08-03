// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

using System.Globalization;

namespace AddressDiff;

// Compares two BAN exports of the same commune to isolate what changed between
// them: added and removed addresses, and field-level modifications on addresses
// that persist. Joining is done on the stable BAN identity (id_ban_adresse, or
// cle_interop as a fallback); when no identifier is available it falls back to
// the folded key.
static class BanBanComparison
{
    public static BanBanResult Compare(List<BanAddress> previous, List<BanAddress> current)
    {
        // Deduplicate by identity first: a BAL address can appear on several
        // rows (one per position type), and comparing raw rows would report
        // those as spurious additions or modifications.
        var oldByIdentity = ByIdentity(previous);
        var newByIdentity = ByIdentity(current);

        var added = newByIdentity.Values.Where(b => !oldByIdentity.ContainsKey(Identity(b)))
                                 .OrderBy(b => b.Street).ThenBy(b => Normalization.LeadingNumber(b.Number))
                                 .ToList();

        var removed = oldByIdentity.Values.Where(b => !newByIdentity.ContainsKey(Identity(b)))
                                  .OrderBy(b => b.Street).ThenBy(b => Normalization.LeadingNumber(b.Number))
                                  .ToList();

        var modified = newByIdentity.Values
            .Where(cur => oldByIdentity.ContainsKey(Identity(cur)))
            .Select(cur => new BanModification(Identity(cur), cur.Number, cur.Street,
                Changes(oldByIdentity[Identity(cur)], cur)))
            .Where(m => m.Changes.Count > 0)
            .OrderBy(m => m.Street).ThenBy(m => Normalization.LeadingNumber(m.Number))
            .ToList();

        return new BanBanResult(oldByIdentity.Count, newByIdentity.Count, added, removed, modified);
    }

    static Dictionary<string, BanAddress> ByIdentity(List<BanAddress> rows)
    {
        var map = new Dictionary<string, BanAddress>(StringComparer.Ordinal);
        foreach (var b in rows)
            map[Identity(b)] = b;
        return map;
    }

    static string Identity(BanAddress b) => string.IsNullOrEmpty(b.Identity) ? b.Key : b.Identity;

    static List<FieldChange> Changes(BanAddress a, BanAddress b)
    {
        var changes = new List<FieldChange>();
        AddIf(changes, "numero", a.Number, b.Number);
        AddIf(changes, "rep", a.Rep, b.Rep);
        AddIf(changes, "nom_voie", a.Street, b.Street);
        AddIf(changes, "certifie", a.Certifie, b.Certifie);
        if (Moved(a.Lon, a.Lat, b.Lon, b.Lat))
            changes.Add(new FieldChange("position", $"{a.Lon},{a.Lat}", $"{b.Lon},{b.Lat}"));
        return changes;
    }

    static void AddIf(List<FieldChange> changes, string field, string oldValue, string newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            changes.Add(new FieldChange(field, oldValue, newValue));
    }

    // Treat a position as moved only beyond a small tolerance, so a mere change
    // of coordinate precision between exports is not reported as a modification.
    static bool Moved(string lon1, string lat1, string lon2, string lat2)
    {
        if (TryDouble(lon1, out var x1) && TryDouble(lat1, out var y1)
            && TryDouble(lon2, out var x2) && TryDouble(lat2, out var y2))
            return Math.Abs(x1 - x2) > 1e-6 || Math.Abs(y1 - y2) > 1e-6;
        return !string.Equals($"{lon1},{lat1}", $"{lon2},{lat2}", StringComparison.Ordinal);
    }

    static bool TryDouble(string s, out double d) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
}

record BanBanResult(
    int PreviousCount,
    int CurrentCount,
    List<BanAddress> Added,
    List<BanAddress> Removed,
    List<BanModification> Modified);

record BanModification(string Identity, string Number, string Street, List<FieldChange> Changes);

record FieldChange(string Field, string Old, string New);
