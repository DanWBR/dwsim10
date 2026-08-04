using NLog;
using System;
using System.IO;

namespace DWSIM.Logging
{
    public class Logger
    {
        private enum Platform
        {
            Windows,
            Linux,
            Mac,
        }

        private static NLog.Logger logger;

        private static bool initialized = false;

        static Logger()
        {
            Initialize(InitializeFilesystemLogs);
        }

        public static void Initialize(Action<NLog.Config.LoggingConfiguration> configFactory, bool overrideExisting = false)
        {
            if (initialized && !overrideExisting) 
                return;

            var config = new NLog.Config.LoggingConfiguration();

            configFactory(config);

            // Apply config           
            NLog.LogManager.Configuration = config;

            logger = NLog.LogManager.GetCurrentClassLogger();

            initialized = true;
        }

        private static void InitializeFilesystemLogs(NLog.Config.LoggingConfiguration config)
        {
            var logfiledir = "";

            if (RunningPlatform() == Platform.Mac)
                logfiledir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Documents", "DWSIM Application Data");
            else
                logfiledir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DWSIM Application Data");

            if (!Directory.Exists(logfiledir))
                Directory.CreateDirectory(logfiledir);

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = Path.Combine(logfiledir, "errors.log") };
            var layout = NLog.Layouts.Layout.FromString("${longdate} ${message:exceptionSeparator=\r\n:withException=true}", throwConfigExceptions: true);
            logfile.Layout = layout;

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logfile);
        }

        public static void LogError(string message, Exception ex)
        {
            logger.Error(ex, message);
        }

        public static void LogUnhandled(string message, Exception ex)
        {
            logger.Fatal(ex, message);
        }

        public static void LogWarning(string message, Exception ex)
        {
            logger.Warn(ex, message);
        }

        public static void LogInfo(string message)
        {
            logger.Info(message);
        }

        private static Platform RunningPlatform()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    //  Well, there are chances MacOSX is reported as Unix instead of MacOSX.
                    //  Instead of platform check, we'll do a feature checks (Mac specific root folders)
                    if ((Directory.Exists("/Applications")
                                && (Directory.Exists("/System")
                                && (Directory.Exists("/Users") && Directory.Exists("/Volumes")))))
                    {
                        return Platform.Mac;
                    }
                    else
                    {
                        return Platform.Linux;
                    }
                case PlatformID.MacOSX:
                    return Platform.Mac;
                default:
                    return Platform.Windows;
            }
        }
    }
}
