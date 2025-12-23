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

        /// <summary>
        /// Gets whether this message has any attached files.
        /// </summary>
        public bool HasAttachedFiles => AttachedFiles != null && AttachedFiles.Count > 0;

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
        /// Gets the message content with attached file contents included as context.
        /// </summary>
        public string GetContentWithFileContext()
        {
            if (!HasAttachedFiles)
            {
                return Content;
            }

            var sb = new StringBuilder();
            sb.AppendLine(Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("**Referenced Files:**");
            sb.AppendLine();

            foreach (var file in AttachedFiles)
            {
                sb.Append(file.ToFormattedContext());
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a display-friendly summary of attached files.
        /// </summary>
        public string GetAttachmentsSummary()
        {
            if (!HasAttachedFiles)
            {
                return "";
            }

            var successCount = SuccessfulAttachmentCount;
            var totalCount = AttachedFiles.Count;

            if (successCount == totalCount)
            {
                return $"📎 {totalCount} file{(totalCount == 1 ? "" : "s")} attached";
            }
            else
            {
                return $"📎 {successCount}/{totalCount} files attached";
            }
        }
    }
}
#endif
