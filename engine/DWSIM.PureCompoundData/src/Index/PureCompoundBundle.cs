using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DWSIM.PureCompoundData.Index
{
    /// <summary>
    /// Public helper for host applications (DWSIM UI, scripting) to discover,
    /// download, and install the pre-built pure-compound LiteDB bundle.
    /// </summary>
    public static class PureCompoundBundle
    {
        public const string DefaultBundleUrl =
            "https://mrabcdkqjhotmejgtayb.supabase.co/storage/v1/object/public/dwsim-data/purecompound.litedb.gz";

        public static string DefaultDbPath() => CachePaths.DefaultDatabasePath();

        public static bool IsInstalled(string? dbPath = null)
            => File.Exists(dbPath ?? DefaultDbPath());

        /// <summary>
        /// Downloads the gzipped bundle and decompresses to <paramref name="dbPath"/>.
        /// <paramref name="progress"/> receives (bytesTransferred, totalBytes); total may be -1 when unknown.
        /// </summary>
        public static async Task DownloadAndInstallAsync(
            string? dbPath = null,
            string? url = null,
            IProgress<(long Bytes, long Total)>? progress = null,
            CancellationToken ct = default)
        {
            dbPath ??= DefaultDbPath();
            url ??= DefaultBundleUrl;

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? -1L;

            var tmpGz = dbPath + ".gz.tmp";
            try
            {
                using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fs = File.Create(tmpGz))
                {
                    var buffer = new byte[81920];
                    long read = 0;
                    int n;
                    while ((n = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, n, ct).ConfigureAwait(false);
                        read += n;
                        progress?.Report((read, total));
                    }
                }

                var tmpDb = dbPath + ".tmp";
                using (var gz = new GZipStream(File.OpenRead(tmpGz), CompressionMode.Decompress))
                using (var fs = File.Create(tmpDb))
                {
                    await gz.CopyToAsync(fs, 81920, ct).ConfigureAwait(false);
                }

                if (File.Exists(dbPath)) File.Delete(dbPath);
                File.Move(tmpDb, dbPath);
            }
            finally
            {
                try { if (File.Exists(tmpGz)) File.Delete(tmpGz); } catch { }
            }
        }
    }
}
