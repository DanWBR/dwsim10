using System;
using System.IO;

namespace DWSIM.PureCompoundData.Index
{
    /// <summary>
    /// Platform-appropriate filesystem locations for the pure-compound cache.
    /// Windows: <c>%LOCALAPPDATA%/DWSIM/PureCompound</c>.
    /// Unix: <c>$XDG_DATA_HOME/DWSIM/PureCompound</c> (or <c>~/.local/share/DWSIM/PureCompound</c>).
    /// </summary>
    public static class CachePaths
    {
        public static string RootDirectory()
        {
            var plat = Environment.OSVersion.Platform;
            string baseDir;
            if (plat == PlatformID.Win32NT)
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            else
            {
                baseDir = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".local", "share");
            }
            var dir = Path.Combine(baseDir, "DWSIM", "PureCompound");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string DefaultDatabasePath()
            => Path.Combine(RootDirectory(), "index.litedb");

        public static string DownloadsDirectory()
        {
            var dir = Path.Combine(RootDirectory(), "downloads");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
