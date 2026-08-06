using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace DWSIM.UI.Desktop.Avalonia;

/// <summary>
/// What the Windows about box shows: what DWSIM is, which version is running, who it belongs to,
/// the licence, what the machine is running it on, and who the release owes thanks to.
/// </summary>
public partial class AboutWindow : Window
{

    private const string Acknowledgements =
        "Some icons by Icons8. https://icons8.com/\n" +
        "\n" +
        "Some icons created by Freepik - https://www.flaticon.com/authors/freepik, " +
        "https://www.flaticon.com/free-icons.\n" +
        "\n" +
        "Some images from Freepik - https://br.freepik.com/fotos-gratis/" +
        "pesquisadores-em-busca-de-fontes-alternativas-de-energia_23668250.htm\n" +
        "\n" +
        "Pressure Relief Valve icon by Michael Senkow from Noun Project (CC BY 3.0)\n" +
        "\n" +
        "Reaktoro Gibbs Reactor icons created by Nhor Phai - Flaticon " +
        "(https://www.flaticon.com/free-icons/reactor).\n" +
        "\n" +
        "Chiller icons created by JK-Icon - Flaticon\n" +
        "\n" +
        "DWSIM uses ChemSep's Pure Compound Data (PCD) file as the main source of pure compound " +
        "data under permission from its authors. ChemSep is Copyright (c) Harry Kooijman and " +
        "Ross Taylor - https://www.chemsep.org.\n" +
        "\n" +
        "DWSIM uses thermo and chemicals libraries as one of the sources of pure compound data. " +
        "thermo and chemicals are Copyright (c) Caleb Bell and Contributors (2016-2021). Thermo: " +
        "Chemical properties component of Chemical Engineering Design Library (ChEDL) - " +
        "https://github.com/CalebBell/thermo.\n" +
        "\n" +
        "DWSIM uses OPEM library for PEM Fuel Cells. Haghighi et al., (2018). OPEM: Open Source " +
        "PEM Cell Simulation Tool. Journal of Open Source Software, 3(27), 676, " +
        "https://doi.org/10.21105/joss.00676\n" +
        "\n" +
        "Reaktoro Gibbs Reactor is based on Reaktoro v1. https://reaktoro.org/v1/ - " +
        "Copyright 2021, Allan Leal and Reaktoro contributors\n" +
        "\n" +
        "Weather conditions are provided by Azure Maps Weather Service.";

    public AboutWindow()
    {
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);

        LoadLogo();
        LoadIdentity();
        LoadMachine();
        LoadLicense();

        TbAcknowledgements.Text = Acknowledgements;

        BtnOK.Click += (_, _) => Close();
        BtnSite.Click += (_, _) => Open("https://dwsim.org");
    }

    private void LoadLogo()
    {
        try
        {
            using var stream = global::Avalonia.Platform.AssetLoader.Open(
                new Uri("avares://DWSIM.UI.Desktop.Avalonia/Assets/dwsim.ico"));

            Logo.Source = new Bitmap(stream);
        }
        catch (Exception)
        {
            // without the icon the header simply starts with the title
            Logo.IsVisible = false;
        }
    }

    private void LoadIdentity()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        // the assembly version is what carries the build number, the file version is declared
        // with a wildcard and comes back as it was written
        var version = assembly.GetName().Version?.ToString() ?? "";

        if (string.IsNullOrEmpty(version) || version.Contains('*'))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(assembly.Location);
                if (!string.IsNullOrEmpty(info.FileVersion)) version = info.FileVersion;
            }
            catch (Exception) { }
        }

        LblVersion.Text = "Version " + version;

        try
        {
            var built = File.GetLastWriteTime(assembly.Location);
            LblVersion.Text += "  (build of " + built.ToString("yyyy-MM-dd HH:mm") + ")";
        }
        catch (Exception) { }

        var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

        LblCopyright.Text = string.IsNullOrEmpty(copyright)
            ? "Copyright © Daniel Wagner Oliveira de Medeiros"
            : copyright;
    }

    private void LoadMachine()
    {
        LblOS.Text = RuntimeInformation.OSDescription + ", " + RuntimeInformation.OSArchitecture +
                     " platform";

        LblClr.Text = RuntimeInformation.FrameworkDescription + " (CLR v" + Environment.Version + ")";

        var cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");

        LblCpu.Text = (string.IsNullOrEmpty(cpu) ? RuntimeInformation.ProcessArchitecture.ToString() : cpu) +
                      " / " + Environment.ProcessorCount + " logical processors";

        try
        {
            var managed = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            var total = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
            LblMemory.Text = managed.ToString("N0") + " MB managed, " + total.ToString("N0") + " MB total";
        }
        catch (Exception) { }
    }

    /// <summary>
    /// Reads the licence shipped with the application. The application folder also carries the
    /// licences of the libraries DWSIM uses, so only a file that really is the GPL is taken.
    /// </summary>
    private void LoadLicense()
    {
        foreach (var name in new[] { "gpl-3.0.txt", "COPYING", "LICENSE", "LICENSE.txt", "license.txt" })
        {
            foreach (var dir in new[] { AppContext.BaseDirectory,
                                        Path.Combine(AppContext.BaseDirectory, "..") })
            {
                try
                {
                    var path = Path.GetFullPath(Path.Combine(dir, name));
                    if (!File.Exists(path)) continue;

                    var text = File.ReadAllText(path);
                    if (text.IndexOf("GNU GENERAL PUBLIC LICENSE",
                            StringComparison.OrdinalIgnoreCase) < 0) continue;

                    TbLicense.Text = text;
                    return;
                }
                catch (Exception) { }
            }
        }

        TbLicense.Text =
            "DWSIM is free software: you can redistribute it and/or modify it under the terms of " +
            "the GNU General Public License as published by the Free Software Foundation, either " +
            "version 3 of the License, or (at your option) any later version." + Environment.NewLine +
            Environment.NewLine +
            "DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; " +
            "without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR " +
            "PURPOSE. See the GNU General Public License for more details." + Environment.NewLine +
            Environment.NewLine +
            "You should have received a copy of the GNU General Public License along with DWSIM. " +
            "If not, see https://www.gnu.org/licenses/.";
    }

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception) { }
    }

}
