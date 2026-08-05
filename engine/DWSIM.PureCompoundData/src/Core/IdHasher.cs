using System;
using System.Security.Cryptography;
using System.Text;

namespace DWSIM.PureCompoundData.Core
{
    /// Deterministic SHA256-derived IDs for <see cref="PureCompoundRecord"/> instances.
    /// The hash inputs are joined with '|' and UTF-8 encoded before hashing, so equal
    /// logical records from the same source always produce the same ID.
    public static class IdHasher
    {
        public static string ComputeRecordId(
            string sourceProvider,
            string casNumber,
            PropertyCategory category,
            string property,
            string? doi,
            int? sourceDatasetIndex = null)
        {
            var raw = string.Join("|",
                sourceProvider ?? string.Empty,
                (casNumber ?? string.Empty).Trim().ToUpperInvariant(),
                category.ToString(),
                (property ?? string.Empty).Trim(),
                (doi ?? string.Empty).Trim().ToLowerInvariant(),
                sourceDatasetIndex?.ToString() ?? string.Empty);

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
