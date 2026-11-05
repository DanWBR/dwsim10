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
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DWSIM.UI.Shared.Avalonia;

namespace DWSIM.Extensions.SolidCraft
{
    /// <summary>
    /// The frame of the import wizards, laid out as the Simulation Setup Wizard: the step and its
    /// description on top, the steps listed on the left (a click goes to one), the page in the
    /// middle, and a note with Back, Next, Finish and Close at the bottom.
    /// </summary>
    internal sealed class WizardFrame
    {
        readonly string[] steps, texts;
        readonly Control[] pages;
        readonly List<TextBlock> labels = new List<TextBlock>();
        readonly TextBlock title = new TextBlock { FontSize = 16, FontWeight = FontWeight.Bold };
        readonly TextBlock description = new TextBlock { TextWrapping = TextWrapping.Wrap };
        readonly TextBlock footer = new TextBlock { FontSize = UiScale.Font(11), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        readonly ContentControl host = new ContentControl { Margin = new Thickness(12, 10) };
        bool nextAllowed = true;

        public readonly Button Back = Make("< Back"), Next = Make("Next >"), Finish = Make("Finish"), Close = Make("Close");

        /// <summary>Leaving a page for another (from, to): where a page takes its edits.</summary>
        public Action<int, int> Leaving;

        /// <summary>A page just shown: where it is filled.</summary>
        public Action<int> Entered;

        /// <summary>Whether a page past the first can be shown (a document has been read).</summary>
        public Func<bool> Ready = () => true;

        public int Page { get; private set; } = -1;

        public WizardFrame(Action close, string[] steps, string[] texts, Control[] pages, string note)
        {
            this.steps = steps;
            this.texts = texts;
            this.pages = pages;
            footer.Text = note;
            Back.Click += (_, _) => Show(Page - 1);
            Next.Click += (_, _) => Show(Page + 1);
            Finish.Click += (_, _) => close();
            Close.Click += (_, _) => close();
            Close.IsCancel = true;
        }

        static Button Make(string text)
        {
            var b = new Button { Content = text, Width = 90 };
            b.Classes.Add("wizard");
            return b;
        }

        /// <summary>Whether Next may go on from the first page (false while nothing has been read, or while reading).</summary>
        public bool NextAllowed
        {
            set { nextAllowed = value; Update(); }
        }

        public Control Build()
        {
            var list = new StackPanel { Spacing = 2 };
            for (var i = 0; i < steps.Length; i++)
            {
                var label = new TextBlock { Text = (i + 1) + ". " + steps[i], FontSize = UiScale.Font(12), TextWrapping = TextWrapping.Wrap };
                // the steps are buttons so a step is only reached on a click, never on a stray press
                var button = new Button
                {
                    Content = label,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(2, 3),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                };
                var step = i;
                button.Click += (_, _) => Show(step);
                labels.Add(label);
                list.Children.Add(button);
            }

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Back, Next, Finish, Close } };
            DockPanel.SetDock(buttons, Dock.Right);
            var bottom = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(12, 8), Child = new DockPanel { Children = { buttons, footer } } };
            var top = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(16, 10), Child = new StackPanel { Spacing = 2, Children = { title, description } } };
            var left = new Border { Width = 190, Padding = new Thickness(10), BorderThickness = new Thickness(0, 0, 1, 0), Child = list };
            DockPanel.SetDock(bottom, Dock.Bottom);
            DockPanel.SetDock(top, Dock.Top);
            DockPanel.SetDock(left, Dock.Left);
            return new DockPanel { LastChildFill = true, Children = { bottom, top, left, host } };
        }

        public void Show(int page)
        {
            if (page < 0 || page >= pages.Length || page == Page) return;
            if (page > 0 && !Ready()) return;
            if (Page >= 0) Leaving?.Invoke(Page, page);
            Page = page;
            host.Content = pages[page];
            title.Text = "Step " + (page + 1) + " of " + pages.Length + " - " + steps[page];
            description.Text = texts[page];
            for (var i = 0; i < labels.Count; i++)
            {
                labels[i].FontWeight = i == page ? FontWeight.Bold : FontWeight.Normal;
                labels[i].Opacity = i == page ? 1.0 : 0.7;
            }
            Update();
            Entered?.Invoke(page);
        }

        void Update()
        {
            Back.IsEnabled = Page > 0;
            Next.IsEnabled = Page < pages.Length - 1 && (Page > 0 || nextAllowed);
            Finish.IsVisible = Page == pages.Length - 1;
        }

        /// <summary>Locks the frame while something runs on the last page (the fit), and frees it after.</summary>
        public void Lock(bool locked)
        {
            Back.IsEnabled = !locked && Page > 0;
            Finish.IsEnabled = Close.IsEnabled = !locked;
        }
    }
}
