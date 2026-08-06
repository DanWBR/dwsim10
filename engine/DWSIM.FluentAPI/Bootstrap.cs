using System;
using System.IO;
using System.Reflection;
using DWSIM.Automation;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Process-wide singleton that hosts a single <see cref="Automation3"/> instance,
    /// used to bootstrap headless flowsheets with all property packages and compounds
    /// pre-loaded. Reused across all <see cref="Flowsheet"/> instances created via the Fluent API.
    /// </summary>
    internal static class Bootstrap
    {
        private static readonly object _gate = new object();
        private static Automation3 _automation;
        private static bool _resolverInstalled;

        /// <summary>
        /// Installs an <see cref="AppDomain.AssemblyResolve"/> handler that probes the
        /// <c>extenders</c>, <c>unitops</c> and <c>ppacks</c> sub-folders next to the
        /// running assembly. Required for the JIT to find Plus / DWSIMPlus assemblies
        /// (LCA, TEA, electrolyte / ThermoPack PPs, refining UOs) before any
        /// <see cref="Flowsheet"/> method that statically references them is called.
        /// Idempotent; safe to call multiple times.
        /// </summary>
        public static void RegisterAssemblyResolver()
        {
            lock (_gate)
            {
                if (_resolverInstalled) return;
                AppDomain.CurrentDomain.AssemblyResolve += ProbeExtensionFolders;
                _resolverInstalled = true;
            }
        }

        private static Assembly ProbeExtensionFolders(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name).Name + ".dll";
            string baseDir;
            try { baseDir = Path.GetDirectoryName(typeof(Bootstrap).Assembly.Location); }
            catch { baseDir = AppDomain.CurrentDomain.BaseDirectory; }

            // Free build uses extenders/unitops/ppacks; Plus build uses *2 suffix.
            string[] dirs =
            {
                baseDir,
                Path.Combine(baseDir ?? "", "extenders"),
                Path.Combine(baseDir ?? "", "extenders2"),
                Path.Combine(baseDir ?? "", "unitops"),
                Path.Combine(baseDir ?? "", "unitops2"),
                Path.Combine(baseDir ?? "", "ppacks"),
                Path.Combine(baseDir ?? "", "ppacks2"),
            };

            foreach (var dir in dirs)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    try { return Assembly.LoadFrom(path); } catch { /* fall through */ }
                }
            }
            return null;
        }

        public static Automation3 Automation
        {
            get
            {
                if (_automation != null) return _automation;
                lock (_gate)
                {
                    if (_automation == null)
                    {
                        // Make sure JIT-time loads of Plus assemblies (LCA/TEA/etc.)
                        // succeed even before the user creates a Flowsheet.
                        if (!_resolverInstalled)
                        {
                            AppDomain.CurrentDomain.AssemblyResolve += ProbeExtensionFolders;
                            _resolverInstalled = true;
                        }
                        _automation = new Automation3();
                    }
                }
                return _automation;
            }
        }
    }
}
