using System;
using System.IO;

namespace DWSIM.PhaseEquilibriumData.CLI
{
    internal static class AppPaths
    {
        public const string DefaultArchiveUrl = "https://data.nist.gov/od/ds/mds2-2422/ThermoML.v2020-09-30.tgz";
        public const string ArchiveFileName = "ThermoML.v2020-09-30.tgz";
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
        public static string DefaultArchivePath() => Path.Combine(DefaultDataDir(), ArchiveFileName);
    }

    internal static class ExitCodes
    {
        public const int Success = 0;
        public const int UserError = 1;
        public const int DataError = 2;
        public const int NetworkError = 3;
    }
}
