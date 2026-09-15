using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace SnapMini.Services
{
    /// <summary>
    /// Export service for Google Docs & Google Drive.
    /// Smooths and summarizes Q&A with AI before appending to existing Google Documents or creating new documents in Drive.
    /// Supports fallback opening in web browser with Q&A pre-copied to clipboard.
    /// </summary>
    public static class GoogleDocsService
    {
        public static async Task<string> ExportQaSummaryAsync(string question, string answer)
        {
            var settings = AIService.ReadSettings();

            string rawFolder = (settings.GoogleDriveFolderId ?? "").Trim();
            string customLink = (settings.GoogleDocsCustomLink ?? "").Trim();
            string credentialsJson = (settings.GoogleCredentialsJson ?? "").Trim();

            // 1. AI Smoothing: Summarize and refine Question & Answer to include ONLY key important points
            string smoothedContent = "";
            try
            {
                string smoothingPrompt = $"Refine and smooth the following Question and AI Answer into a concise, high-level summary containing ONLY the core key points and essential takeaways. Omit all unnecessary filler or repetition.\n\n" +
                                         $"Question:\n{question}\n\n" +
                                         $"Answer:\n{answer}";

                smoothedContent = await AIService.GetAnswerAsync(smoothingPrompt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AI Smoothing notice: {ex.Message}");
            }

            string finalAnswerBody = !string.IsNullOrWhiteSpace(smoothedContent) ? smoothedContent.Trim() : answer.Trim();

            // Format Q&A text payload
            string timestamp = DateTime.Now.ToString("g");
            string textToCopy = $"========================================\n" +
                                $"Q&A Summary ({timestamp}):\n" +
                                $"Question:\n{question.Trim()}\n\n" +
                                $"Key Takeaways & Answer:\n{finalAnswerBody}\n" +
                                $"========================================\n\n";

            // Check if user provided an existing Document link or ID
            string existingDocId = "";
            string combinedTarget = customLink + " " + rawFolder;
            var docMatch = Regex.Match(combinedTarget, @"/document/d/([a-zA-Z0-9_-]+)");
            if (docMatch.Success)
            {
                existingDocId = docMatch.Groups[1].Value;
            }

            // Check if user provided a Drive Folder link or ID
            string folderId = "";
            var folderMatch = Regex.Match(rawFolder, @"/folders/([a-zA-Z0-9_-]+)");
            if (folderMatch.Success)
            {
                folderId = folderMatch.Groups[1].Value;
            }
            else if (!rawFolder.StartsWith("http") && !rawFolder.Contains("/"))
            {
                folderId = rawFolder;
            }

            // Resolve google_credentials.json location across build & source paths
            string defaultJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google_credentials.json");
            if (!File.Exists(defaultJsonPath))
            {
                string altPath = Path.Combine(Directory.GetCurrentDirectory(), "google_credentials.json");
                if (File.Exists(altPath))
                {
                    defaultJsonPath = altPath;
                }
            }

            bool hasFileCreds = File.Exists(defaultJsonPath);
            bool hasStringCreds = !string.IsNullOrWhiteSpace(credentialsJson);

            if (hasFileCreds || hasStringCreds)
            {
                try
                {
#pragma warning disable CS0618
                    GoogleCredential credential;
                    if (hasStringCreds)
                    {
                        credential = GoogleCredential.FromJson(credentialsJson).CreateScoped(new[]
                        {
                            DocsService.Scope.Documents,
                            DriveService.Scope.Drive
                        });
                    }
                    else
                    {
                        using var stream = new FileStream(defaultJsonPath, FileMode.Open, FileAccess.Read);
                        credential = GoogleCredential.FromStream(stream).CreateScoped(new[]
                        {
                            DocsService.Scope.Documents,
                            DriveService.Scope.Drive
                        });
                    }
#pragma warning restore CS0618

                    var docsService = new DocsService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "SnapMini"
                    });

                    var driveService = new DriveService(new BaseClientService.Initializer
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "SnapMini"
                    });

                    // CASE 1: Append to an existing Google Document
                    if (!string.IsNullOrWhiteSpace(existingDocId))
                    {
                        try
                        {
                            var docObj = await docsService.Documents.Get(existingDocId).ExecuteAsync();
                            int endIndex = docObj.Body?.Content?.Count > 0 
                                ? Math.Max(1, (int)(docObj.Body.Content[docObj.Body.Content.Count - 1].EndIndex ?? 1) - 1)
                                : 1;

                            var requests = new List<Request>
                            {
                                new Request
                                {
                                    InsertText = new InsertTextRequest
                                    {
                                        Location = new Location { Index = endIndex },
                                        Text = textToCopy
                                    }
                                }
                            };
                            await docsService.Documents.BatchUpdate(new BatchUpdateDocumentRequest { Requests = requests }, existingDocId).ExecuteAsync();

                            string docUrl = $"https://docs.google.com/document/d/{existingDocId}/edit";
                            return docUrl;
                        }
                        catch (Exception ex) when (ex.Message.Contains("Forbidden") || ex.Message.Contains("403"))
                        {
                            MessageBox.Show(
                                "To save directly into this existing Google Document, please share it with your Service Account email:\n\n" +
                                "1. Open your document in Google Drive.\n" +
                                "2. Click 'Share' (top right).\n" +
                                "3. Paste your Service Account email (from google_credentials.json).\n" +
                                "4. Set permission to 'Editor' and click Save.\n\n" +
                                "Falling back to Clipboard copy.", 
                                "Google Document Permission Needed", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Information);
                        }
                    }

                    // CASE 2: Create a NEW Google Document inside target folder or root Drive
                    string title = $"SnapMini Q&A - {DateTime.Now:yyyy-MM-dd HH:mm}";
                    var newDoc = new Document { Title = title };
                    var createdDoc = await docsService.Documents.Create(newDoc).ExecuteAsync();
                    string docId = createdDoc.DocumentId;

                    // Write Q&A Content
                    var writeReqs = new List<Request>
                    {
                        new Request
                        {
                            InsertText = new InsertTextRequest
                            {
                                Location = new Location { Index = 1 },
                                Text = textToCopy
                            }
                        }
                    };
                    await docsService.Documents.BatchUpdate(new BatchUpdateDocumentRequest { Requests = writeReqs }, docId).ExecuteAsync();

                    // Grant permission so user opening the link in browser can view/edit
                    try
                    {
                        var permission = new Google.Apis.Drive.v3.Data.Permission
                        {
                            Type = "anyone",
                            Role = "writer"
                        };
                        await driveService.Permissions.Create(permission, docId).ExecuteAsync();
                    }
                    catch (Exception permEx)
                    {
                        Debug.WriteLine($"Permission grant warning: {permEx.Message}");
                    }

                    // Move file into target folder if folder ID specified
                    if (!string.IsNullOrWhiteSpace(folderId))
                    {
                        try
                        {
                            var getReq = driveService.Files.Get(docId);
                            getReq.Fields = "parents";
                            var file = await getReq.ExecuteAsync();

                            string previousParents = string.Join(",", file.Parents ?? new List<string>());
                            var updateReq = driveService.Files.Update(new Google.Apis.Drive.v3.Data.File(), docId);
                            updateReq.AddParents = folderId;
                            updateReq.RemoveParents = previousParents;
                            await updateReq.ExecuteAsync();
                        }
                        catch (Exception moveEx)
                        {
                            Debug.WriteLine($"Move to folder warning: {moveEx.Message}");
                        }
                    }

                    string newDocUrl = $"https://docs.google.com/document/d/{docId}/edit";
                    return newDocUrl;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Google API Error: {ex.Message}");
                    MessageBox.Show($"Google API Notice:\n{ex.Message}\n\nFalling back to Clipboard copy & browser launch.", "SnapMini Google Export");
                }
            }

            // Fallback: Copy to Clipboard & launch target Google Link or default Google Docs URL
            Clipboard.SetText(textToCopy);

            string targetUrl = !string.IsNullOrWhiteSpace(customLink) ? customLink :
                               (!string.IsNullOrWhiteSpace(rawFolder) ? rawFolder : "https://docs.google.com/document/create");

            if (!targetUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                targetUrl = $"https://drive.google.com/drive/folders/{targetUrl}";
            }

            Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
            return targetUrl;
        }
    }
}
