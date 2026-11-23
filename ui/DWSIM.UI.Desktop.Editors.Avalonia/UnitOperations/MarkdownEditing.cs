using System;
using Avalonia.Controls;

namespace DWSIM.UI.Desktop.Editors
{
    /// <summary>
    /// The formatting buttons of the Markdown text editor: they wrap the selection in Markdown marks,
    /// mark the lines of the selection (headings, lists, quotes) or insert a block (table, code, rule).
    /// </summary>
    internal static class MarkdownEditing
    {

        /// <summary>Wraps the selection in <paramref name="before"/> and <paramref name="after"/>, or inserts a placeholder.</summary>
        public static void Wrap(TextBox box, string before, string after, string placeholder)
        {
            var text = box.Text ?? "";
            var (start, end) = Selection(box, text);
            var selected = text.Substring(start, end - start);
            var inner = selected.Length > 0 ? selected : placeholder;
            box.Text = text.Substring(0, start) + before + inner + after + text.Substring(end);
            box.SelectionStart = start + before.Length;
            box.SelectionEnd = start + before.Length + inner.Length;
        }

        /// <summary>Puts <paramref name="prefix"/> at the start of each line of the selection (or of the caret's line).</summary>
        public static void PrefixLine(TextBox box, string prefix)
        {
            var text = box.Text ?? "";
            var (start, end) = Selection(box, text);
            var lineStart = start > 0 ? text.LastIndexOf('\n', start - 1) + 1 : 0;
            var lineEnd = text.IndexOf('\n', Math.Max(end, lineStart));
            if (lineEnd < 0) lineEnd = text.Length;
            var lines = text.Substring(lineStart, lineEnd - lineStart).Split('\n');
            var numbered = prefix == "1. ";
            for (var i = 0; i < lines.Length; i++)
                lines[i] = (numbered ? (i + 1) + ". " : prefix) + lines[i];
            var block = string.Join("\n", lines);
            box.Text = text.Substring(0, lineStart) + block + text.Substring(lineEnd);
            box.SelectionStart = box.SelectionEnd = lineStart + block.Length;
        }

        /// <summary>Inserts a block at the caret, on its own lines with a blank line around it.</summary>
        public static void InsertBlock(TextBox box, string block)
        {
            var text = box.Text ?? "";
            var (start, end) = Selection(box, text);
            var before = text.Substring(0, start);
            var lead = before.Length == 0 ? "" : before.EndsWith("\n\n") ? "" : before.EndsWith("\n") ? "\n" : "\n\n";
            var insert = lead + block + "\n\n";
            box.Text = before + insert + text.Substring(end).TrimStart('\n');
            box.SelectionStart = box.SelectionEnd = start + insert.Length;
        }

        private static (int start, int end) Selection(TextBox box, string text)
        {
            var a = Math.Max(0, Math.Min(text.Length, box.SelectionStart));
            var b = Math.Max(0, Math.Min(text.Length, box.SelectionEnd));
            return (Math.Min(a, b), Math.Max(a, b));
        }

    }
}
