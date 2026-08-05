using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DWSIM.PhaseEquilibriumData.Index
{
    /// <summary>
    /// Public helper for host applications (DWSIM UI, scripting) to discover,
    /// download, and install the pre-built phase-equilibrium LiteDB bundle.
    /// </summary>
    public static class PhaseEqBundle
    {
        public const string DefaultBundleUrl =
            "https://mrabcdkqjhotmejgtayb.supabase.co/storage/v1/object/public/dwsim-data/phaseq.litedb.gz";

        public const string DbFileName = "phaseq.litedb";

        public static string DefaultDataDir()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, "DWSIM", "PhaseEq");
            }
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdg))
                return Path.Combine(xdg!, "DWSIM", "PhaseEq");
            var home = Environment.GetEnvironmentVariable("HOME") ?? ".";
            return Path.Combine(home, ".local", "share", "DWSIM", "PhaseEq");
        }

        public static string DefaultDbPath() => Path.Combine(DefaultDataDir(), DbFileName);

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
