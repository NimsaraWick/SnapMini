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
        private static readonly SolidColorBrush TextBrush = new((Color)ColorConverter.ConvertFromString("#F8FAFC"));
        private static readonly SolidColorBrush BoldBrush = new((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        private static readonly SolidColorBrush MutedBrush = new((Color)ColorConverter.ConvertFromString("#94A3B8"));
        private static readonly SolidColorBrush AccentBrush = new((Color)ColorConverter.ConvertFromString("#818CF8"));
        private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#10B981"));
        private static readonly SolidColorBrush UserTagBrush = new((Color)ColorConverter.ConvertFromString("#38BDF8"));
        private static readonly SolidColorBrush CodeBgBrush = new((Color)ColorConverter.ConvertFromString("#0B1120"));
        private static readonly SolidColorBrush CodeBorderBrush = new((Color)ColorConverter.ConvertFromString("#334155"));
        private static readonly SolidColorBrush CodeTextBrush = new((Color)ColorConverter.ConvertFromString("#38BDF8"));
        private static readonly SolidColorBrush InlineCodeBgBrush = new((Color)ColorConverter.ConvertFromString("#1E293B"));
        private static readonly SolidColorBrush QuoteBorderBrush = new((Color)ColorConverter.ConvertFromString("#6366F1"));
        private static readonly SolidColorBrush UserBubbleBgBrush = new((Color)ColorConverter.ConvertFromString("#1E293B"));

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
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8")),
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
                    p.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A5B4FC"));
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
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
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
                SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1")),
                SelectionOpacity = 0.5,
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
