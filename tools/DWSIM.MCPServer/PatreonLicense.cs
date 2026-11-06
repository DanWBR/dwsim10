//    Validates the Patreon membership key at startup, when the server runs in a Patreon Edition install.
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
using DWSIM.Automation.FluentAPI;

namespace DWSIM.MCPServer
{
    /// <summary>
    /// The open build carries no licensing. The Patreon Edition's server ships DWSIM.Support.dll next
    /// to the executable, with the Patreon property packages and unit operations, and those cannot be
    /// created or saved until the membership is validated. This validates it at startup.
    /// </summary>
    internal static class PatreonLicense
    {
        /// <summary>
        /// Validates <paramref name="key"/>, or the key the Patreon app saved for this OS user when it
        /// is empty, and logs the outcome to stderr. The key itself is never logged.
        /// </summary>
        internal static void Activate(string key)
        {
            if (!License.IsPatreonEdition)
            {
                if (!string.IsNullOrWhiteSpace(key))
                    Console.Error.WriteLine("[dwsim-mcp] A licence key was given, but this is the open-source build: it has no Patreon components to unlock.");
                return;
            }

            Console.Error.WriteLine("[dwsim-mcp] Validating the Patreon licence...");
            Console.Error.WriteLine(License.ActivateWithKey(key)
                ? $"[dwsim-mcp] Patreon licence active: {License.LastMessage}."
                : $"[dwsim-mcp] Patreon components locked: {License.LastMessage}. Set DWSIM_LICENSE_KEY or pass --license-key.");
        }
    }
}
