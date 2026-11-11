// Copyright 2026 Daniel Wagner O. de Medeiros
//
// This file is part of DWSIM.
//
// DWSIM is free software: you can redistribute it and/or modify it under the terms of the GNU
// General Public License as published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// DWSIM is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even
// the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General
// Public License for more details.
//
// You should have received a copy of the GNU General Public License along with DWSIM. If not, see
// <http://www.gnu.org/licenses/>.

using System.Windows.Forms;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>Asks where the DEXPI P&amp;ID model goes, in the Windows interface, and writes it.</summary>
    internal static class DexpiPidExportPrompt
    {
        internal static void Show(IFlowsheet fs)
        {
            using (var d = new SaveFileDialog { Title = "Export DEXPI P&ID Model", Filter = "DEXPI models (*.xml)|*.xml", FileName = DexpiPidExportExtender.SuggestedName(fs) })
            {
                if (d.ShowDialog(Form.ActiveForm) != DialogResult.OK) return;
                DexpiPidExportExtender.Export(fs, d.FileName);
            }
        }
    }
}
