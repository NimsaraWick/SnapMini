using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;

namespace SnapMini.Services
{
    /// <summary>
    /// Export service for Google Docs & Google Drive.
    ///
    /// Supports:
    /// - Question / Answer formatting
    /// - Purple important-text highlighting using **text**
    /// - Compact fenced code blocks using ```code```
    /// - Markdown tables converted to real Google Docs tables
    /// - Native bullet lists
    /// - Existing Google Document append
    /// - New Google Document creation
    /// - Google Drive folder placement
    /// </summary>
    public static class GoogleDocsService
    {
        // ============================================================
        // COLORS
        // ============================================================

        private static readonly RgbColor DividerColor =
            new RgbColor
            {
                Red = 0.851f,
                Green = 0.859f,
                Blue = 0.878f
            };

        private static readonly RgbColor DateColor =
            new RgbColor
            {
                Red = 0.612f,
                Green = 0.639f,
                Blue = 0.686f
            };

        private static readonly RgbColor QuestionColor =
            new RgbColor
            {
                Red = 0.357f,
                Green = 0.608f,
                Blue = 0.835f
            };

        private static readonly RgbColor AnswerColor =
            new RgbColor
            {
                Red = 0.122f,
                Green = 0.161f,
                Blue = 0.216f
            };

        private static readonly RgbColor HighlightColor =
            new RgbColor
            {
                Red = 0.576f,
                Green = 0.200f,
                Blue = 0.918f
            };

        private static readonly RgbColor CodeTextColor =
            new RgbColor
            {
                Red = 0.184f,
                Green = 0.204f,
                Blue = 0.251f
            };

        private static readonly RgbColor CodeBoxBorder =
            new RgbColor
            {
                Red = 0.827f,
                Green = 0.835f,
                Blue = 0.855f
            };

        private static readonly RgbColor TableBorderColor =
            new RgbColor
            {
                Red = 0.820f,
                Green = 0.830f,
                Blue = 0.850f
            };

        private static readonly RgbColor TableHeaderColor =
            new RgbColor
            {
                Red = 0.930f,
                Green = 0.945f,
                Blue = 0.965f
            };

        private static readonly RgbColor TableTextColor =
            new RgbColor
            {
                Red = 0.122f,
                Green = 0.161f,
                Blue = 0.216f
            };

        private static readonly RgbColor LabelColor =
            new RgbColor
            {
                Red = 0.522f,
                Green = 0.545f,
                Blue = 0.573f
            };

        private static readonly OptionalColor CodeBoxFill =
            new OptionalColor
            {
                Color = new Color
                {
                    RgbColor = new RgbColor
                    {
                        Red = 0.965f,
                        Green = 0.969f,
                        Blue = 0.976f
                    }
                }
            };

        // ============================================================
        // FONTS / SIZES
        // ============================================================

        private const string BodyFont = "Arial";

        private const string CodeFont = "Consolas";

        private const double BodyFontSize = 11;

        private const double QuestionFontSize = 12;

        private const double DateFontSize = 7.5;

        private const double LabelFontSize = 8.5;

        private const double CodeFontSize = 9;

        private const double TableFontSize = 9.5;

        // Public write access is disabled by default.
        private const bool AllowPublicWriteAccess = false;

        // ============================================================
        // PUBLIC ENTRY POINT
        // ============================================================

        public static async Task<string> ExportQaSummaryAsync(
            string question,
            string answer,
            AIService.GoogleExportTarget? target = null)
        {
            var settings = AIService.ReadSettings();

            string rawFolder =
                (settings.GoogleDriveFolderId ?? "").Trim();

            string customLink =
                (settings.GoogleDocsCustomLink ?? "").Trim();

            string credentialsJson =
                (settings.GoogleCredentialsJson ?? "").Trim();

            // --------------------------------------------------------
            // 1. AI SMOOTHING
            // --------------------------------------------------------

            string smoothedContent = "";

            try
            {
                string smoothingPrompt =
                    "Refine and smooth the following Question and AI Answer into a concise, " +
                    "high-level summary containing ONLY the core key points and essential takeaways. " +
                    "Omit unnecessary filler or repetition. " +
                    "Wrap the few most important terms or values in **double asterisks** sparingly. " +
                    "Keep any code exactly as given, inside a ```fenced code block```. " +
                    "Keep Markdown tables as tables.\n\n" +
                    $"Question:\n{question}\n\n" +
                    $"Answer:\n{answer}";

                smoothedContent =
                    await AIService.GetAnswerAsync(smoothingPrompt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"AI Smoothing notice: {ex.Message}");
            }

            string rawAnswerBody =
                !string.IsNullOrWhiteSpace(smoothedContent)
                    ? smoothedContent.Trim()
                    : answer.Trim();

            string finalAnswerBody =
                NormalizeMarkdown(rawAnswerBody);

            string cleanedQuestion =
                NormalizeMarkdown(question.Trim());

            string timestamp =
                DateTime.Now.ToString("g");

            // --------------------------------------------------------
            // 2. FIND DOCUMENT ID OR DRIVE FOLDER ID
            // --------------------------------------------------------

            string existingDocId = "";
            string folderId = "";

            if (target != null)
            {
                string urlOrId = (target.UrlOrId ?? "").Trim();
                bool isFolderType = string.Equals(target.Type, "Folder", StringComparison.OrdinalIgnoreCase);
                bool isDocType = string.Equals(target.Type, "Doc", StringComparison.OrdinalIgnoreCase);

                var docRegex = Regex.Match(urlOrId, @"/document/d/([a-zA-Z0-9_-]+)");
                var folderRegex = Regex.Match(urlOrId, @"/folders/([a-zA-Z0-9_-]+)");

                if (docRegex.Success)
                {
                    existingDocId = docRegex.Groups[1].Value;
                    customLink = urlOrId;
                    rawFolder = "";
                }
                else if (folderRegex.Success)
                {
                    folderId = folderRegex.Groups[1].Value;
                    rawFolder = urlOrId;
                    customLink = "";
                }
                else if (isDocType)
                {
                    if (!urlOrId.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !urlOrId.Contains("/"))
                    {
                        existingDocId = urlOrId;
                    }
                    customLink = urlOrId;
                    rawFolder = "";
                }
                else if (isFolderType)
                {
                    if (!urlOrId.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !urlOrId.Contains("/"))
                    {
                        folderId = urlOrId;
                    }
                    rawFolder = urlOrId;
                    customLink = "";
                }
                else
                {
                    if (urlOrId.Contains("drive.google.com") || urlOrId.Contains("folders"))
                    {
                        rawFolder = urlOrId;
                    }
                    else
                    {
                        customLink = urlOrId;
                    }
                }
            }
            else
            {
                var docMatch =
                    Regex.Match(
                        customLink,
                        @"/document/d/([a-zA-Z0-9_-]+)");

                if (!docMatch.Success)
                {
                    docMatch =
                        Regex.Match(
                            rawFolder,
                            @"/document/d/([a-zA-Z0-9_-]+)");
                }

                if (docMatch.Success)
                {
                    existingDocId =
                        docMatch.Groups[1].Value;
                }

                var folderMatch =
                    Regex.Match(
                        rawFolder,
                        @"/folders/([a-zA-Z0-9_-]+)");

                if (folderMatch.Success)
                {
                    folderId =
                        folderMatch.Groups[1].Value;
                }
                else if (
                    !rawFolder.StartsWith(
                        "http",
                        StringComparison.OrdinalIgnoreCase) &&
                    !rawFolder.Contains("/"))
                {
                    folderId = rawFolder;
                }
            }

            // --------------------------------------------------------
            // 4. FIND CREDENTIALS
            // --------------------------------------------------------

            string[] possibleCredPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google_credentials.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "google_credentials.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..", "google_credentials.json"),
                @"c:\Users\NwicK\Desktop\SnapMini\google_credentials.json"
            };

            string defaultJsonPath = "";
            foreach (var candidate in possibleCredPaths)
            {
                if (File.Exists(candidate))
                {
                    defaultJsonPath = Path.GetFullPath(candidate);
                    break;
                }
            }

            bool hasFileCreds =
                !string.IsNullOrEmpty(defaultJsonPath);

            bool hasStringCreds =
                !string.IsNullOrWhiteSpace(credentialsJson);

            // --------------------------------------------------------
            // 5. GOOGLE AUTHENTICATION
            // --------------------------------------------------------

            if (hasFileCreds || hasStringCreds)
            {
                try
                {
#pragma warning disable CS0618

                    GoogleCredential credential;

                    if (hasStringCreds)
                    {
                        credential =
                            GoogleCredential
                                .FromJson(credentialsJson)
                                .CreateScoped(new[]
                                {
                                    DocsService.Scope.Documents,
                                    Google.Apis.Drive.v3.DriveService.Scope.Drive
                                });
                    }
                    else
                    {
                        using var stream =
                            new FileStream(
                                defaultJsonPath,
                                FileMode.Open,
                                FileAccess.Read);

                        credential =
                            GoogleCredential
                                .FromStream(stream)
                                .CreateScoped(new[]
                                {
                                    DocsService.Scope.Documents,
                                    Google.Apis.Drive.v3.DriveService.Scope.Drive
                                });
                    }

#pragma warning restore CS0618

                    var docsService =
                        new DocsService(
                            new BaseClientService.Initializer
                            {
                                HttpClientInitializer = credential,
                                ApplicationName = "SnapMini"
                            });

                    var driveService =
                        new Google.Apis.Drive.v3.DriveService(
                            new BaseClientService.Initializer
                            {
                                HttpClientInitializer = credential,
                                ApplicationName = "SnapMini"
                            });

                    // ------------------------------------------------
                    // CASE 1:
                    // EXISTING GOOGLE DOCUMENT
                    // ------------------------------------------------

                    if (!string.IsNullOrWhiteSpace(existingDocId))
                    {
                        try
                        {
                            await WriteRichQaToDocAsync(
                                docsService,
                                existingDocId,
                                cleanedQuestion,
                                finalAnswerBody,
                                timestamp);

                            string docUrl =
                                $"https://docs.google.com/document/d/{existingDocId}/edit";

                            return docUrl;
                        }
                        catch (Exception ex)
                            when (
                                ex.Message.Contains("Forbidden") ||
                                ex.Message.Contains("403"))
                        {
                            MessageBox.Show(
                                "To save directly into this existing Google Document, " +
                                "please share it with your Service Account email.\n\n" +
                                "1. Open the document in Google Drive.\n" +
                                "2. Click Share.\n" +
                                "3. Add your Service Account email.\n" +
                                "4. Give it Editor permission.\n\n" +
                                "Falling back to Clipboard copy.",
                                "Google Document Permission Needed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }

                    // ------------------------------------------------
                    // CASE 2:
                    // CREATE NEW DOCUMENT
                    // ------------------------------------------------

                    string title =
                        $"SnapMini Q&A - {DateTime.Now:yyyy-MM-dd HH:mm}";

                    var newDoc =
                        new Document
                        {
                            Title = title
                        };

                    var createdDoc =
                        await WithRetryAsync(
                            () =>
                                docsService
                                    .Documents
                                    .Create(newDoc)
                                    .ExecuteAsync());

                    string docId =
                        createdDoc.DocumentId;

                    // Write content
                    await WriteRichQaToDocAsync(
                        docsService,
                        docId,
                        cleanedQuestion,
                        finalAnswerBody,
                        timestamp);

                    // ------------------------------------------------
                    // PERMISSION
                    // ------------------------------------------------

                    try
                    {
                        var permission =
                            new Google.Apis.Drive.v3.Data.Permission
                            {
                                Type = "anyone",
                                Role =
                                    AllowPublicWriteAccess
                                        ? "writer"
                                        : "reader"
                            };

                        await WithRetryAsync(
                            () =>
                                driveService
                                    .Permissions
                                    .Create(permission, docId)
                                    .ExecuteAsync());
                    }
                    catch (Exception permEx)
                    {
                        Debug.WriteLine(
                            $"Permission grant warning: {permEx.Message}");
                    }

                    // ------------------------------------------------
                    // MOVE INTO FOLDER
                    // ------------------------------------------------

                    if (!string.IsNullOrWhiteSpace(folderId))
                    {
                        try
                        {
                            var getReq =
                                driveService.Files.Get(docId);

                            getReq.Fields = "parents";

                            var file =
                                await WithRetryAsync(
                                    () => getReq.ExecuteAsync());

                            string previousParents =
                                string.Join(
                                    ",",
                                    file.Parents ??
                                    new List<string>());

                            var updateReq =
                                driveService.Files.Update(
                                    new Google.Apis.Drive.v3.Data.File(),
                                    docId);

                            updateReq.AddParents =
                                folderId;

                            updateReq.RemoveParents =
                                previousParents;

                            await WithRetryAsync(
                                () => updateReq.ExecuteAsync());
                        }
                        catch (Exception moveEx)
                        {
                            Debug.WriteLine(
                                $"Move to folder warning: {moveEx.Message}");
                        }
                    }

                    return
                        $"https://docs.google.com/document/d/{docId}/edit";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"Google API Error: {ex.Message}");

                    MessageBox.Show(
                        $"Google Docs API Notice:\n{ex.Message}\n\n" +
                        "Summary text has been copied to your clipboard.",
                        "SnapMini Google Export",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            // ========================================================
            // FALLBACK
            // ========================================================

            string plainQuestion =
                StripMarkdown(cleanedQuestion);

            string plainAnswer =
                StripMarkdown(finalAnswerBody);

            string plainTextCopy =
                "──────────────────────────────────────────────────────────\n" +
                $"{timestamp}\n\n" +
                $"{plainQuestion}\n\n" +
                $"{plainAnswer}\n\n";

            Clipboard.SetText(plainTextCopy);

            string targetUrl =
                !string.IsNullOrWhiteSpace(customLink)
                    ? (customLink.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? customLink
                        : (!string.IsNullOrWhiteSpace(existingDocId)
                            ? $"https://docs.google.com/document/d/{existingDocId}/edit"
                            : customLink))
                    : (
                        !string.IsNullOrWhiteSpace(rawFolder)
                            ? (rawFolder.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                ? rawFolder
                                : (!string.IsNullOrWhiteSpace(folderId)
                                    ? $"https://drive.google.com/drive/folders/{folderId}"
                                    : rawFolder))
                            : (!string.IsNullOrWhiteSpace(existingDocId)
                                ? $"https://docs.google.com/document/d/{existingDocId}/edit"
                                : (!string.IsNullOrWhiteSpace(folderId)
                                    ? $"https://drive.google.com/drive/folders/{folderId}"
                                    : "https://docs.google.com/document/create"))
                    );

            if (!targetUrl.StartsWith(
                    "http",
                    StringComparison.OrdinalIgnoreCase))
            {
                targetUrl =
                    $"https://drive.google.com/drive/folders/{targetUrl}";
            }

            // Do not open browser window; keep summary on clipboard and return target URL
            return targetUrl;
        }

        // ============================================================
        // MARKDOWN NORMALIZATION
        // ============================================================

        private static string NormalizeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text =
                text.Replace("\r\n", "\n")
                    .Replace("\r", "\n");

            // Remove single-star italic markers.
            text =
                Regex.Replace(
                    text,
                    @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)",
                    "$1");

            // Normalize bullets.
            text =
                Regex.Replace(
                    text,
                    @"^\s*[\*•]\s+",
                    "- ",
                    RegexOptions.Multiline);

            return text;
        }

        private static string StripMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text =
                Regex.Replace(
                    text,
                    @"\*\*(.*?)\*\*",
                    "$1");

            text =
                Regex.Replace(
                    text,
                    @"```(?:[a-zA-Z0-9_+-]+)?\s*",
                    "");

            text =
                text.Replace("```", "");

            return text;
        }

        // ============================================================
        // CONTENT TYPES
        // ============================================================

        private enum ParaKind
        {
            Divider,
            Timestamp,
            Label,
            Question,
            AnswerBullet,
            AnswerNormal,
            Code,
            Table
        }

        private sealed class TableSpec
        {
            public List<List<string>> Rows { get; } =
                new List<List<string>>();
        }

        private sealed class ParaSpec
        {
            public string Text = "";

            public ParaKind Kind;

            public List<(int Start, int End)> HighlightRanges =
                new List<(int Start, int End)>();

            public TableSpec? Table;
        }

        // ============================================================
        // INLINE HIGHLIGHT PARSER
        // ============================================================

        private static (
            string PlainText,
            List<(int Start, int End)> HighlightRanges)
            ParseInlineHighlights(string line)
        {
            var ranges =
                new List<(int, int)>();

            var sb =
                new StringBuilder();

            int i = 0;

            while (i < line.Length)
            {
                if (
                    i + 1 < line.Length &&
                    line[i] == '*' &&
                    line[i + 1] == '*')
                {
                    int closeIdx =
                        line.IndexOf(
                            "**",
                            i + 2,
                            StringComparison.Ordinal);

                    if (closeIdx >= 0)
                    {
                        string inner =
                            line.Substring(
                                i + 2,
                                closeIdx - (i + 2));

                        int start =
                            sb.Length;

                        sb.Append(inner);

                        ranges.Add(
                            (start, sb.Length));

                        i = closeIdx + 2;

                        continue;
                    }
                }

                sb.Append(line[i]);

                i++;
            }

            return (
                sb.ToString(),
                ranges);
        }

        // ============================================================
        // TABLE DETECTION
        // ============================================================

        private static bool IsPotentialTableRow(string line)
        {
            string trimmed =
                line.Trim();

            if (!trimmed.Contains("|"))
                return false;

            string withoutOuterPipes =
                trimmed.Trim('|');

            string[] cells =
                withoutOuterPipes.Split('|');

            return cells.Length >= 2;
        }

        private static bool IsTableSeparator(string line)
        {
            string trimmed =
                line.Trim().Trim('|');

            string[] cells =
                trimmed.Split('|');

            if (cells.Length < 2)
                return false;

            foreach (string cell in cells)
            {
                string c =
                    cell.Trim();

                if (
                    !Regex.IsMatch(
                        c,
                        @"^:?-{3,}:?$"))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<string> ParseTableRow(string line)
        {
            string cleaned =
                line.Trim();

            if (cleaned.StartsWith("|"))
                cleaned = cleaned.Substring(1);

            if (cleaned.EndsWith("|"))
                cleaned =
                    cleaned.Substring(
                        0,
                        cleaned.Length - 1);

            return cleaned
                .Split('|')
                .Select(
                    cell =>
                        cell
                            .Trim()
                            .Replace(
                                "\\|",
                                "|"))
                .ToList();
        }

        private static TableSpec ParseMarkdownTable(
            List<string> lines)
        {
            var table =
                new TableSpec();

            foreach (string line in lines)
            {
                if (IsTableSeparator(line))
                    continue;

                List<string> cells =
                    ParseTableRow(line);

                if (cells.Count > 0)
                    table.Rows.Add(cells);
            }

            // Make every row the same width.
            int maxColumns =
                table.Rows.Count == 0
                    ? 0
                    : table.Rows.Max(
                        row => row.Count);

            foreach (var row in table.Rows)
            {
                while (row.Count < maxColumns)
                    row.Add("");
            }

            return table;
        }

        // ============================================================
        // BUILD CONTENT SPECS
        // ============================================================

        private static List<ParaSpec> BuildContentSpecs(
            string text,
            ParaKind normalKind,
            ParaKind? bulletKind)
        {
            var result =
                new List<ParaSpec>();

            bool inCode = false;

            var codeLines =
                new List<string>();

            var tableLines =
                new List<string>();

            string[] lines =
                text.Split('\n');

            foreach (string rawLine in lines)
            {
                string line =
                    rawLine.TrimEnd();

                // ----------------------------------------------------
                // CODE FENCE
                // ----------------------------------------------------

                if (
                    line.TrimStart()
                        .StartsWith("```"))
                {
                    if (!inCode)
                    {
                        // Start code block.
                        inCode = true;
                        codeLines.Clear();
                    }
                    else
                    {
                        // End code block.
                        inCode = false;

                        foreach (string codeLine in codeLines)
                        {
                            result.Add(
                                new ParaSpec
                                {
                                    Text =
                                        string.IsNullOrEmpty(codeLine)
                                            ? " "
                                            : codeLine,

                                    Kind =
                                        ParaKind.Code
                                });
                        }

                        codeLines.Clear();
                    }

                    continue;
                }

                if (inCode)
                {
                    // IMPORTANT:
                    // Do NOT Trim() code.
                    // Preserve indentation.
                    codeLines.Add(line);

                    continue;
                }

                // ----------------------------------------------------
                // TABLE
                // ----------------------------------------------------

                if (IsPotentialTableRow(line))
                {
                    tableLines.Add(line);

                    continue;
                }

                // If we were collecting a table and this line
                // is no longer part of it, flush the table.
                if (tableLines.Count > 0)
                {
                    if (tableLines.Count >= 2)
                    {
                        result.Add(
                            new ParaSpec
                            {
                                Kind = ParaKind.Table,
                                Table =
                                    ParseMarkdownTable(
                                        tableLines)
                            });
                    }
                    else
                    {
                        // It was not actually a table.
                        foreach (string tableLine in tableLines)
                        {
                            AddNormalLine(
                                result,
                                tableLine,
                                normalKind,
                                bulletKind);
                        }
                    }

                    tableLines.Clear();
                }

                // ----------------------------------------------------
                // NORMAL LINE
                // ----------------------------------------------------

                AddNormalLine(
                    result,
                    line,
                    normalKind,
                    bulletKind);
            }

            // --------------------------------------------------------
            // FLUSH UNFINISHED CODE
            // --------------------------------------------------------

            if (inCode)
            {
                foreach (string codeLine in codeLines)
                {
                    result.Add(
                        new ParaSpec
                        {
                            Text =
                                string.IsNullOrEmpty(codeLine)
                                    ? " "
                                    : codeLine,

                            Kind =
                                ParaKind.Code
                        });
                }
            }

            // --------------------------------------------------------
            // FLUSH TABLE
            // --------------------------------------------------------

            if (tableLines.Count > 0)
            {
                if (tableLines.Count >= 2)
                {
                    result.Add(
                        new ParaSpec
                        {
                            Kind = ParaKind.Table,
                            Table =
                                ParseMarkdownTable(
                                    tableLines)
                        });
                }
                else
                {
                    foreach (string tableLine in tableLines)
                    {
                        AddNormalLine(
                            result,
                            tableLine,
                            normalKind,
                            bulletKind);
                    }
                }
            }

            // --------------------------------------------------------
            // EMPTY CONTENT
            // --------------------------------------------------------

            if (result.Count == 0)
            {
                var parsed =
                    ParseInlineHighlights(
                        text.Trim());

                result.Add(
                    new ParaSpec
                    {
                        Text = parsed.PlainText,
                        Kind = normalKind,
                        HighlightRanges =
                            parsed.HighlightRanges
                    });
            }

            return result;
        }

        private static void AddNormalLine(
            List<ParaSpec> result,
            string line,
            ParaKind normalKind,
            ParaKind? bulletKind)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            bool isBullet =
                bulletKind.HasValue &&
                line.TrimStart().StartsWith("- ");

            string content =
                isBullet
                    ? line.TrimStart()
                        .Substring(2)
                        .Trim()
                    : line.Trim();

            var parsed =
                ParseInlineHighlights(content);

            result.Add(
                new ParaSpec
                {
                    Text = parsed.PlainText,

                    Kind =
                        isBullet
                            ? bulletKind!.Value
                            : normalKind,

                    HighlightRanges =
                        parsed.HighlightRanges
                });
        }

        // ============================================================
        // WRITE Q&A
        // ============================================================

        private static async Task WriteRichQaToDocAsync(
            DocsService docsService,
            string docId,
            string question,
            string answerBody,
            string timestamp)
        {
            var docObj =
                await WithRetryAsync(
                    () =>
                        docsService
                            .Documents
                            .Get(docId)
                            .ExecuteAsync());

            int startIndex =
                docObj.Body?.Content?.Count > 0
                    ? Math.Max(
                        1,
                        (int)(
                            docObj
                                .Body
                                .Content[
                                    docObj.Body.Content.Count - 1]
                                .EndIndex
                            ?? 1) - 1)
                    : 1;

            // --------------------------------------------------------
            // BUILD ORDERED CONTENT
            // --------------------------------------------------------

            var specs =
                new List<ParaSpec>
                {
                    new ParaSpec
                    {
                        Text = " ",
                        Kind = ParaKind.Divider
                    },

                    new ParaSpec
                    {
                        Text = timestamp,
                        Kind = ParaKind.Timestamp
                    },

                    new ParaSpec
                    {
                        Text = "Question",
                        Kind = ParaKind.Label
                    }
                };

            specs.AddRange(
                BuildContentSpecs(
                    question.Trim(),
                    ParaKind.Question,
                    bulletKind: null));

            specs.Add(
                new ParaSpec
                {
                    Text = "Answer",
                    Kind = ParaKind.Label
                });

            specs.AddRange(
                BuildContentSpecs(
                    answerBody,
                    ParaKind.AnswerNormal,
                    ParaKind.AnswerBullet));

            // --------------------------------------------------------
            // WRITE NORMAL TEXT + CODE BLOCKS
            //
            // Tables are handled separately.
            // --------------------------------------------------------

            int cursor =
                startIndex;

            foreach (ParaSpec spec in specs)
            {
                if (spec.Kind == ParaKind.Table)
                    continue;

                string textToInsert =
                    spec.Text + "\n";

                int elementStart =
                    cursor;

                int elementEnd =
                    cursor + textToInsert.Length;

                var insertRequest =
                    new Request
                    {
                        InsertText =
                            new InsertTextRequest
                            {
                                Location =
                                    new Location
                                    {
                                        Index = cursor
                                    },

                                Text =
                                    textToInsert
                            }
                    };

                await WithRetryAsync(
                    () =>
                        docsService
                            .Documents
                            .BatchUpdate(
                                new BatchUpdateDocumentRequest
                                {
                                    Requests =
                                        new List<Request>
                                        {
                                            insertRequest
                                        }
                                },
                                docId)
                            .ExecuteAsync());

                // Format this paragraph.
                await FormatParagraphAsync(
                    docsService,
                    docId,
                    spec,
                    elementStart,
                    elementEnd);

                cursor =
                    elementEnd;
            }

            // --------------------------------------------------------
            // TABLES
            //
            // We rebuild the document positions because tables have
            // their own internal indexes.
            // --------------------------------------------------------

            await InsertTablesAsync(
                docsService,
                docId,
                specs);
        }

        // ============================================================
        // FORMAT PARAGRAPH
        // ============================================================

        private static async Task FormatParagraphAsync(
            DocsService docsService,
            string docId,
            ParaSpec spec,
            int start,
            int end)
        {
            var requests =
                new List<Request>();

            // --------------------------------------------------------
            // TEXT STYLE
            // --------------------------------------------------------

            RgbColor color;

            string font;

            double fontSize;

            switch (spec.Kind)
            {
                case ParaKind.Divider:

                    color =
                        DividerColor;

                    font =
                        BodyFont;

                    fontSize =
                        2;

                    break;

                case ParaKind.Timestamp:

                    color =
                        DateColor;

                    font =
                        BodyFont;

                    fontSize =
                        DateFontSize;

                    break;

                case ParaKind.Label:

                    color =
                        LabelColor;

                    font =
                        BodyFont;

                    fontSize =
                        LabelFontSize;

                    break;

                case ParaKind.Question:

                    color =
                        QuestionColor;

                    font =
                        BodyFont;

                    fontSize =
                        QuestionFontSize;

                    break;

                case ParaKind.Code:

                    color =
                        CodeTextColor;

                    font =
                        CodeFont;

                    fontSize =
                        CodeFontSize;

                    break;

                default:

                    color =
                        AnswerColor;

                    font =
                        BodyFont;

                    fontSize =
                        BodyFontSize;

                    break;
            }

            requests.Add(
                new Request
                {
                    UpdateTextStyle =
                        new UpdateTextStyleRequest
                        {
                            Range =
                                new Google.Apis.Docs.v1.Data.Range
                                {
                                    StartIndex = start,
                                    EndIndex = end
                                },

                            TextStyle =
                                new TextStyle
                                {
                                    Bold = false,
                                    Italic = false,

                                    WeightedFontFamily =
                                        new WeightedFontFamily
                                        {
                                            FontFamily = font,
                                            Weight = 400
                                        },

                                    FontSize =
                                        new Dimension
                                        {
                                            Magnitude =
                                                fontSize,
                                            Unit = "PT"
                                        },

                                    ForegroundColor =
                                        new OptionalColor
                                        {
                                            Color =
                                                new Color
                                                {
                                                    RgbColor =
                                                        color
                                                }
                                        }
                                },

                            Fields =
                                "bold,italic," +
                                "weightedFontFamily," +
                                "fontSize," +
                                "foregroundColor"
                        }
                });

            // --------------------------------------------------------
            // PARAGRAPH SPACING
            // --------------------------------------------------------

            double above = 0;

            double below = 2;

            float lineSpacing = 115;

            switch (spec.Kind)
            {
                case ParaKind.Divider:

                    above = 2;
                    below = 6;
                    lineSpacing = 100;

                    break;

                case ParaKind.Timestamp:

                    above = 0;
                    below = 3;
                    lineSpacing = 100;

                    break;

                case ParaKind.Label:

                    above = 4;
                    below = 1;
                    lineSpacing = 100;

                    break;

                case ParaKind.Question:

                    above = 0;
                    below = 4;
                    lineSpacing = 110;

                    break;

                case ParaKind.Code:

                    // Very compact.
                    above = 0;
                    below = 0;
                    lineSpacing = 100;

                    break;

                case ParaKind.AnswerBullet:

                    above = 0;
                    below = 1;
                    lineSpacing = 110;

                    break;

                case ParaKind.AnswerNormal:

                    above = 0;
                    below = 3;
                    lineSpacing = 110;

                    break;
            }

            requests.Add(
                new Request
                {
                    UpdateParagraphStyle =
                        new UpdateParagraphStyleRequest
                        {
                            Range =
                                new Google.Apis.Docs.v1.Data.Range
                                {
                                    StartIndex = start,
                                    EndIndex = end
                                },

                            ParagraphStyle =
                                new Google.Apis.Docs.v1.Data.ParagraphStyle
                                {
                                    SpaceAbove =
                                        new Dimension
                                        {
                                            Magnitude = above,
                                            Unit = "PT"
                                        },

                                    SpaceBelow =
                                        new Dimension
                                        {
                                            Magnitude = below,
                                            Unit = "PT"
                                        },

                                    LineSpacing =
                                        lineSpacing
                                },

                            Fields =
                                "spaceAbove," +
                                "spaceBelow," +
                                "lineSpacing"
                        }
                });

            // --------------------------------------------------------
            // CODE BOX
            // --------------------------------------------------------

            if (spec.Kind == ParaKind.Code)
            {
                var border =
                    new ParagraphBorder
                    {
                        Color =
                            new OptionalColor
                            {
                                Color =
                                    new Color
                                    {
                                        RgbColor =
                                            CodeBoxBorder
                                    }
                            },

                        DashStyle = "SOLID",

                        Width =
                            new Dimension
                            {
                                Magnitude = 0.75,
                                Unit = "PT"
                            },

                        Padding =
                            new Dimension
                            {
                                Magnitude = 2,
                                Unit = "PT"
                            }
                    };

                requests.Add(
                    new Request
                    {
                        UpdateParagraphStyle =
                            new UpdateParagraphStyleRequest
                            {
                                Range =
                                    new Google.Apis.Docs.v1.Data.Range
                                    {
                                        StartIndex = start,
                                        EndIndex = end
                                    },

                                ParagraphStyle =
                                    new Google.Apis.Docs.v1.Data.ParagraphStyle
                                    {
                                        Shading =
                                            new Shading
                                            {
                                                BackgroundColor =
                                                    CodeBoxFill
                                            },

                                        BorderLeft = border,
                                        BorderRight = border,

                                        IndentStart =
                                            new Dimension
                                            {
                                                Magnitude = 3,
                                                Unit = "PT"
                                            },

                                        IndentEnd =
                                            new Dimension
                                            {
                                                Magnitude = 3,
                                                Unit = "PT"
                                            }
                                    },

                                Fields =
                                    "shading," +
                                    "borderLeft," +
                                    "borderRight," +
                                    "indentStart," +
                                    "indentEnd"
                            }
                    });
            }

            // --------------------------------------------------------
            // DIVIDER
            // --------------------------------------------------------

            if (spec.Kind == ParaKind.Divider)
            {
                requests.Add(
                    new Request
                    {
                        UpdateParagraphStyle =
                            new UpdateParagraphStyleRequest
                            {
                                Range =
                                    new Google.Apis.Docs.v1.Data.Range
                                    {
                                        StartIndex = start,
                                        EndIndex = end
                                    },

                                ParagraphStyle =
                                    new Google.Apis.Docs.v1.Data.ParagraphStyle
                                    {
                                        BorderBottom =
                                            new ParagraphBorder
                                            {
                                                Color =
                                                    new OptionalColor
                                                    {
                                                        Color =
                                                            new Color
                                                            {
                                                                RgbColor =
                                                                    DividerColor
                                                            }
                                                    },

                                                DashStyle = "SOLID",

                                                Width =
                                                    new Dimension
                                                    {
                                                        Magnitude = 0.75,
                                                        Unit = "PT"
                                                    },

                                                Padding =
                                                    new Dimension
                                                    {
                                                        Magnitude = 1,
                                                        Unit = "PT"
                                                    }
                                            }
                                    },

                                Fields =
                                    "borderBottom"
                            }
                    });
            }

            // --------------------------------------------------------
            // IMPORTANT TEXT HIGHLIGHTS
            // --------------------------------------------------------

            if (
                spec.Kind != ParaKind.Code &&
                spec.HighlightRanges.Count > 0)
            {
                foreach (
                    var range
                    in spec.HighlightRanges)
                {
                    if (range.End <= range.Start)
                        continue;

                    requests.Add(
                        new Request
                        {
                            UpdateTextStyle =
                                new UpdateTextStyleRequest
                                {
                                    Range =
                                        new Google.Apis.Docs.v1.Data.Range
                                        {
                                            StartIndex =
                                                start +
                                                range.Start,

                                            EndIndex =
                                                start +
                                                range.End
                                        },

                                    TextStyle =
                                        new TextStyle
                                        {
                                            ForegroundColor =
                                                new OptionalColor
                                                {
                                                    Color =
                                                        new Color
                                                        {
                                                            RgbColor =
                                                                HighlightColor
                                                        }
                                                }
                                        },

                                    Fields =
                                        "foregroundColor"
                                }
                        });
                }
            }

            // --------------------------------------------------------
            // NATIVE BULLET
            // --------------------------------------------------------

            if (spec.Kind == ParaKind.AnswerBullet)
            {
                requests.Add(
                    new Request
                    {
                        CreateParagraphBullets =
                            new CreateParagraphBulletsRequest
                            {
                                Range =
                                    new Google.Apis.Docs.v1.Data.Range
                                    {
                                        StartIndex = start,
                                        EndIndex = end
                                    },

                                BulletPreset =
                                    "BULLET_DISC_CIRCLE_SQUARE"
                            }
                    });
            }

            if (requests.Count == 0)
                return;

            await WithRetryAsync(
                () =>
                    docsService
                        .Documents
                        .BatchUpdate(
                            new BatchUpdateDocumentRequest
                            {
                                Requests = requests
                            },
                            docId)
                        .ExecuteAsync());
        }

        // ============================================================
        // INSERT TABLES
        // ============================================================

        private static async Task InsertTablesAsync(
            DocsService docsService,
            string docId,
            List<ParaSpec> specs)
        {
            var tableSpecs =
                specs
                    .Where(
                        x =>
                            x.Kind == ParaKind.Table &&
                            x.Table != null &&
                            x.Table.Rows.Count > 0)
                    .ToList();

            if (tableSpecs.Count == 0)
                return;

            /*
             * Tables need actual document locations.
             *
             * We rebuild the document after the normal paragraphs have
             * been inserted and append each table to the end.
             *
             * This means the table is structurally a real Google Docs
             * table rather than Markdown text.
             */

            foreach (ParaSpec spec in tableSpecs)
            {
                var latestDoc =
                    await WithRetryAsync(
                        () =>
                            docsService
                                .Documents
                                .Get(docId)
                                .ExecuteAsync());

                int insertIndex =
                    GetDocumentEndIndex(latestDoc);

                int rows =
                    spec.Table!.Rows.Count;

                int columns =
                    spec.Table.Rows.Max(
                        r => r.Count);

                if (columns <= 0)
                    continue;

                // ----------------------------------------------------
                // INSERT TABLE
                // ----------------------------------------------------

                var insertTableRequest =
                    new Request
                    {
                        InsertTable =
                            new InsertTableRequest
                            {
                                Rows = rows,
                                Columns = columns,

                                Location =
                                    new Location
                                    {
                                        Index =
                                            insertIndex
                                    }
                            }
                    };

                await WithRetryAsync(
                    () =>
                        docsService
                            .Documents
                            .BatchUpdate(
                                new BatchUpdateDocumentRequest
                                {
                                    Requests =
                                        new List<Request>
                                        {
                                            insertTableRequest
                                        }
                                },
                                docId)
                            .ExecuteAsync());

                // ----------------------------------------------------
                // GET TABLE POSITIONS
                // ----------------------------------------------------

                var updatedDoc =
                    await WithRetryAsync(
                        () =>
                            docsService
                                .Documents
                                .Get(docId)
                                .ExecuteAsync());

                var (table, _) =
                    FindLastTable(updatedDoc);

                if (table == null)
                    continue;

                // ----------------------------------------------------
                // FILL CELLS
                // ----------------------------------------------------

                await FillTableCellsAsync(
                    docsService,
                    docId,
                    table,
                    spec.Table.Rows);

                // ----------------------------------------------------
                // STYLE TABLE
                // ----------------------------------------------------

                await StyleTableAsync(
                    docsService,
                    docId,
                    table,
                    spec.Table.Rows.Count,
                    columns);
            }
        }

        // ============================================================
        // GET DOCUMENT END
        // ============================================================

        private static int GetDocumentEndIndex(
            Document document)
        {
            if (
                document.Body == null ||
                document.Body.Content == null ||
                document.Body.Content.Count == 0)
            {
                return 1;
            }

            var last =
                document.Body.Content[
                    document.Body.Content.Count - 1];

            return Math.Max(
                1,
                (int)(last.EndIndex ?? 1) - 1);
        }

        // ============================================================
        // FIND LAST TABLE
        // ============================================================

        private static (Table? Table, int StartIndex) FindLastTable(
            Document document)
        {
            if (
                document.Body?.Content == null)
            {
                return (null, 0);
            }

            Table? result = null;
            int startIndex = 1;

            foreach (
                var element
                in document.Body.Content)
            {
                if (element.Table != null)
                {
                    result =
                        element.Table;
                    startIndex =
                        (int)(element.StartIndex ?? 1);
                }
            }

            return (result, startIndex);
        }

        // ============================================================
        // GET CELL INSERT POSITION
        // ============================================================

        private static int GetCellStartIndex(
            TableCell cell)
        {
            if (
                cell.Content == null ||
                cell.Content.Count == 0)
            {
                return 1;
            }

            var first =
                cell.Content[0];

            return (int)(first.StartIndex ?? 1);
        }

        // ============================================================
        // FILL TABLE CELLS
        // ============================================================

        private static async Task FillTableCellsAsync(
            DocsService docsService,
            string docId,
            Table table,
            List<List<string>> rows)
        {
            var cells =
                new List<(
                    int Index,
                    string Text,
                    int Row,
                    int Column)>();

            for (int r = 0; r < table.TableRows.Count; r++)
            {
                var tableRow =
                    table.TableRows[r];

                for (
                    int c = 0;
                    c < tableRow.TableCells.Count;
                    c++)
                {
                    var cell =
                        tableRow.TableCells[c];

                    int index =
                        GetCellStartIndex(cell);

                    string text =
                        r < rows.Count &&
                        c < rows[r].Count
                            ? rows[r][c]
                            : "";

                    cells.Add(
                        (
                            index,
                            text,
                            r,
                            c));
                }
            }

            /*
             * Insert from the bottom/right toward the top/left.
             *
             * This prevents earlier insertions from changing the
             * indexes of cells that still need text.
             */

            foreach (
                var cell
                in cells.OrderByDescending(
                    x => x.Index))
            {
                string text =
                    cell.Text;

                if (string.IsNullOrEmpty(text))
                    text = " ";

                var request =
                    new Request
                    {
                        InsertText =
                            new InsertTextRequest
                            {
                                Location =
                                    new Location
                                    {
                                        Index =
                                            cell.Index
                                    },

                                Text =
                                    text
                            }
                    };

                await WithRetryAsync(
                    () =>
                        docsService
                            .Documents
                            .BatchUpdate(
                                new BatchUpdateDocumentRequest
                                {
                                    Requests =
                                        new List<Request>
                                        {
                                            request
                                        }
                                },
                                docId)
                            .ExecuteAsync());
            }
        }

        // ============================================================
        // STYLE TABLE
        // ============================================================

        private static async Task StyleTableAsync(
            DocsService docsService,
            string docId,
            Table table,
            int rowCount,
            int columnCount)
        {
            var requests =
                new List<Request>();

            // --------------------------------------------------------
            // RELOAD DOCUMENT
            // --------------------------------------------------------

            var latestDoc =
                await WithRetryAsync(
                    () =>
                        docsService
                            .Documents
                            .Get(docId)
                            .ExecuteAsync());

            var (latestTable, tableStartIndex) =
                FindLastTable(latestDoc);

            if (latestTable == null)
                return;

            // --------------------------------------------------------
            // STYLE EVERY CELL
            // --------------------------------------------------------

            for (
                int r = 0;
                r < latestTable.TableRows.Count;
                r++)
            {
                var row =
                    latestTable.TableRows[r];

                for (
                    int c = 0;
                    c < row.TableCells.Count;
                    c++)
                {
                    var cell =
                        row.TableCells[c];

                    if (
                        cell.Content == null ||
                        cell.Content.Count == 0)
                    {
                        continue;
                    }

                    int start =
                        GetCellStartIndex(cell);

                    int end =
                        GetCellEndIndex(cell);

                    if (end <= start)
                        continue;

                    bool isHeader =
                        r == 0;

                    // --------------------------------------------
                    // TEXT
                    // --------------------------------------------

                    requests.Add(
                        new Request
                        {
                            UpdateTextStyle =
                                new UpdateTextStyleRequest
                                {
                                    Range =
                                        new Google.Apis.Docs.v1.Data.Range
                                        {
                                            StartIndex = start,
                                            EndIndex = end
                                        },

                                    TextStyle =
                                        new TextStyle
                                        {
                                            Bold =
                                                isHeader,

                                            Italic = false,

                                            WeightedFontFamily =
                                                new WeightedFontFamily
                                                {
                                                    FontFamily =
                                                        BodyFont,
                                                    Weight =
                                                        isHeader
                                                            ? 600
                                                            : 400
                                                },

                                            FontSize =
                                                new Dimension
                                                {
                                                    Magnitude =
                                                        TableFontSize,
                                                    Unit = "PT"
                                                },

                                            ForegroundColor =
                                                new OptionalColor
                                                {
                                                    Color =
                                                        new Color
                                                        {
                                                            RgbColor =
                                                                TableTextColor
                                                        }
                                                }
                                        },

                                    Fields =
                                        "bold," +
                                        "italic," +
                                        "weightedFontFamily," +
                                        "fontSize," +
                                        "foregroundColor"
                                }
                        });

                    // --------------------------------------------
                    // PARAGRAPH
                    // --------------------------------------------

                    requests.Add(
                        new Request
                        {
                            UpdateParagraphStyle =
                                new UpdateParagraphStyleRequest
                                {
                                    Range =
                                        new Google.Apis.Docs.v1.Data.Range
                                        {
                                            StartIndex = start,
                                            EndIndex = end
                                        },

                                    ParagraphStyle =
                                        new Google.Apis.Docs.v1.Data.ParagraphStyle
                                        {
                                            SpaceAbove =
                                                new Dimension
                                                {
                                                    Magnitude = 0,
                                                    Unit = "PT"
                                                },

                                            SpaceBelow =
                                                new Dimension
                                                {
                                                    Magnitude = 0,
                                                    Unit = "PT"
                                                },

                                            LineSpacing =
                                                100
                                        },

                                    Fields =
                                        "spaceAbove," +
                                        "spaceBelow," +
                                        "lineSpacing"
                                }
                        });
                }
            }

            // --------------------------------------------------------
            // HEADER BACKGROUND
            // --------------------------------------------------------

            if (
                latestTable.TableRows.Count > 0)
            {
                var headerRow =
                    latestTable.TableRows[0];

                foreach (
                    var cell
                    in headerRow.TableCells)
                {
                    requests.Add(
                        new Request
                        {
                            UpdateTableCellStyle =
                                new UpdateTableCellStyleRequest
                                {
                                    TableRange =
                                        new TableRange
                                        {
                                            TableCellLocation =
                                                new TableCellLocation
                                                {
                                                    TableStartLocation =
                                                        new Location
                                                        {
                                                            Index =
                                                                tableStartIndex
                                                        },

                                                    RowIndex = 0,

                                                    ColumnIndex =
                                                        headerRow
                                                            .TableCells
                                                            .IndexOf(cell)
                                                },

                                            RowSpan = 1,
                                            ColumnSpan = 1
                                        },

                                    TableCellStyle =
                                        new TableCellStyle
                                        {
                                            BackgroundColor =
                                                new OptionalColor
                                                {
                                                    Color =
                                                        new Color
                                                        {
                                                            RgbColor =
                                                                TableHeaderColor
                                                        }
                                                }
                                        },

                                    Fields =
                                        "backgroundColor"
                                }
                        });
                }
            }

            // --------------------------------------------------------
            // TABLE BORDERS / PADDING
            // --------------------------------------------------------

            requests.Add(
                new Request
                {
                    UpdateTableCellStyle =
                        new UpdateTableCellStyleRequest
                        {
                            TableRange =
                                new TableRange
                                {
                                    TableCellLocation =
                                        new TableCellLocation
                                        {
                                            TableStartLocation =
                                                new Location
                                                {
                                                    Index =
                                                        tableStartIndex
                                                },

                                            RowIndex = 0,
                                            ColumnIndex = 0
                                        },

                                    RowSpan =
                                        latestTable
                                            .TableRows
                                            .Count,

                                    ColumnSpan =
                                        columnCount
                                },

                            TableCellStyle =
                                new TableCellStyle
                                {
                                    PaddingTop =
                                        new Dimension
                                        {
                                            Magnitude = 3,
                                            Unit = "PT"
                                        },

                                    PaddingBottom =
                                        new Dimension
                                        {
                                            Magnitude = 3,
                                            Unit = "PT"
                                        },

                                    PaddingLeft =
                                        new Dimension
                                        {
                                            Magnitude = 5,
                                            Unit = "PT"
                                        },

                                    PaddingRight =
                                        new Dimension
                                        {
                                            Magnitude = 5,
                                            Unit = "PT"
                                        },

                                    BorderTop =
                                        CreateTableBorder(),

                                    BorderBottom =
                                        CreateTableBorder(),

                                    BorderLeft =
                                        CreateTableBorder(),

                                    BorderRight =
                                        CreateTableBorder()
                                },

                            Fields =
                                "paddingTop," +
                                "paddingBottom," +
                                "paddingLeft," +
                                "paddingRight," +
                                "borderTop," +
                                "borderBottom," +
                                "borderLeft," +
                                "borderRight"
                        }
                });

            if (requests.Count == 0)
                return;

            await WithRetryAsync(
                () =>
                    docsService
                        .Documents
                        .BatchUpdate(
                            new BatchUpdateDocumentRequest
                            {
                                Requests = requests
                            },
                            docId)
                        .ExecuteAsync());
        }

        // ============================================================
        // TABLE BORDER
        // ============================================================

        private static TableCellBorder CreateTableBorder()
        {
            return new TableCellBorder
            {
                Color =
                    new OptionalColor
                    {
                        Color =
                            new Color
                            {
                                RgbColor =
                                    TableBorderColor
                            }
                    },

                Width =
                    new Dimension
                    {
                        Magnitude = 0.5,
                        Unit = "PT"
                    },

                DashStyle = "SOLID"
            };
        }

        // ============================================================
        // CELL END INDEX
        // ============================================================

        private static int GetCellEndIndex(
            TableCell cell)
        {
            if (
                cell.Content == null ||
                cell.Content.Count == 0)
            {
                return GetCellStartIndex(cell) + 1;
            }

            var last =
                cell.Content[
                    cell.Content.Count - 1];

            return (int)(
                last.EndIndex
                ?? (
                    GetCellStartIndex(cell) + 1));
        }

        // ============================================================
        // RETRY
        // ============================================================

        private static readonly HashSet<int>
            TransientStatusCodes =
                new HashSet<int>
                {
                    429,
                    500,
                    502,
                    503,
                    504
                };

        private static bool IsTransient(
            GoogleApiException ex)
        {
            return TransientStatusCodes.Contains(
                (int)ex.HttpStatusCode);
        }

        private static async Task<T> WithRetryAsync<T>(
            Func<Task<T>> action,
            int maxAttempts = 3)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (GoogleApiException ex)
                    when (
                        attempt < maxAttempts &&
                        IsTransient(ex))
                {
                    Debug.WriteLine(
                        $"Transient Google API error " +
                        $"(attempt {attempt}/{maxAttempts}): " +
                        ex.Message);

                    await Task.Delay(
                        TimeSpan.FromMilliseconds(
                            500 *
                            Math.Pow(
                                2,
                                attempt - 1)));
                }
            }
        }
    }
}
