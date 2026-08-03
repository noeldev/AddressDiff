// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

using System.Text;

namespace AddressDiff;

// Loads a BAN export into rows. Accepts both the "usage classique" adresses CSV
// and the BAL 1.4 format by looking columns up under known aliases. Fictive
// rows (numero 99999) and, when insee is given, rows of other communes are
// skipped.
static class BanReader
{
    public static List<BanAddress> Load(string path, string? insee)
    {
        var result = new List<BanAddress>();
        using var reader = new StreamReader(path, Encoding.UTF8);

        var header = reader.ReadLine();
        if (header is null) return result;

        var index = Csv.Header(Csv.ParseLine(header, ';'));
        var iId = Csv.Column(index, "id_ban_adresse", "uid_adresse", "cle_interop", "id");
        var iNum = Csv.Column(index, "numero");
        var iRep = Csv.Column(index, "rep", "suffixe");
        var iVoie = Csv.Column(index, "nom_voie", "voie_nom", "toponyme");
        var iInsee = Csv.Column(index, "code_insee", "commune_insee");
        var iLon = Csv.Column(index, "lon", "long");
        var iLat = Csv.Column(index, "lat");
        var iCert = Csv.Column(index, "certifie", "certification_commune");

        if (iNum < 0 || iVoie < 0)
            throw new InvalidDataException(
                "BAN header is missing 'numero' or a street column (nom_voie/voie_nom/toponyme). "
                + $"Header: {header}");

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var f = Csv.ParseLine(line, ';');

            var number = Csv.Field(f, iNum);
            if (number == "99999") continue; // fictive address (a voie without a real number)

            if (insee is not null && iInsee >= 0
                && !string.Equals(Csv.Field(f, iInsee), insee, StringComparison.Ordinal))
                continue;

            var rep = Csv.Field(f, iRep);
            var street = Csv.Field(f, iVoie);
            var full = string.IsNullOrEmpty(rep) ? number : $"{number} {rep}";

            result.Add(new BanAddress(
                Csv.Field(f, iId), Normalization.Key(street, full),
                number, rep, street, Csv.Field(f, iLon), Csv.Field(f, iLat), Csv.Field(f, iCert)));
        }

        return result;
    }
}
