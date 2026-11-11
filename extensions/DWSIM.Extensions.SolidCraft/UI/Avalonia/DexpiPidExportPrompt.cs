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

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DWSIM.Interfaces;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>Asks where the DEXPI P&amp;ID model goes, in the cross-platform interface, and writes it.</summary>
    internal static class DexpiPidExportPrompt
    {
        internal static async void Show(IFlowsheet fs)
        {
            try
            {
                var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var owner = desktop?.Windows.FirstOrDefault(w => w.IsActive) ?? desktop?.MainWindow;
                if (owner == null) return;
                var picked = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export DEXPI P&ID Model",
                    SuggestedFileName = DexpiPidExportExtender.SuggestedName(fs),
                    DefaultExtension = "xml",
                    FileTypeChoices = new[] { new FilePickerFileType("DEXPI models") { Patterns = new[] { "*.xml" } } }
                });
                var path = picked?.TryGetLocalPath();
                if (path != null) DexpiPidExportExtender.Export(fs, path);
            }
            catch (Exception ex)
            {
                try { fs.ShowMessage("DEXPI P&ID export could not open: " + ex.GetBaseException().Message, IFlowsheet.MessageType.GeneralError); } catch { }
            }
        }
    }
}
