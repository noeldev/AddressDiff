// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

namespace AddressDiff;

// A BAN row. Identity is the stable BAN identifier (id_ban_adresse or, as a
// fallback, cle_interop) when present; Key is the folded street-plus-number
// value used when no identifier is available and for the OSM comparison.
record BanAddress(
    string Identity,
    string Key,
    string Number,
    string Rep,
    string Street,
    string Lon,
    string Lat,
    string Certifie)
{
    public Address ToAddress() =>
        new(Key, Number, Rep, Street, Lon, Lat, "", "", Normalization.LeadingNumber(Number));
}
