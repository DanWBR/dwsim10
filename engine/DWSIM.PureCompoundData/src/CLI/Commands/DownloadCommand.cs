using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace DWSIM.PureCompoundData.CLI.Commands
{
    internal static class DownloadCommand
    {
        public static async Task<int> RunAsync(string? url, string? dest, TextWriter stdout, TextWriter stderr)
        {
            url ??= AppPaths.DefaultArchiveUrl;
            dest ??= AppPaths.DefaultArchivePath();
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            stdout.WriteLine($"Downloading {url}");
            stdout.WriteLine($"      -> {dest}");

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using var wc = new WebClient();
                await wc.DownloadFileTaskAsync(new Uri(url), dest).ConfigureAwait(false);
                var size = new FileInfo(dest).Length;
                stdout.WriteLine($"Done. {size / (1024 * 1024)} MiB.");
                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                stderr.WriteLine($"Download failed: {ex.Message}");
                return ExitCodes.NetworkError;
            }
        }
    }
}
