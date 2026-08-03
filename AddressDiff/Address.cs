// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

namespace AddressDiff;

// A single address on either side of the comparison. Rep is only meaningful for
// BAN rows; OsmType and OsmId are only meaningful for OSM objects.
record Address(
    string Key,
    string Number,
    string Rep,
    string Street,
    string Lon,
    string Lat,
    string OsmType,
    string OsmId,
    int NumberSort);
