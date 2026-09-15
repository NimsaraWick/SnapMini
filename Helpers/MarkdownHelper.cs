using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SnapMini.Helpers
{
    /// <summary>
    /// Converts Markdown text into a rich, formatted WPF FlowDocument
    /// supporting Headings, Bold, Italic, Inline Code, Code Blocks, Bullet/Numbered Lists,
    /// Blockquotes, Dividers, Hyperlinks, and multi-turn chat continuation with dark SaaS theme styling.
    /// </summary>
    public static class MarkdownHelper
    {
        private static readonly SolidColorBrush TextBrush = new((Color)ColorConverter.ConvertFromString("#F5F3FF"));
        private static readonly SolidColorBrush BoldBrush = new((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        private static readonly SolidColorBrush MutedBrush = new((Color)ColorConverter.ConvertFromString("#C4B5FD"));
        private static readonly SolidColorBrush AccentBrush = new((Color)ColorConverter.ConvertFromString("#C084FC"));
        private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#E9D5FF"));
        private static readonly SolidColorBrush UserTagBrush = new((Color)ColorConverter.ConvertFromString("#C084FC"));
        private static readonly SolidColorBrush CodeBgBrush = new((Color)ColorConverter.ConvertFromString("#040407"));
        private static readonly SolidColorBrush CodeBorderBrush = new((Color)ColorConverter.ConvertFromString("#2E1848"));
        private static readonly SolidColorBrush CodeTextBrush = new((Color)ColorConverter.ConvertFromString("#D8B4FE"));
        private static readonly SolidColorBrush InlineCodeBgBrush = new((Color)ColorConverter.ConvertFromString("#140A26"));
        private static readonly SolidColorBrush QuoteBorderBrush = new((Color)ColorConverter.ConvertFromString("#A855F7"));
        private static readonly SolidColorBrush UserBubbleBgBrush = new((Color)ColorConverter.ConvertFromString("#08070D"));
        private static readonly SolidColorBrush TableBgBrush = new((Color)ColorConverter.ConvertFromString("#000000"));
        private static readonly SolidColorBrush TableHeaderBgBrush = new((Color)ColorConverter.ConvertFromString("#120924"));
        private static readonly SolidColorBrush TableHeaderFgBrush = new((Color)ColorConverter.ConvertFromString("#E9D5FF"));
        private static readonly SolidColorBrush TableBorderBrush = new((Color)ColorConverter.ConvertFromString("#2E1848"));
        private static readonly SolidColorBrush TableRowAltBgBrush = new((Color)ColorConverter.ConvertFromString("#090712"));
        private static readonly SolidColorBrush TableCellBorderBrush = new((Color)ColorConverter.ConvertFromString("#1E1133"));

        // Regex for Markdown table separator lines (e.g. |---|---| or |:---|:---:|---:|)
        private static readonly Regex TableSeparatorRegex = new(
            @"^\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)+\|?$",
            RegexOptions.Compiled);

        // Regex for inline Markdown elements: ***bold-italic***, **bold**, *italic*, `code`, [link](url)
        private static readonly Regex InlineRegex = new(
            @"(?:\*\*\*(?<bi>[^*]+)\*\*\*)|" +
            @"(?:\*\*(?<b>[^*]+)\*\*)|" +
            @"(?:__(?<b>[^_]+)__)|" +
            @"(?:\*(?<i>[^*]+)\*)|" +
            @"(?:_(?<i>[^_]+)_)|" +
            @"(?:`(?<c>[^`]+)`)|" +
            @"(?:\[(?<lt>[^\]]+)\]\((?<lu>[^)]+)\))",
            RegexOptions.Compiled);

        public static FlowDocument ToFlowDocument(string markdown)
        {
            var doc = new FlowDocument
            {
                Foreground = TextBrush,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                PagePadding = new Thickness(0),
                LineHeight = 22
            };

            var blocks = ParseBlocks(markdown);
            foreach (var block in blocks)
            {
                doc.Blocks.Add(block);
            }

            return doc;
        }

        public static void AppendUserMessage(FlowDocument doc, string userMessage)
        {
            doc.Blocks.Add(CreateHorizontalRule());

            var container = new Border
            {
                Background = UserBubbleBgBrush,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8D3BF0")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 8, 0, 8)
            };

            var sp = new StackPanel();
            var header = new TextBlock
            {
                Text = "👤 You",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = UserTagBrush,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var body = new TextBlock
            {
                Text = userMessage,
                Foreground = TextBrush,
                FontSize = 13.5,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            };

            sp.Children.Add(header);
            sp.Children.Add(body);
            container.Child = sp;

            doc.Blocks.Add(new BlockUIContainer(container));
        }

        public static Block AppendThinkingIndicator(FlowDocument doc)
        {
            var p = new Paragraph
            {
                Margin = new Thickness(0, 8, 0, 8),
                FontStyle = FontStyles.Italic
            };
            p.Inlines.Add(new Run("✨ Thinking...") { Foreground = AccentBrush });
            doc.Blocks.Add(p);
            return p;
        }

        public static void AppendAiResponse(FlowDocument doc, string aiMarkdown)
        {
            var headerP = new Paragraph
            {
                Margin = new Thickness(0, 8, 0, 4)
            };
            headerP.Inlines.Add(new Run("✨ SnapMini AI")
            {
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = GreenBrush
            });
            doc.Blocks.Add(headerP);

            var blocks = ParseBlocks(aiMarkdown);
            foreach (var b in blocks)
            {
                doc.Blocks.Add(b);
            }
        }

        public static void RemoveBlock(FlowDocument doc, Block block)
        {
            if (doc.Blocks.Contains(block))
            {
                doc.Blocks.Remove(block);
            }
        }

        public static List<Block> ParseBlocks(string markdown)
        {
            var list = new List<Block>();
            if (string.IsNullOrWhiteSpace(markdown))
                return list;

            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            bool inCodeBlock = false;
            var codeBlockLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string trimmedLine = rawLine.Trim();

                // Code block toggle: ```
                if (trimmedLine.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        list.Add(CreateCodeBlock(string.Join("\n", codeBlockLines)));
                        codeBlockLines.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        inCodeBlock = true;
                        codeBlockLines.Clear();
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockLines.Add(rawLine);
                    continue;
                }

                // Empty line
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                // Horizontal Rule
                if (trimmedLine == "---" || trimmedLine == "***" || trimmedLine == "___")
                {
                    list.Add(CreateHorizontalRule());
                    continue;
                }

                // Headings (#, ##, ###)
                if (trimmedLine.StartsWith("#"))
                {
                    int level = 0;
                    while (level < trimmedLine.Length && trimmedLine[level] == '#') level++;
                    if (level <= 6 && trimmedLine.Length > level && trimmedLine[level] == ' ')
                    {
                        string headingText = trimmedLine[(level + 1)..].Trim();
                        list.Add(CreateHeading(headingText, level));
                        continue;
                    }
                }

                // Bullet Lists: *, -, +, •
                if (IsBulletLine(trimmedLine, out string bulletContent))
                {
                    list.Add(CreateBulletItem(bulletContent));
                    continue;
                }

                // Numbered Lists: "1. ", "2. ", etc.
                if (IsNumberedLine(trimmedLine, out string numPrefix, out string numContent))
                {
                    list.Add(CreateNumberedItem(numPrefix, numContent));
                    continue;
                }

                // Blockquote: >
                if (trimmedLine.StartsWith(">"))
                {
                    string quoteText = trimmedLine.TrimStart('>', ' ').Trim();
                    list.Add(CreateBlockQuote(quoteText));
                    continue;
                }

                // Markdown Table (Header line + Separator line)
                if (i + 1 < lines.Length && IsTableLine(trimmedLine) && IsTableSeparator(lines[i + 1].Trim()))
                {
                    var headerCells = SplitTableRow(trimmedLine);
                    var separatorCells = SplitTableRow(lines[i + 1].Trim());

                    int colCount = Math.Max(headerCells.Count, separatorCells.Count);
                    var alignments = new List<TextAlignment>();
                    for (int c = 0; c < colCount; c++)
                    {
                        if (c < separatorCells.Count)
                            alignments.Add(ParseAlignment(separatorCells[c]));
                        else
                            alignments.Add(TextAlignment.Left);
                    }

                    var dataRows = new List<List<string>>();
                    int j = i + 2;
                    while (j < lines.Length)
                    {
                        string rawNext = lines[j];
                        string trimmedNext = rawNext.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedNext))
                            break;
                        if (trimmedNext.StartsWith("```") || trimmedNext.StartsWith("#") || trimmedNext == "---" || trimmedNext == "***" || trimmedNext == "___")
                            break;
                        if (!trimmedNext.Contains('|'))
                            break;

                        dataRows.Add(SplitTableRow(trimmedNext));
                        j++;
                    }

                    list.Add(CreateTableBlock(headerCells, alignments, dataRows));
                    i = j - 1;
                    continue;
                }

                // Regular Paragraph
                var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8), LineHeight = 22 };
                AddFormattedInlines(paragraph.Inlines, trimmedLine);
                list.Add(paragraph);
            }

            // Flush unclosed code block if needed
            if (inCodeBlock && codeBlockLines.Count > 0)
            {
                list.Add(CreateCodeBlock(string.Join("\n", codeBlockLines)));
            }

            return list;
        }

        private static bool IsBulletLine(string line, out string content)
        {
            content = "";
            if (line.StartsWith("* ") || line.StartsWith("- ") || line.StartsWith("+ ") || line.StartsWith("• "))
            {
                content = line[2..].Trim();
                return true;
            }
            return false;
        }

        private static bool IsNumberedLine(string line, out string prefix, out string content)
        {
            prefix = "";
            content = "";
            var match = Regex.Match(line, @"^(\d+\.)\s+(.*)$");
            if (match.Success)
            {
                prefix = match.Groups[1].Value;
                content = match.Groups[2].Value;
                return true;
            }
            return false;
        }

        private static Block CreateHeading(string text, int level)
        {
            var p = new Paragraph { LineHeight = 26 };
            switch (level)
            {
                case 1:
                    p.FontSize = 18;
                    p.FontWeight = FontWeights.Bold;
                    p.Foreground = AccentBrush;
                    p.Margin = new Thickness(0, 10, 0, 6);
                    break;
                case 2:
                    p.FontSize = 16;
                    p.FontWeight = FontWeights.Bold;
                    p.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C4B5FD"));
                    p.Margin = new Thickness(0, 8, 0, 5);
                    break;
                case 3:
                default:
                    p.FontSize = 15;
                    p.FontWeight = FontWeights.SemiBold;
                    p.Foreground = BoldBrush;
                    p.Margin = new Thickness(0, 6, 0, 4);
                    break;
            }
            AddFormattedInlines(p.Inlines, text);
            return p;
        }

        private static Block CreateBulletItem(string content)
        {
            var p = new Paragraph
            {
                Margin = new Thickness(12, 0, 0, 5),
                LineHeight = 22
            };

            p.Inlines.Add(new Run("•  ")
            {
                Foreground = AccentBrush,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            });

            AddFormattedInlines(p.Inlines, content);
            return p;
        }

        private static Block CreateNumberedItem(string prefix, string content)
        {
            var p = new Paragraph
            {
                Margin = new Thickness(12, 0, 0, 5),
                LineHeight = 22
            };

            p.Inlines.Add(new Run($"{prefix} ")
            {
                Foreground = AccentBrush,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            });

            AddFormattedInlines(p.Inlines, content);
            return p;
        }

        private static Block CreateBlockQuote(string quoteText)
        {
            var border = new Border
            {
                BorderBrush = QuoteBorderBrush,
                BorderThickness = new Thickness(3, 0, 0, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#13121C")),
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(0, 6, 6, 0),
                Margin = new Thickness(0, 4, 0, 8)
            };

            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = MutedBrush,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                LineHeight = 20
            };
            tb.Text = quoteText;
            border.Child = tb;

            return new BlockUIContainer(border);
        }

        private static Block CreateCodeBlock(string code)
        {
            var border = new Border
            {
                Background = CodeBgBrush,
                BorderBrush = CodeBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 4, 0, 8)
            };

            var tb = new TextBox
            {
                Text = code,
                IsReadOnly = true,
                Background = Brushes.Transparent,
                Foreground = CodeTextBrush,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New"),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8D3BF0")),
                SelectionOpacity = 0.4,
                Cursor = System.Windows.Input.Cursors.IBeam
            };

            border.Child = tb;
            return new BlockUIContainer(border);
        }

        private static Block CreateHorizontalRule()
        {
            var border = new Border
            {
                Height = 1,
                Background = CodeBorderBrush,
                Margin = new Thickness(0, 8, 0, 8)
            };
            return new BlockUIContainer(border);
        }

        private static bool IsTableLine(string line)
        {
            return !string.IsNullOrWhiteSpace(line) && line.Contains('|');
        }

        private static bool IsTableSeparator(string line)
        {
            return TableSeparatorRegex.IsMatch(line);
        }

        private static List<string> SplitTableRow(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("|"))
                trimmed = trimmed[1..];
            if (trimmed.EndsWith("|"))
                trimmed = trimmed[..^1];

            // Protect escaped pipes
            string placeholder = "\u0000";
            trimmed = trimmed.Replace(@"\|", placeholder);

            var rawCells = trimmed.Split('|');
            var cells = new List<string>(rawCells.Length);
            foreach (var c in rawCells)
            {
                cells.Add(c.Replace(placeholder, "|").Trim());
            }
            return cells;
        }

        private static TextAlignment ParseAlignment(string sep)
        {
            sep = sep.Trim();
            bool leftColon = sep.StartsWith(":");
            bool rightColon = sep.EndsWith(":");
            if (leftColon && rightColon) return TextAlignment.Center;
            if (rightColon) return TextAlignment.Right;
            return TextAlignment.Left;
        }

        private static Block CreateTableBlock(List<string> headers, List<TextAlignment> alignments, List<List<string>> rows)
        {
            int colCount = headers.Count;
            foreach (var r in rows)
            {
                if (r.Count > colCount)
                    colCount = r.Count;
            }

            if (colCount == 0)
                return new Paragraph();

            while (headers.Count < colCount) headers.Add("");
            while (alignments.Count < colCount) alignments.Add(TextAlignment.Left);

            int totalRows = rows.Count + 1; // 1 for header + data rows

            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            for (int c = 0; c < colCount; c++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (int r = 0; r < totalRows; r++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            // Header cells
            for (int c = 0; c < colCount; c++)
            {
                double topLeft = (c == 0) ? 7 : 0;
                double topRight = (c == colCount - 1) ? 7 : 0;
                double bottomLeft = (rows.Count == 0 && c == 0) ? 7 : 0;
                double bottomRight = (rows.Count == 0 && c == colCount - 1) ? 7 : 0;

                var border = new Border
                {
                    Background = TableHeaderBgBrush,
                    BorderBrush = TableBorderBrush,
                    BorderThickness = new Thickness(0, 0, (c < colCount - 1 ? 1 : 0), (rows.Count > 0 ? 1 : 0)),
                    Padding = new Thickness(14, 10, 14, 10),
                    CornerRadius = new CornerRadius(topLeft, topRight, bottomRight, bottomLeft)
                };

                var tb = new TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Foreground = TableHeaderFgBrush,
                    TextAlignment = alignments[c],
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18
                };
                AddFormattedInlines(tb.Inlines, headers[c]);
                border.Child = tb;

                Grid.SetRow(border, 0);
                Grid.SetColumn(border, c);
                grid.Children.Add(border);
            }

            // Data rows
            for (int r = 0; r < rows.Count; r++)
            {
                var rowData = rows[r];
                bool isAlt = r % 2 == 1;
                bool isLastRow = r == rows.Count - 1;

                for (int c = 0; c < colCount; c++)
                {
                    string cellText = c < rowData.Count ? rowData[c] : "";
                    double bottomLeft = (isLastRow && c == 0) ? 7 : 0;
                    double bottomRight = (isLastRow && c == colCount - 1) ? 7 : 0;

                    var border = new Border
                    {
                        Background = isAlt ? TableRowAltBgBrush : TableBgBrush,
                        BorderBrush = TableCellBorderBrush,
                        BorderThickness = new Thickness(0, 0, (c < colCount - 1 ? 1 : 0), (isLastRow ? 0 : 1)),
                        Padding = new Thickness(14, 9, 14, 9),
                        CornerRadius = new CornerRadius(0, 0, bottomRight, bottomLeft)
                    };

                    var tb = new TextBlock
                    {
                        FontSize = 13,
                        Foreground = TextBrush,
                        TextAlignment = alignments[c],
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 20
                    };
                    AddFormattedInlines(tb.Inlines, cellText);
                    border.Child = tb;

                    Grid.SetRow(border, r + 1);
                    Grid.SetColumn(border, c);
                    grid.Children.Add(border);
                }
            }

            var outerBorder = new Border
            {
                Background = TableBgBrush,
                BorderBrush = TableBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 8, 0, 12),
                Child = grid
            };

            return new BlockUIContainer(outerBorder);
        }

        private static void AddFormattedInlines(InlineCollection inlines, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            int lastIndex = 0;
            var matches = InlineRegex.Matches(text);

            foreach (Match match in matches)
            {
                // Plain text before match
                if (match.Index > lastIndex)
                {
                    string plain = text[lastIndex..match.Index];
                    inlines.Add(new Run(plain) { Foreground = TextBrush });
                }

                if (match.Groups["bi"].Success)
                {
                    inlines.Add(new Bold(new Italic(new Run(match.Groups["bi"].Value)))
                    {
                        Foreground = BoldBrush
                    });
                }
                else if (match.Groups["b"].Success)
                {
                    inlines.Add(new Bold(new Run(match.Groups["b"].Value))
                    {
                        Foreground = BoldBrush,
                        FontWeight = FontWeights.SemiBold
                    });
                }
                else if (match.Groups["i"].Success)
                {
                    inlines.Add(new Italic(new Run(match.Groups["i"].Value))
                    {
                        Foreground = TextBrush
                    });
                }
                else if (match.Groups["c"].Success)
                {
                    var codeRun = new Run($" {match.Groups["c"].Value} ")
                    {
                        FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New"),
                        Foreground = CodeTextBrush,
                        Background = InlineCodeBgBrush,
                        FontSize = 13,
                        FontWeight = FontWeights.Medium
                    };
                    inlines.Add(codeRun);
                }
                else if (match.Groups["lt"].Success && match.Groups["lu"].Success)
                {
                    string linkText = match.Groups["lt"].Value;
                    string linkUrl = match.Groups["lu"].Value;
                    try
                    {
                        var link = new Hyperlink(new Run(linkText))
                        {
                            NavigateUri = new Uri(linkUrl),
                            Foreground = AccentBrush,
                            TextDecorations = TextDecorations.Underline,
                            Cursor = System.Windows.Input.Cursors.Hand
                        };
                        link.RequestNavigate += (s, e) =>
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                            }
                            catch { }
                            e.Handled = true;
                        };
                        inlines.Add(link);
                    }
                    catch
                    {
                        inlines.Add(new Run(linkText) { Foreground = AccentBrush });
                    }
                }

                lastIndex = match.Index + match.Length;
            }

            // Trailing text
            if (lastIndex < text.Length)
            {
                inlines.Add(new Run(text[lastIndex..]) { Foreground = TextBrush });
            }
        }
    }
}
