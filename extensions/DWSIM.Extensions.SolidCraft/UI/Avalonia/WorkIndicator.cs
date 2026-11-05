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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
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

        string localAccepted;

        /// <summary>Where the model the assistant reads with is shown (the first page).</summary>
        public readonly TextBlock ModelText = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold };

        /// <summary>Shows the model the assistant reads with, starting the assistant when it is not running; the window stays free meanwhile.</summary>
        public async void ShowModel()
        {
            ModelText.Text = "Asking the AI Assistant which model it reads with...";
            try
            {
                var m = await Task.Run(() => DrawingImportClient.Model(CancellationToken.None));
                ModelText.Text = DrawingImportClient.ModelLine(m);
            }
            catch (Exception ex)
            {
                ModelText.Text = "The AI Assistant's model could not be read: " + ex.GetBaseException().Message;
            }
        }

        /// <summary>
        /// The model the assistant will read with; null when it is a local model met for the first
        /// time, which the status then explains, so that the next read goes ahead with it.
        /// </summary>
        public async Task<DrawingImportClient.AssistantModel> Model(CancellationToken ct, string what)
        {
            var m = await Task.Run(() => DrawingImportClient.Model(ct, s => Dispatcher.UIThread.Post(() => Step(s))));
            ModelText.Text = DrawingImportClient.ModelLine(m);
            if (!m.Local || localAccepted == m.ToString()) return m;
            localAccepted = m.ToString();
            Stop();
            status.Text = DrawingImportClient.LocalModelNote(m, what);
            return null;
        }

        void Show() => status.Text = step + " (" + DrawingImportClient.Elapsed(Elapsed) + ")";
    }
}
