using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;
using TextCopy;

namespace TTMG.ConsoleEditor
{
    public class Editor
    {
        private List<string> lines = new List<string> { "" };
        private int cursorRow = 0;
        private int cursorCol = 0;
        private string? filePath;
        private bool isDirty = false;
        private bool running = true;
        private int scrollRow = 0;

        private int? selStartRow;
        private int? selStartCol;

        public Editor(string? path = null)
        {
            filePath = path;
            if (path != null && File.Exists(path))
            {
                lines = File.ReadAllLines(path).ToList();
                if (lines.Count == 0) lines.Add("");
            }
        }

        private int lastWidth;
        private int lastHeight;

        private string[]? lastRenderedLines;
        private string? lastStatus;
        private int lastScrollRow = -1;

        public void Run()
        {
            Console.Clear();
            lastWidth = Console.WindowWidth;
            lastHeight = Console.WindowHeight;
            Render(force: true);
            while (running)
            {
                if (Console.WindowWidth != lastWidth || Console.WindowHeight != lastHeight)
                {
                    lastWidth = Console.WindowWidth;
                    lastHeight = Console.WindowHeight;
                    Console.Clear();
                    Render(force: true);
                }

                if (Console.KeyAvailable)
                {
                    HandleInput();
                    if (running) Render();
                }
                else
                {
                    System.Threading.Thread.Sleep(10);
                }
            }
            Console.Clear();
        }

        private void Render(bool force = false)
        {
            var windowHeight = Console.WindowHeight;
            var windowWidth = Console.WindowWidth;
            var height = Math.Max(1, windowHeight - 1);
            var width = Math.Max(1, windowWidth);

            // Simple scrolling
            if (cursorRow < scrollRow) scrollRow = cursorRow;
            if (cursorRow >= scrollRow + height) scrollRow = Math.Max(0, cursorRow - height + 1);

            if (force || lastRenderedLines == null || lastRenderedLines.Length != height || scrollRow != lastScrollRow)
            {
                lastRenderedLines = new string[height];
                lastScrollRow = scrollRow;
                force = true;
            }

            for (int i = 0; i < height; i++)
            {
                int lineIdx = i + scrollRow;
                string contentToRender;
                bool isSelectedLine = selStartRow.HasValue && 
                    ((lineIdx >= selStartRow && lineIdx <= cursorRow) || (lineIdx >= cursorRow && lineIdx <= selStartRow));

                if (lineIdx < lines.Count)
                {
                    if (isSelectedLine)
                    {
                        contentToRender = RenderLineWithSelection(lineIdx, width);
                    }
                    else
                    {
                        contentToRender = Highlight(lines[lineIdx]);
                    }
                }
                else
                {
                    contentToRender = "[grey]~[/]";
                }

                // If selection is active on this line, we bypass the cache comparison to ensure it updates
                if (force || isSelectedLine || lastRenderedLines[i] != contentToRender)
                {
                    Console.SetCursorPosition(0, i);
                    
                    var plainText = Regex.Replace(contentToRender, "\\[.*?\\]", "").Replace("[[", "[").Replace("]]", "]");
                    var spacesToAdd = Math.Max(0, width - plainText.Length);
                    var lineToRender = contentToRender + new string(' ', spacesToAdd);
                    
                    if (plainText.Length > width)
                    {
                        AnsiConsole.Markup(Markup.Escape(plainText.Substring(0, width)));
                    }
                    else
                    {
                        AnsiConsole.Markup(lineToRender);
                    }
                    lastRenderedLines[i] = contentToRender;
                }
            }

            // Status bar
            if (windowHeight > 0)
            {
                var status = $" {filePath ?? "New File"} {(isDirty ? "*" : "")} | Line: {cursorRow + 1}, Col: {cursorCol + 1} | Ctrl+O: Save, Ctrl+X: Exit, Ctrl+K: Cut";
                if (force || status != lastStatus)
                {
                    Console.SetCursorPosition(0, windowHeight - 1);
                    AnsiConsole.Markup($"[invert]{Markup.Escape(status.PadRight(width).Substring(0, width))}[/]");
                    lastStatus = status;
                }
            }

            // Clamp cursor position to window bounds
            var finalCursorRow = Math.Clamp(cursorRow - scrollRow, 0, height - 1);
            var visualCol = GetVisualColumn(lines[cursorRow], cursorCol);
            var finalCursorCol = Math.Clamp(visualCol, 0, width - 1);
            Console.SetCursorPosition(finalCursorCol, finalCursorRow);
        }

        private int GetVisualColumn(string line, int colIndex)
        {
            int visualCol = 0;
            for (int i = 0; i < Math.Min(colIndex, line.Length); i++)
            {
                if (line[i] == '\t') visualCol += 4;
                else visualCol++;
            }
            return visualCol;
        }

        private string RenderLineWithSelection(int lineIdx, int width)
        {
            var line = lines[lineIdx];
            var (startR, startC, endR, endC) = GetSelectionRange();

            if (lineIdx < startR || lineIdx > endR) return Highlight(line);

            if (lineIdx > startR && lineIdx < endR)
            {
                return $"[invert]{Markup.Escape(line)}[/]";
            }

            if (startR == endR)
            {
                var before = line.Substring(0, startC);
                var selected = line.Substring(startC, endC - startC);
                var after = line.Substring(endC);
                return Highlight(before) + $"[invert]{Markup.Escape(selected)}[/]" + Highlight(after);
            }

            if (lineIdx == startR)
            {
                var before = line.Substring(0, startC);
                var selected = line.Substring(startC);
                return Highlight(before) + $"[invert]{Markup.Escape(selected)}[/]";
            }

            if (lineIdx == endR)
            {
                var selected = line.Substring(0, endC);
                var after = line.Substring(endC);
                return $"[invert]{Markup.Escape(selected)}[/]" + Highlight(after);
            }

            return Highlight(line);
        }

        private (int startR, int startC, int endR, int endC) GetSelectionRange()
        {
            if (!selStartRow.HasValue || !selStartCol.HasValue) return (0, 0, 0, 0);
            if (selStartRow < cursorRow || (selStartRow == cursorRow && selStartCol < cursorCol))
                return (selStartRow.Value, selStartCol.Value, cursorRow, cursorCol);
            return (cursorRow, cursorCol, selStartRow.Value, selStartCol.Value);
        }

        private string Highlight(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";

            var extension = Path.GetExtension(filePath)?.ToLower();
            if (extension == ".lua") return HighlightLua(line);
            if (extension == ".yaml" || extension == ".yml") return HighlightYaml(line);

            return Markup.Escape(line);
        }

        private string HighlightLua(string line)
        {
            var escaped = Markup.Escape(line);
            var keywords = new[] { "local", "function", "end", "if", "then", "else", "elseif", "for", "while", "do", "repeat", "until", "return", "break", "true", "false", "nil", "not", "and", "or" };
            
            string highlighted = escaped;
            foreach (var k in keywords)
            {
                highlighted = Regex.Replace(highlighted, $@"\b{k}\b", $"[blue]{k}[/]");
            }
            
            highlighted = Regex.Replace(highlighted, "\".*?\"", "[green]$0[/]");
            highlighted = Regex.Replace(highlighted, "'.*?'", "[green]$0[/]");
            
            if (highlighted.Contains("--"))
            {
                var idx = highlighted.IndexOf("--");
                highlighted = highlighted.Substring(0, idx) + "[grey]" + highlighted.Substring(idx) + "[/]";
            }

            return highlighted;
        }

        private string HighlightYaml(string line)
        {
            var escaped = Markup.Escape(line);
            string highlighted = Regex.Replace(escaped, @"^(\s*)([\w-]+):", "$1[blue]$2[/]:");
            highlighted = Regex.Replace(highlighted, @":\s+(.*)$", ": [green]$1[/]");
            
            if (highlighted.Contains("#"))
            {
                var idx = highlighted.IndexOf("#");
                highlighted = highlighted.Substring(0, idx) + "[grey]" + highlighted.Substring(idx) + "[/]";
            }
            return highlighted;
        }

        private void HandleInput()
        {
            var key = Console.ReadKey(true);
            bool shift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);

            if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                switch (key.Key)
                {
                    case ConsoleKey.X: running = false; break;
                    case ConsoleKey.O: Save(); break;
                    case ConsoleKey.V: Paste(); break;
                    case ConsoleKey.C: Copy(); break;
                    case ConsoleKey.K: Cut(); break;
                }
                return;
            }

            if (shift && !selStartRow.HasValue)
            {
                selStartRow = cursorRow;
                selStartCol = cursorCol;
            }
            else if (!shift && (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow || key.Key == ConsoleKey.Home || key.Key == ConsoleKey.End))
            {
                selStartRow = null;
                selStartCol = null;
            }

            switch (key.Key)
            {
                case ConsoleKey.Backspace:
                    if (selStartRow.HasValue) DeleteSelection();
                    else if (cursorCol > 0)
                    {
                        lines[cursorRow] = lines[cursorRow].Remove(cursorCol - 1, 1);
                        cursorCol--;
                        isDirty = true;
                    }
                    else if (cursorRow > 0)
                    {
                        cursorCol = lines[cursorRow - 1].Length;
                        lines[cursorRow - 1] += lines[cursorRow];
                        lines.RemoveAt(cursorRow);
                        cursorRow--;
                        isDirty = true;
                    }
                    break;
                case ConsoleKey.Delete:
                    if (selStartRow.HasValue) DeleteSelection();
                    else if (cursorCol < lines[cursorRow].Length)
                    {
                        lines[cursorRow] = lines[cursorRow].Remove(cursorCol, 1);
                        isDirty = true;
                    }
                    else if (cursorRow < lines.Count - 1)
                    {
                        lines[cursorRow] += lines[cursorRow + 1];
                        lines.RemoveAt(cursorRow + 1);
                        isDirty = true;
                    }
                    break;
                case ConsoleKey.Enter:
                    if (selStartRow.HasValue) DeleteSelection();
                    var currentLine = lines[cursorRow];
                    var remaining = currentLine.Substring(cursorCol);
                    var beforeCursor = currentLine.Substring(0, cursorCol);
                    
                    lines[cursorRow] = beforeCursor;
                    var indent = Regex.Match(beforeCursor, @"^\s*").Value;
                    
                    // Smart indent triggers
                    var trimmedBefore = beforeCursor.TrimEnd();
                    var extension = Path.GetExtension(filePath)?.ToLower();
                    
                    if (extension == ".lua")
                    {
                        if (trimmedBefore.EndsWith("then") || trimmedBefore.EndsWith("do") || 
                            trimmedBefore.EndsWith("{") || trimmedBefore.EndsWith("(") ||
                            Regex.IsMatch(trimmedBefore, @"\bfunction\b.*$"))
                        {
                            indent += "\t";
                        }
                    }
                    else if (extension == ".yaml" || extension == ".yml")
                    {
                        if (trimmedBefore.EndsWith(":"))
                        {
                            indent += "\t";
                        }
                    }

                    lines.Insert(cursorRow + 1, indent + remaining);
                    cursorRow++;
                    cursorCol = indent.Length;
                    isDirty = true;
                    break;
                case ConsoleKey.Tab:
                    if (selStartRow.HasValue) DeleteSelection();
                    lines[cursorRow] = lines[cursorRow].Insert(cursorCol, "\t");
                    cursorCol += 1;
                    isDirty = true;
                    break;
                case ConsoleKey.UpArrow:
                    if (cursorRow > 0) cursorRow--;
                    cursorCol = Math.Min(cursorCol, lines[cursorRow].Length);
                    break;
                case ConsoleKey.DownArrow:
                    if (cursorRow < lines.Count - 1) cursorRow++;
                    cursorCol = Math.Min(cursorCol, lines[cursorRow].Length);
                    break;
                case ConsoleKey.LeftArrow:
                    if (cursorCol > 0) cursorCol--;
                    else if (cursorRow > 0) { cursorRow--; cursorCol = lines[cursorRow].Length; }
                    break;
                case ConsoleKey.RightArrow:
                    if (cursorCol < lines[cursorRow].Length) cursorCol++;
                    else if (cursorRow < lines.Count - 1) { cursorRow++; cursorCol = 0; }
                    break;
                case ConsoleKey.Home: cursorCol = 0; break;
                case ConsoleKey.End: cursorCol = lines[cursorRow].Length; break;
                default:
                    if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
                    {
                        if (selStartRow.HasValue) DeleteSelection();
                        lines[cursorRow] = lines[cursorRow].Insert(cursorCol, key.KeyChar.ToString());
                        cursorCol++;
                        isDirty = true;
                    }
                    break;
            }
        }

        private void Cut()
        {
            Copy();
            DeleteSelection();
        }

        private void DeleteSelection()
        {
            if (!selStartRow.HasValue || !selStartCol.HasValue) return;

            var (startR, startC, endR, endC) = GetSelectionRange();
            
            if (startR == endR)
            {
                lines[startR] = lines[startR].Remove(startC, endC - startC);
            }
            else
            {
                var firstLinePart = lines[startR].Substring(0, startC);
                var lastLinePart = lines[endR].Substring(endC);
                lines[startR] = firstLinePart + lastLinePart;
                
                for (int i = 0; i < endR - startR; i++)
                {
                    lines.RemoveAt(startR + 1);
                }
            }

            cursorRow = startR;
            cursorCol = startC;
            selStartRow = null;
            selStartCol = null;
            isDirty = true;
        }

        private void Save()
        {
            if (filePath == null)
            {
                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                Console.Write("Save as: ");
                filePath = Console.ReadLine();
                Console.Clear();
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                File.WriteAllLines(filePath, lines);
                isDirty = false;
            }
        }

        private void Copy()
        {
            if (selStartRow.HasValue)
            {
                var (startR, startC, endR, endC) = GetSelectionRange();
                var sb = new StringBuilder();
                if (startR == endR)
                {
                    sb.Append(lines[startR].Substring(startC, endC - startC));
                }
                else
                {
                    sb.AppendLine(lines[startR].Substring(startC));
                    for (int i = startR + 1; i < endR; i++)
                    {
                        sb.AppendLine(lines[i]);
                    }
                    sb.Append(lines[endR].Substring(0, endC));
                }
                ClipboardService.SetText(sb.ToString());
            }
            else
            {
                ClipboardService.SetText(lines[cursorRow]);
            }
        }

        private void Paste()
        {
            if (selStartRow.HasValue) DeleteSelection();

            var text = ClipboardService.GetText();
            if (string.IsNullOrEmpty(text)) return;

            var pasteLines = text.Replace("\r\n", "\n").Split('\n');
            if (pasteLines.Length == 1)
            {
                lines[cursorRow] = lines[cursorRow].Insert(cursorCol, pasteLines[0]);
                cursorCol += pasteLines[0].Length;
            }
            else
            {
                var currentLineSuffix = lines[cursorRow].Substring(cursorCol);
                lines[cursorRow] = lines[cursorRow].Substring(0, cursorCol) + pasteLines[0];
                for (int i = 1; i < pasteLines.Length - 1; i++)
                {
                    lines.Insert(cursorRow + i, pasteLines[i]);
                }
                lines.Insert(cursorRow + pasteLines.Length - 1, pasteLines.Last() + currentLineSuffix);
                cursorRow += pasteLines.Length - 1;
                cursorCol = pasteLines.Last().Length;
            }
            isDirty = true;
        }
    }
}