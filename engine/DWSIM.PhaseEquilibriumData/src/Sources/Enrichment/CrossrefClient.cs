using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using DWSIM.PhaseEquilibriumData.Core;

namespace DWSIM.PhaseEquilibriumData.Sources.Enrichment
{
    /// <summary>
    /// Minimal Crossref REST-API client for citation enrichment by DOI.
    /// Hits <c>https://api.crossref.org/works/{doi}</c>, returns a <see cref="Citation"/>.
    /// Respects Crossref's "polite pool" convention by sending a descriptive User-Agent.
    /// Uses <see cref="HttpWebRequest"/> (BCL) - no System.Net.Http dependency.
    /// </summary>
    public sealed class CrossrefClient
    {
        private const string BaseUrl = "https://api.crossref.org/works/";
        private readonly string _userAgent;
        private readonly int _timeoutMs;

        static CrossrefClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public CrossrefClient(string contactEmail = "dwsim@dwsim.org", int timeoutMs = 20_000)
        {
            _userAgent = $"DWSIM.PhaseEquilibriumData/1.0 (mailto:{contactEmail})";
            _timeoutMs = timeoutMs;
        }

        /// <summary>
        /// Fetches citation metadata for the given DOI. Returns <c>null</c> if the DOI is
        /// unknown to Crossref (404) or the response cannot be parsed.
        /// </summary>
        public Task<Citation?> GetByDoiAsync(string doi, CancellationToken ct = default)
        {
            return Task.Run<Citation?>(() => GetByDoi(doi, ct), ct);
        }

        public Citation? GetByDoi(string doi, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(doi)) return null;
            var url = BaseUrl + Uri.EscapeDataString(doi.Trim());
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = _userAgent;
                req.Timeout = _timeoutMs;
                req.ReadWriteTimeout = _timeoutMs;
                req.Accept = "application/json";

                using var resp = (HttpWebResponse)req.GetResponse();
                if (resp.StatusCode != HttpStatusCode.OK) return null;
                using var stream = resp.GetResponseStream();
                if (stream == null) return null;
                using var sr = new StreamReader(stream);
                var body = sr.ReadToEnd();
                ct.ThrowIfCancellationRequested();
                return ParseWork(body, doi);
            }
            catch (WebException) { return null; }
            catch (IOException) { return null; }
        }

        public static Citation? ParseWork(string body, string doi)
        {
            JObject root;
            try { root = JObject.Parse(body); }
            catch { return null; }
            var msg = root["message"] as JObject;
            if (msg == null) return null;

            string? title = (msg["title"] as JArray)?.OfType<JValue>().Select(v => v.ToString()).FirstOrDefault();
            string? journal = (msg["container-title"] as JArray)?.OfType<JValue>().Select(v => v.ToString()).FirstOrDefault();
            string? volume = (string?)msg["volume"];
            string? pages = (string?)msg["page"];

            int? year = null;
            var issued = msg["issued"] as JObject;
            var dateParts = issued?["date-parts"] as JArray;
            if (dateParts != null && dateParts.Count > 0 && dateParts[0] is JArray first && first.Count > 0)
            {
                if (int.TryParse(first[0].ToString(), out var y)) year = y;
            }

            var authors = new List<string>();
            if (msg["author"] is JArray arr)
            {
                foreach (var a in arr.OfType<JObject>())
                {
                    var family = (string?)a["family"];
                    var given = (string?)a["given"];
                    if (!string.IsNullOrWhiteSpace(family))
                        authors.Add(string.IsNullOrWhiteSpace(given) ? family! : $"{family}, {given}");
                    else if (!string.IsNullOrWhiteSpace((string?)a["name"]))
                        authors.Add((string)a["name"]!);
                }
            }

            int? volInt = null;
            if (int.TryParse(volume, out var vi)) volInt = vi;

            return new Citation(doi, title, authors, journal, year, volInt, pages);
        }
    }
}
