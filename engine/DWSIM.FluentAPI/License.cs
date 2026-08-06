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

namespace DWSIM.Automation.FluentAPI
{
    /// <summary>
    /// Some unit operations and property packages are part of the Patreon edition, which carries
    /// the licensing assembly this build does not. Their entry points are still here, and say so.
    /// </summary>
    public static class License
    {
        /// <summary>Always false in this build: there is nothing here to activate.</summary>
        public static bool IsActivated
        {
            get { return false; }
        }

        /// <summary>
        /// Refuses a component that belongs to the Patreon edition.
        /// </summary>
        public static void RequirePlus()
        {
            throw new NotSupportedException(
                "This component is part of the Patreon edition of DWSIM and is not available in " +
                "this build. See https://dwsim.org for the editions.");
        }
    }
}
