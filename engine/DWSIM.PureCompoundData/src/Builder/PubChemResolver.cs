using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DWSIM.PureCompoundData.Builder
{
    /// Resolves missing identifiers (SMILES, InChIKey) from PubChem's public REST API
    /// when ThermoML / other local sources don't provide them. Used by the builder as a
    /// last-resort enrichment step before group fragmentation.
    public static class PubChemResolver
    {
        private const string BaseUrl = "https://pubchem.ncbi.nlm.nih.gov/rest/pug";

        private static readonly HttpClient _http = CreateClient();

        private static HttpClient CreateClient()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DWSIM-PureCompoundData/1.0");
            return c;
        }

        /// Returns the canonical SMILES for the given InChIKey, or null if lookup fails.
        public static string? SmilesFromInChIKey(string inchiKey, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(inchiKey)) return null;
            return TryFetchTxt($"{BaseUrl}/compound/inchikey/{Uri.EscapeDataString(inchiKey)}/property/CanonicalSMILES/TXT", ct);
        }

        /// Returns the canonical SMILES for the given CAS number, or null if lookup fails.
        public static string? SmilesFromCas(string cas, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cas)) return null;
            return TryFetchTxt($"{BaseUrl}/compound/name/{Uri.EscapeDataString(cas)}/property/CanonicalSMILES/TXT", ct);
        }

        /// Returns the canonical SMILES for the given common/IUPAC name, or null if lookup fails.
        public static string? SmilesFromName(string name, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return TryFetchTxt($"{BaseUrl}/compound/name/{Uri.EscapeDataString(name)}/property/CanonicalSMILES/TXT", ct);
        }

        private static string? TryFetchTxt(string url, CancellationToken ct)
        {
            try
            {
                var task = _http.GetAsync(url, ct);
                task.Wait(ct);
                var resp = task.Result;
                if (!resp.IsSuccessStatusCode) return null;
                var body = resp.Content.ReadAsStringAsync().Result?.Trim();
                if (string.IsNullOrEmpty(body)) return null;
                // PubChem TXT returns one line per result; take the first.
                var nl = body.IndexOf('\n');
                if (nl > 0) body = body.Substring(0, nl).Trim();
                return body;
            }
            catch
            {
                return null;
            }
        }
    }
}
