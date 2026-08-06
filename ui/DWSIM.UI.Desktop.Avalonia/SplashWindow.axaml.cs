using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DWSIM.SharedClasses;
using DWSIM.Thermodynamics.BaseClasses;

namespace DWSIM.UI.Desktop.Avalonia;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        IconHelper.ApplyWindowIcon(this);
        PopulateLabels();
        Opened += OnOpened;
    }

    private void PopulateLabels()
    {
        // Version
        var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        var versionText = $"Version {ver}";
        versionText += Environment.Is64BitProcess
            ? " (Avalonia Cross-Platform UI, 64-bit)"
            : " (Avalonia Cross-Platform UI, 32-bit)";
        LblVersion.Text = versionText;

        // GPL notice is already set in AXAML

        // Copyright - read from assembly attribute
        var attrs = Assembly.GetExecutingAssembly()
            .GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
        LblCopyright.Text = attrs.Length > 0
            ? ((AssemblyCopyrightAttribute)attrs[0]).Copyright
            : "Copyright © 2026 Daniel Wagner and contributors";

        // Patrons placeholder until async load completes
        LblPatrons.Text = "Special thanks to the following Patrons/Sponsors: Loading...";
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // Background task: load patrons + user compounds, enforce the minimum display time, then close
        Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Load patrons list
            try
            {
                var patronsList = Patrons.GetList();
                Dispatcher.UIThread.Post(() =>
                    LblPatrons.Text = "Special thanks to the following Patrons/Sponsors: " + patronsList);
            }
            catch
            {
                Dispatcher.UIThread.Post(() =>
                    LblPatrons.Text = "Special thanks to all our Patrons and Sponsors!");
            }

            // Load user compound databases
            LoadUserCompounds();

            // Ensure at least 5 seconds of splash display
            sw.Stop();
            var remaining = 4000 - (int)sw.ElapsedMilliseconds;
            if (remaining > 0)
                System.Threading.Thread.Sleep(remaining);

            // Close splash on the UI thread
            Dispatcher.UIThread.Post(() => Close());
        });
    }

    private void LoadUserCompounds()
    {
        // Load user compounds into the MainWindow that opened us
        var mainWindow = Owner as MainWindow;

        foreach (var path in GlobalSettings.Settings.UserDatabases)
        {
            try
            {
                if (Path.GetExtension(path).Equals(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var comp in DWSIM.Thermodynamics.Databases.UserDB.ReadComps(path))
                        Store(mainWindow, comp);
                }
                else if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var comp = Newtonsoft.Json.JsonConvert.DeserializeObject<ConstantProperties>(
                        File.ReadAllText(path));
                    if (comp != null) Store(mainWindow, comp);
                }
            }
            catch
            {
                // Skip individual compound files that fail to load
            }
        }
    }

    /// <summary>
    /// Adds the compound to the list, replacing the one already there under the same name when
    /// the settings say so.
    /// </summary>
    private static void Store(MainWindow? mainWindow, ConstantProperties compound)
    {
        if (mainWindow == null || compound == null) return;

        var existing = mainWindow.UserCompounds.FindIndex(x => x.Name == compound.Name);

        if (existing < 0) mainWindow.UserCompounds.Add(compound);
        else if (GlobalSettings.Settings.ReplaceComps) mainWindow.UserCompounds[existing] = compound;
    }
}
