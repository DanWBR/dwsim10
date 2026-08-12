//    Entry point of the DWSIM desktop application.
//
//    This file is part of DWSIM.
//
//    DWSIM is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    DWSIM is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

using Avalonia;
using Avalonia.WebView.Desktop;
using System;

namespace DWSIM.UI.Desktop.Avalonia;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // IronPython reads the console encoding while it builds its standard streams, and a legacy
        // code page has no data item there, so the language fails to load with "No data is
        // available for encoding 1252". Registering the provider and running the console in UTF-8
        // keeps that path on an encoding the runtime knows.
        PrepareTextEncodings();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void PrepareTextEncodings()
    {
        // the legacy code pages, which the engine reads simulation files and databases with
        try { System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance); }
        catch (Exception ex) { Console.WriteLine("Could not register the legacy code pages: " + ex.Message); }

        // scripts run through IronPython, which builds its standard streams from the console
        // encoding; UTF-8 is one the runtime can describe, a legacy code page is not
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
        catch (Exception) { }

        try { Console.InputEncoding = System.Text.Encoding.UTF8; }
        catch (Exception) { }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        // No .WithInterFont(): use each platform's own UI font (Segoe UI on Windows, San Francisco
        // on macOS, the fontconfig default on Linux), which the OS rasterises with native hinting and
        // reads sharper than the bundled Inter, closer to the WinForms edition.
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseDesktopWebView()
            .LogToTrace();

        // WSLg and some VMs present a blank window with the GPU compositor; force Avalonia's own
        // software renderer when asked, so the app can be verified there. Opt-in, off by default.
        if (Environment.GetEnvironmentVariable("DWSIM_SOFTWARE_RENDER") == "1")
            builder = builder.With(new global::Avalonia.X11PlatformOptions
            {
                RenderingMode = new[] { global::Avalonia.X11RenderingMode.Software }
            });

        return builder;
    }
}
