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
using Avalonia.Controls;
using Avalonia.Threading;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// What the import windows show while the AI Assistant works: a moving bar, the step under
    /// way and the time it has taken so far, so a reading of a few minutes does not look like a
    /// window that stopped answering.
    /// </summary>
    internal sealed class WorkIndicator
    {
        readonly TextBlock status;
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        DateTime started;
        string step = "";

        /// <summary>The moving bar, to put beside the read buttons; hidden while nothing runs.</summary>
        public readonly ProgressBar Bar = new ProgressBar { IsIndeterminate = true, Width = 180, Height = 6, IsVisible = false, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

        public WorkIndicator(TextBlock status)
        {
            this.status = status;
            timer.Tick += (s, e) => Show();
        }

        public TimeSpan Elapsed => DateTime.Now - started;

        public void Start(string first)
        {
            started = DateTime.Now;
            step = first;
            Bar.IsVisible = true;
            timer.Start();
            Show();
        }

        /// <summary>The step under way; called on the UI thread.</summary>
        public void Step(string text)
        {
            step = text;
            Show();
        }

        public void Stop()
        {
            timer.Stop();
            Bar.IsVisible = false;
        }

        void Show() => status.Text = step + " (" + DrawingImportClient.Elapsed(Elapsed) + ")";
    }
}
