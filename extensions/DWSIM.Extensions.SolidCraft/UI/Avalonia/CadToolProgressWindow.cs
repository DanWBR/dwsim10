//    Copyright 2026 Daniel Wagner O. de Medeiros
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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The window of a CAD tool running in the cross-platform interface: the step it is on, the time
    /// it has taken and a Cancel button; when it is over, what happened and a button to open the
    /// folder. Closing it while the tool runs leaves the tool running.
    /// </summary>
    public sealed class CadToolProgressWindow : PlusWindow
    {
        readonly CadJob Job;
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        readonly TextBlock step, elapsed;
        readonly ProgressBar bar;
        readonly Button cancel, close, openFolder;

        public CadToolProgressWindow(CadJob job)
        {
            Job = job;
            Title = job.Title;
            using (var icon = job.Request.Icon())
                if (icon != null) Icon = new WindowIcon(icon);
            Width = 480;
            SizeToContent = SizeToContent.Height;
            CanResize = false;

            step = new TextBlock { TextWrapping = TextWrapping.Wrap, MinHeight = 34, Margin = new Thickness(0, 0, 0, 8) };
            bar = new ProgressBar { IsIndeterminate = true, Minimum = 0, Maximum = 100, Height = 16 };
            elapsed = new TextBlock { Opacity = 0.7, Margin = new Thickness(0, 6, 0, 0), FontSize = UiScale.Font(12) };

            cancel = new Button { Content = "Cancel", MinWidth = 90 };
            cancel.Classes.Add("dialog");
            cancel.Click += (_, _) =>
            {
                cancel.IsEnabled = false;
                Job.Cancel();
                step.Text = Job.Step;
            };
            close = new Button { Content = "Close", MinWidth = 90, IsVisible = false };
            close.Classes.Add("dialog");
            close.Click += (_, _) => Close();
            openFolder = new Button { Content = "Open Folder", MinWidth = 90, IsVisible = false };
            openFolder.Classes.Add("dialog");
            openFolder.Click += (_, _) => Job.OpenFolder();

            Content = new StackPanel
            {
                Margin = new Thickness(14, 12, 14, 12),
                Children =
                {
                    step,
                    bar,
                    elapsed,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 12, 0, 0),
                        Children = { openFolder, close, cancel }
                    }
                }
            };

            timer.Tick += (_, _) => UpdateState();
            Closed += (_, _) => timer.Stop();
            UpdateState();
            timer.Start();
        }

        void UpdateState()
        {
            elapsed.Text = (Job.Finished ? "Time: " : "Elapsed: ") + CadJob.Duration(Job.Elapsed);
            if (!Job.Finished)
            {
                step.Text = Job.Step;
                return;
            }
            timer.Stop();
            step.Text = CadJob.Shorten(Job.Outcome);
            if (Job.Failed) step.Foreground = Brushes.Firebrick;
            else if (!Job.Cancelled) step.Foreground = Brushes.SeaGreen;
            bar.IsIndeterminate = false;
            bar.Value = Job.Failed || Job.Cancelled ? 0 : 100;
            cancel.IsVisible = false;
            close.IsVisible = true;
            close.IsDefault = close.IsCancel = true;
            openFolder.IsVisible = Directory.Exists(Job.OutputFolder ?? "");
        }
    }
}
