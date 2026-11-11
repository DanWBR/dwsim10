// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The identifiers DEXPI names a pure compound by, for the compounds of DWSIM's databases: the
    /// IUPAC name and the ChEBI identifier of each CAS number, from the table in
    /// Resources/dexpi_identifiers.tsv (CAS, IUPAC name, ChEBI identifier; see LICENSE-NOTICE.txt
    /// for its sources). The export writes them, and the import finds a compound a model names
    /// only by one of them.
    /// </summary>
    public static class DexpiIdentifiers
    {
        static Dictionary<string, (string Iupac, string Chebi)> byCas;
        static Dictionary<string, string> casOf;

        static void Load()
        {
            if (byCas != null) return;
            var table = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
            var back = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var stream = typeof(DexpiIdentifiers).Assembly.GetManifestResourceStream("DWSIM.Extensions.SolidCraft.dexpi_identifiers.tsv"))
                using (var reader = new StreamReader(stream))
                    while (!reader.EndOfStream)
                    {
                        var p = (reader.ReadLine() ?? "").Split('\t');
                        if (p.Length < 3 || p[0].Trim().Length == 0) continue;
                        string cas = p[0].Trim(), iupac = p[1].Trim(), chebi = p[2].Trim();
                        table[cas] = (iupac, chebi);
                        if (iupac.Length > 0 && !back.ContainsKey(iupac)) back[iupac] = cas;
                        if (chebi.Length > 0 && !back.ContainsKey(chebi)) back[chebi] = cas;
                    }
            }
            catch (Exception) { }
            casOf = back;
            byCas = table;
        }

        /// <summary>The IUPAC name and the ChEBI identifier of a CAS number, each "" when the table does not have it.</summary>
        public static (string Iupac, string Chebi) Of(string cas)
        {
            Load();
            return byCas.TryGetValue((cas ?? "").Trim(), out var found) ? found : ("", "");
        }

        /// <summary>The CAS number of the compound an IUPAC name or a ChEBI identifier ("CHEBI:15377") names, or null.</summary>
        public static string CasOf(string identifier)
        {
            Load();
            return casOf.TryGetValue((identifier ?? "").Trim(), out var cas) ? cas : null;
        }
    }
}
