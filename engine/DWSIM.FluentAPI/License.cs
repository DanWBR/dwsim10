//    The gate in front of the components that belong to the Patreon edition.
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

using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Some unit operations and property packages are part of the Patreon edition. Their entry points
    /// are here in every build. In the open-source build they refuse; in a Patreon Edition install,
    /// which ships its licensing assembly (DWSIM.Support.dll) next to this one, they work once the
    /// membership key is validated with <see cref="ActivateWithKey"/>.
    /// </summary>
    public static class License
    {
        private static readonly object _lock = new object();
        private static bool _probed;
        private static Type _initialization;

        /// <summary>
        /// The Patreon Edition's licensing class, or null in the open-source build. Loaded with
        /// LoadFrom, as the desktop app does, so the Patreon components see the same instance.
        /// </summary>
        private static Type Initialization
        {
            get
            {
                lock (_lock)
                {
                    if (_probed) return _initialization;
                    _probed = true;
                    try
                    {
                        var support = AppDomain.CurrentDomain.GetAssemblies()
                            .FirstOrDefault(a => a.GetName().Name == "DWSIM.Support");
                        if (support == null)
                        {
                            foreach (var dir in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(typeof(License).Assembly.Location) })
                            {
                                var path = Path.Combine(dir ?? "", "DWSIM.Support.dll");
                                if (File.Exists(path))
                                {
                                    support = Assembly.LoadFrom(path);
                                    break;
                                }
                            }
                        }
                        _initialization = support?.GetType("DWSIM.Support.Initialization");
                    }
                    catch
                    {
                        _initialization = null;
                    }
                    return _initialization;
                }
            }
        }

        /// <summary>True when this install carries the Patreon components (DWSIM.Support.dll).</summary>
        public static bool IsPatreonEdition
        {
            get { return Initialization != null; }
        }

        /// <summary>
        /// The membership's access level: 0 in the open-source build or before the key is validated.
        /// </summary>
        public static int AccessLevel
        {
            get
            {
                var init = Initialization;
                if (init == null) return 0;
                try { return (int)init.GetMethod("GetAccessLevel").Invoke(null, null); }
                catch { return 0; }
            }
        }

        /// <summary>True when the Patreon components are unlocked.</summary>
        public static bool IsActivated
        {
            get { return AccessLevel > 0; }
        }

        /// <summary>What the last <see cref="ActivateWithKey"/> found, for a log. Never contains the key.</summary>
        public static string LastMessage { get; private set; } = "";

        /// <summary>
        /// Validates the membership key with the licensing server (Patreon Edition only). An empty key
        /// uses the one the Patreon desktop app saved for the same OS user. The key is kept in memory
        /// for this process only.
        /// </summary>
        /// <returns>True when the Patreon components are unlocked; <see cref="LastMessage"/> says why not.</returns>
        public static bool ActivateWithKey(string key)
        {
            var init = Initialization;
            if (init == null)
            {
                LastMessage = "this build has no Patreon components";
                return false;
            }
            var activate = init.GetMethod("ActivateHeadless", BindingFlags.Public | BindingFlags.Static);
            if (activate == null)
            {
                LastMessage = "this DWSIM.Support.dll cannot validate a key without the desktop app";
                return false;
            }
            try
            {
                var level = (int)activate.Invoke(null, new object[] { key ?? "" });
                LastMessage = ((init.GetProperty("HeadlessActivationMessage")?.GetValue(null) as string) ?? "").Trim().TrimEnd('.');
                return level > 0;
            }
            catch (Exception ex)
            {
                LastMessage = (ex.InnerException ?? ex).Message;
                return false;
            }
        }

        /// <summary>Same as <see cref="ActivateWithKey"/>.</summary>
        public static bool CheckLicense(string key)
        {
            return ActivateWithKey(key);
        }

        /// <summary>
        /// Refuses a component that belongs to the Patreon edition, unless this is a Patreon Edition
        /// install with a validated key.
        /// </summary>
        public static void RequirePlus()
        {
            if (Initialization == null)
                throw new NotSupportedException(
                    "This component is part of the Patreon edition of DWSIM and is not available in " +
                    "this build. See https://dwsim.org for the editions.");
            if (!IsActivated)
                throw new InvalidOperationException(
                    "This component needs an active DWSIM Patreon membership. Validate the key with " +
                    "License.ActivateWithKey(key) first (on the MCP server: DWSIM_LICENSE_KEY or --license-key).");
        }
    }
}
