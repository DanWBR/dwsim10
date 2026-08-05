using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DWSIM.PhaseEquilibriumData.Sources.Internal
{
    public static class IdHasher
    {
        public static string ComputeDatasetId(
            string? doi,
            int pureOrMixtureIndex,
            IEnumerable<string> casNumbers,
            string? sPubName,
            int? yrPubYr,
            string? firstAuthorSurname)
        {
            var sortedCas = string.Join(",",
                casNumbers
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .OrderBy(c => c, StringComparer.Ordinal));

            string payload;
            if (!string.IsNullOrWhiteSpace(doi))
            {
                payload = $"{doi}|{pureOrMixtureIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{sortedCas}";
            }
            else
            {
                payload = $"{sPubName ?? string.Empty}|{(yrPubYr?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)}|{firstAuthorSurname ?? string.Empty}|{pureOrMixtureIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{sortedCas}";
            }

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
