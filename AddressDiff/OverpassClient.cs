// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

using System.Text.Json;

namespace AddressDiff;

// Fetches OSM address objects for a commune and resolves the street name,
// falling back to addr:place and then to the parent associatedStreet relation.
// The endpoint can be overridden with the OVERPASS_URL environment variable, and
// transient server errors (429/502/503/504) and timeouts are retried.
static class OverpassClient
{
    const string DefaultEndpoint = "https://overpass-api.de/api/interpreter";
    static readonly int[] RetryDelaysSeconds = [2, 5, 10];

    public static async Task<List<Address>> FetchAddressesAsync(string insee)
    {
        // Address objects keep their coordinates through "out center" (nodes
        // report their own position, ways and relations report a center). The
        // parent associatedStreet relations are fetched separately so a missing
        // street can be filled from the relation name.
        var query = $$"""
            [out:json][timeout:180];
            area["ref:INSEE"="{{insee}}"]["boundary"="administrative"]->.a;
            nwr(area.a)["addr:housenumber"]->.addr;
            .addr out center;
            (
              rel(bn.addr)["type"="associatedStreet"];
              rel(bw.addr)["type"="associatedStreet"];
            );
            out body;
            """;

        var endpoint = Environment.GetEnvironmentVariable("OVERPASS_URL") is { Length: > 0 } url
            ? url
            : DefaultEndpoint;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(240) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("address-diff/1.0 (OSM-BAN cross-check)");

        using var doc = await FetchAsync(http, endpoint, query);
        return Parse(doc);
    }

    static async Task<JsonDocument> FetchAsync(HttpClient http, string endpoint, string query)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
                using var response = await http.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync();
                return await JsonDocument.ParseAsync(stream);
            }
            catch (Exception ex) when (attempt < RetryDelaysSeconds.Length && IsTransient(ex))
            {
                Console.Error.WriteLine(
                    $"Failed to fetch OSM data (error: {ex.Message}); retrying in {RetryDelaysSeconds[attempt]}s...");
                await Task.Delay(TimeSpan.FromSeconds(RetryDelaysSeconds[attempt]));
            }
        }
    }

    // Transient failures worth retrying: a busy or timing-out server, or a
    // connection-level error (null status).
    static bool IsTransient(Exception ex) => ex switch
    {
        HttpRequestException h => (int?)h.StatusCode is 429 or 502 or 503 or 504 or null,
        TaskCanceledException => true,
        _ => false
    };

    static List<Address> Parse(JsonDocument doc)
    {
        var result = new List<Address>();
        if (!doc.RootElement.TryGetProperty("elements", out var elements))
            return result;

        var streetByObject = MapAssociatedStreets(elements);

        foreach (var el in elements.EnumerateArray())
        {
            if (!el.TryGetProperty("tags", out var tags)) continue;

            var number = Tag(tags, "addr:housenumber");
            if (string.IsNullOrEmpty(number)) continue;

            var type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            var id = el.TryGetProperty("id", out var idv) ? idv.GetRawText() : "";

            // Street resolution: explicit tag, then addr:place, then the parent
            // associatedStreet relation.
            var street = Tag(tags, "addr:street");
            if (string.IsNullOrEmpty(street)) street = Tag(tags, "addr:place");
            if (string.IsNullOrEmpty(street) && streetByObject.TryGetValue($"{type}/{id}", out var relStreet))
                street = relStreet;

            var (lon, lat) = ReadCoords(el);

            result.Add(new Address(
                Normalization.Key(street, number), number, "", street, lon, lat, type, id,
                Normalization.LeadingNumber(number)));
        }

        return result;
    }

    // Map every member of an associatedStreet relation to the relation name,
    // keyed by "type/id". Lookups only happen for objects that lack a street
    // tag, so mapping the street member too is harmless.
    static Dictionary<string, string> MapAssociatedStreets(JsonElement elements)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var el in elements.EnumerateArray())
        {
            if (!el.TryGetProperty("tags", out var relTags)) continue;
            if (Tag(relTags, "type") != "associatedStreet") continue;

            var relName = Tag(relTags, "name");
            if (string.IsNullOrEmpty(relName)) continue;
            if (!el.TryGetProperty("members", out var members)) continue;

            foreach (var m in members.EnumerateArray())
            {
                var mType = m.TryGetProperty("type", out var mt) ? mt.GetString() ?? "" : "";
                var mRef = m.TryGetProperty("ref", out var mr) ? mr.GetRawText() : "";
                if (mType.Length != 0 && mRef.Length != 0)
                    map[$"{mType}/{mRef}"] = relName;
            }
        }

        return map;
    }

    static string Tag(JsonElement tags, string name) =>
        tags.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

    static (string Lon, string Lat) ReadCoords(JsonElement el)
    {
        if (el.TryGetProperty("lon", out var lon) && el.TryGetProperty("lat", out var lat))
            return (lon.GetRawText(), lat.GetRawText());
        if (el.TryGetProperty("center", out var center)
            && center.TryGetProperty("lon", out var clon)
            && center.TryGetProperty("lat", out var clat))
            return (clon.GetRawText(), clat.GetRawText());
        return ("", "");
    }
}
