using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DWSIM.PhaseEquilibriumData.CLI.Commands
{
    internal static class DownloadCommand
    {
        public static async Task<int> RunAsync(string? url, string? dest, TextWriter stdout, TextWriter stderr)
        {
            url ??= AppPaths.DefaultArchiveUrl;
            dest ??= AppPaths.DefaultArchivePath();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(30);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                stdout.WriteLine($"Downloading {url}");
                using (var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    resp.EnsureSuccessStatusCode();
                    var total = resp.Content.Headers.ContentLength ?? 0;
                    using var src = await resp.Content.ReadAsStreamAsync();
                    using var fs = File.Create(dest);
                    await src.CopyToAsync(fs);
                    stdout.WriteLine($"Downloaded {total:N0} bytes to {dest} in {sw.Elapsed.TotalSeconds:F1}s");
                }

                try
                {
                    using var shaResp = await client.GetAsync(url + ".sha256");
                    if (shaResp.IsSuccessStatusCode)
                    {
                        var expected = (await shaResp.Content.ReadAsStringAsync()).Trim().Split(' ', '\t')[0];
                        var actual = ComputeSha256(dest);
                        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                        {
                            stderr.WriteLine($"SHA256 mismatch (expected {expected}, actual {actual}).");
                            return ExitCodes.DataError;
                        }
                        stdout.WriteLine("SHA256 verified.");
                    }
                }
                catch
                {
                    /* sidecar is optional */
                }

                return ExitCodes.Success;
            }
            catch (HttpRequestException ex)
            {
                stderr.WriteLine($"Network error: {ex.Message}");
                return ExitCodes.NetworkError;
            }
            catch (TaskCanceledException ex)
            {
                stderr.WriteLine($"Network timeout: {ex.Message}");
                return ExitCodes.NetworkError;
            }
            catch (Exception ex)
            {
                stderr.WriteLine($"Error: {ex.Message}");
                return ExitCodes.DataError;
            }
        }

        private static string ComputeSha256(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var fs = File.OpenRead(path);
            var hash = sha.ComputeHash(fs);
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
