// HelpWindow.cs
//
// Drop-in WebView2 host for the DWSIM help bundle. Open with
//     new HelpWindow().Show();
// or with HelpWindow.OpenAt("sec:tea") to deep-link to a section.
//
// Prerequisites:
//   1. NuGet:           Microsoft.Web.WebView2  (>= 1.0.2592)
//   2. Runtime:         WebView2 runtime — preinstalled on Win10 22H2+ / Win11.
//                       Bundle the Evergreen Bootstrapper for older systems:
//                       https://developer.microsoft.com/microsoft-edge/webview2/
//   3. Help bundle:     copy the contents of  dist\dwsim-help\  into
//                       <DWSIM>\Help\  during installer build.
//
// The bundle is served over a virtual host (https://dwsim.help/) — this avoids
// every file:// quirk: relative URLs, search index XHR, MathJax CDN, anchor
// scrolling all behave identically to a real web server.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DWSIM.Help
{
    public sealed class HelpWindow : Form
    {
        private const string VirtualHost = "dwsim.help";
        private readonly WebView2 _webView = new WebView2 { Dock = DockStyle.Fill };

        public HelpWindow()
        {
            Text = "DWSIM User Guide";
            Width = 1200;
            Height = 800;
            StartPosition = FormStartPosition.CenterParent;
            Controls.Add(_webView);
            Load += async (_, __) => await InitAsync();
        }

        /// <summary>Open the help and scroll to a specific anchor (e.g. "sec:tea").</summary>
        public static void OpenAt(string anchor = null)
        {
            var w = new HelpWindow();
            if (!string.IsNullOrEmpty(anchor))
                w._initialAnchor = anchor;
            w.Show();
        }

        private string _initialAnchor;

        private async Task InitAsync()
        {
            var helpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help");
            if (!Directory.Exists(helpDir) || !File.Exists(Path.Combine(helpDir, "index.html")))
            {
                MessageBox.Show(this,
                    $"Help bundle not found at:\n{helpDir}\n\n" +
                    "Re-run the DWSIM installer or rebuild the help system " +
                    "via build.py --ship.",
                    "Help unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }

            // Initialize the CoreWebView2 environment in DWSIM's user-data folder
            // so we don't pollute %LOCALAPPDATA% with a generic WebView2 cache.
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DWSIM", "WebView2Cache");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);

            // Map https://dwsim.help/ → <DWSIM>\Help\ (read-only, isolated origin).
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                VirtualHost,
                helpDir,
                CoreWebView2HostResourceAccessKind.Allow);

            // Open all external links (https://github.com/..., etc.) in the user's
            // real default browser instead of inside the help window.
            _webView.CoreWebView2.NavigationStarting += (s, e) =>
            {
                var uri = new Uri(e.Uri);
                if (uri.Host != VirtualHost && uri.Scheme != "about")
                {
                    e.Cancel = true;
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(e.Uri) { UseShellExecute = true });
                }
            };

            var url = $"https://{VirtualHost}/index.html";
            if (!string.IsNullOrEmpty(_initialAnchor))
                url += "#" + _initialAnchor;
            _webView.CoreWebView2.Navigate(url);
        }
    }
}
