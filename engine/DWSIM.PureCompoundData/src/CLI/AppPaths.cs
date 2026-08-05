using System.IO;
using DWSIM.PureCompoundData.Index;

namespace DWSIM.PureCompoundData.CLI
{
    internal static class AppPaths
    {
        public const string DefaultArchiveUrl = "https://data.nist.gov/od/ds/mds2-2422/ThermoML.v2020-09-30.tgz";
        public const string ArchiveFileName = "ThermoML.v2020-09-30.tgz";

        public static string DefaultArchivePath()
            => Path.Combine(CachePaths.DownloadsDirectory(), ArchiveFileName);

        public static string DefaultDbPath() => CachePaths.DefaultDatabasePath();
    }

    internal static class ExitCodes
    {
        public const int Success = 0;
        public const int UserError = 1;
        public const int DataError = 2;
        public const int NetworkError = 3;
    }
}
