#if TOOLS
using System;
using System.Collections.Generic;
using System.Text;

namespace G0.Models
{
    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// List of file references attached to this message.
        /// </summary>
        public List<FileReference> AttachedFiles { get; set; } = new List<FileReference>();

        /// <summary>
        /// List of code snippets attached to this message.
        /// </summary>
        public List<CodeSnippet> AttachedSnippets { get; set; } = new List<CodeSnippet>();

        public ChatMessage() { }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
        }

        public ChatMessage(string role, string content, List<FileReference> attachedFiles)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
            AttachedFiles = attachedFiles ?? new List<FileReference>();
        }

        public ChatMessage(string role, string content, List<FileReference> attachedFiles, List<CodeSnippet> attachedSnippets)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
            AttachedFiles = attachedFiles ?? new List<FileReference>();
            AttachedSnippets = attachedSnippets ?? new List<CodeSnippet>();
        }

        /// <summary>
        /// Gets whether this message has any attached files.
        /// </summary>
        public bool HasAttachedFiles => AttachedFiles != null && AttachedFiles.Count > 0;

        /// <summary>
        /// Gets whether this message has any attached code snippets.
        /// </summary>
        public bool HasAttachedSnippets => AttachedSnippets != null && AttachedSnippets.Count > 0;

        /// <summary>
        /// Gets whether this message has any attachments (files or snippets).
        /// </summary>
        public bool HasAttachments => HasAttachedFiles || HasAttachedSnippets;

        /// <summary>
        /// Gets the count of successfully read attached files.
        /// </summary>
        public int SuccessfulAttachmentCount
        {
            get
            {
                int count = 0;
                if (AttachedFiles != null)
                {
                    foreach (var file in AttachedFiles)
                    {
                        if (file.Exists && string.IsNullOrEmpty(file.ErrorMessage))
                        {
                            count++;
                        }
                    }
                }
                return count;
            }
        }

        public Godot.Collections.Dictionary ToDictionary()
        {
            var dict = new Godot.Collections.Dictionary
            {
                { "role", Role },
                { "content", Content },
                { "timestamp", Timestamp.ToString("o") }
            };

            // Serialize attached files
            if (AttachedFiles != null && AttachedFiles.Count > 0)
            {
                var filesArray = new Godot.Collections.Array();
                foreach (var file in AttachedFiles)
                {
                    filesArray.Add(file.ToDictionary());
                }
                dict["attached_files"] = filesArray;
            }

            // Serialize attached snippets
            if (AttachedSnippets != null && AttachedSnippets.Count > 0)
            {
                var snippetsArray = new Godot.Collections.Array();
                foreach (var snippet in AttachedSnippets)
                {
                    snippetsArray.Add(snippet.ToDictionary());
                }
                dict["attached_snippets"] = snippetsArray;
            }

            return dict;
        }

        public static ChatMessage FromDictionary(Godot.Collections.Dictionary dict)
        {
            var message = new ChatMessage
            {
                Role = dict.ContainsKey("role") ? dict["role"].AsString() : "user",
                Content = dict.ContainsKey("content") ? dict["content"].AsString() : "",
                Timestamp = dict.ContainsKey("timestamp") 
                    ? DateTime.Parse(dict["timestamp"].AsString()) 
                    : DateTime.Now
            };

            // Deserialize attached files
            if (dict.ContainsKey("attached_files"))
            {
                var filesArray = dict["attached_files"].AsGodotArray();
                foreach (var fileDict in filesArray)
                {
                    var fileReference = FileReference.FromDictionary(fileDict.AsGodotDictionary());
                    message.AttachedFiles.Add(fileReference);
                }
            }

            // Deserialize attached snippets
            if (dict.ContainsKey("attached_snippets"))
            {
                var snippetsArray = dict["attached_snippets"].AsGodotArray();
                foreach (var snippetDict in snippetsArray)
                {
                    var snippet = CodeSnippet.FromDictionary(snippetDict.AsGodotDictionary());
                    message.AttachedSnippets.Add(snippet);
                }
            }

            return message;
        }

        /// <summary>
        /// Converts the message to API format, including file contents as context.
        /// </summary>
        public Godot.Collections.Dictionary ToApiFormat()
        {
            return new Godot.Collections.Dictionary
            {
                { "role", Role },
                { "content", GetContentWithFileContext() }
            };
        }

        /// <summary>
        /// Gets the message content with attached file contents and snippets included as context.
        /// </summary>
        public string GetContentWithFileContext()
        {
            if (!HasAttachments)
            {
                return Content;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Content);
            sb.AppendLine();
            sb.AppendLine("---");

            // Add file references
            if (HasAttachedFiles)
            {
                sb.AppendLine("**Referenced Files:**");
                sb.AppendLine();

                foreach (var file in AttachedFiles)
                {
                    sb.Append(file.ToFormattedContext());
                    sb.AppendLine();
                }
            }

            // Add code snippets
            if (HasAttachedSnippets)
            {
                sb.AppendLine("**Code Snippets:**");
                sb.AppendLine();

                foreach (var snippet in AttachedSnippets)
                {
                    sb.Append(snippet.ToFormattedContext());
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a display-friendly summary of attached files and snippets.
        /// </summary>
        public string GetAttachmentsSummary()
        {
            var parts = new List<string>();

            if (HasAttachedFiles)
            {
                var successCount = SuccessfulAttachmentCount;
                var totalCount = AttachedFiles.Count;

                if (successCount == totalCount)
                {
                    parts.Add($"📎 {totalCount} file{(totalCount == 1 ? "" : "s")}");
                }
                else
                {
                    parts.Add($"📎 {successCount}/{totalCount} files");
                }
            }

            if (HasAttachedSnippets)
            {
                var snippetCount = AttachedSnippets.Count;
                parts.Add($"✂️ {snippetCount} snippet{(snippetCount == 1 ? "" : "s")}");
            }

            if (parts.Count == 0)
            {
                return "";
            }

            return string.Join(" • ", parts) + " attached";
        }

        /// <summary>
        /// Gets the count of attached snippets.
        /// </summary>
        public int SnippetCount => AttachedSnippets?.Count ?? 0;
    }
}
#endif
