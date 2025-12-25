#if TOOLS
using System;
using System.IO;
using System.Text;
using Godot;

namespace G0.Models
{
    /// <summary>
    /// Represents a code snippet selected from the Godot script editor.
    /// Contains the selected code content along with file path and line range metadata.
    /// </summary>
    public class CodeSnippet
    {
        /// <summary>
        /// The relative file path from the project root.
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// The selected code content.
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// The starting line number (1-based).
        /// </summary>
        public int StartLine { get; set; } = 1;

        /// <summary>
        /// The ending line number (1-based).
        /// </summary>
        public int EndLine { get; set; } = 1;

        /// <summary>
        /// The programming language of the snippet (e.g., "csharp", "gdscript").
        /// </summary>
        public string Language { get; set; } = "";

        /// <summary>
        /// Timestamp when the snippet was created.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public CodeSnippet() { }

        public CodeSnippet(string filePath, string content, int startLine, int endLine)
        {
            FilePath = filePath;
            Content = content;
            StartLine = startLine;
            EndLine = endLine;
            Language = GetLanguageFromFilePath(filePath);
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// Gets the display summary for the snippet badge (e.g., "Player.gd:42-58").
        /// </summary>
        public string GetDisplaySummary()
        {
            var fileName = Path.GetFileName(FilePath);
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = FilePath;
            }

            if (StartLine == EndLine)
            {
                return $"{fileName}:{StartLine}";
            }
            return $"{fileName}:{StartLine}-{EndLine}";
        }

        /// <summary>
        /// Gets the number of lines in the snippet.
        /// </summary>
        public int LineCount => Math.Max(1, EndLine - StartLine + 1);

        /// <summary>
        /// Formats the snippet for inclusion in the AI context.
        /// Includes file path, line range, and the code content.
        /// </summary>
        public string ToFormattedContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Code Snippet from: {FilePath} (lines {StartLine}-{EndLine})");
            sb.AppendLine($"```{Language}");
            
            // Add line numbers to each line
            var lines = Content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var lineNumber = StartLine + i;
                sb.AppendLine($"{lineNumber,4} | {lines[i].TrimEnd('\r')}");
            }
            
            sb.AppendLine("```");
            return sb.ToString();
        }

        /// <summary>
        /// Gets a preview of the code content, truncated if too long.
        /// </summary>
        /// <param name="maxLines">Maximum number of lines to include.</param>
        public string GetContentPreview(int maxLines = 10)
        {
            if (string.IsNullOrEmpty(Content))
            {
                return "[Empty snippet]";
            }

            var lines = Content.Split('\n');
            if (lines.Length <= maxLines)
            {
                return Content;
            }

            var previewLines = new StringBuilder();
            for (int i = 0; i < maxLines; i++)
            {
                if (i > 0) previewLines.Append('\n');
                previewLines.Append(lines[i].TrimEnd('\r'));
            }
            previewLines.Append($"\n... ({lines.Length - maxLines} more lines)");
            return previewLines.ToString();
        }

        /// <summary>
        /// Serializes the snippet to a dictionary for storage.
        /// </summary>
        public Godot.Collections.Dictionary ToDictionary()
        {
            return new Godot.Collections.Dictionary
            {
                { "file_path", FilePath },
                { "content", Content },
                { "start_line", StartLine },
                { "end_line", EndLine },
                { "language", Language },
                { "timestamp", Timestamp.ToString("o") }
            };
        }

        /// <summary>
        /// Deserializes a snippet from a dictionary.
        /// </summary>
        public static CodeSnippet FromDictionary(Godot.Collections.Dictionary dict)
        {
            var snippet = new CodeSnippet
            {
                FilePath = dict.ContainsKey("file_path") ? dict["file_path"].AsString() : "",
                Content = dict.ContainsKey("content") ? dict["content"].AsString() : "",
                StartLine = dict.ContainsKey("start_line") ? dict["start_line"].AsInt32() : 1,
                EndLine = dict.ContainsKey("end_line") ? dict["end_line"].AsInt32() : 1,
                Language = dict.ContainsKey("language") ? dict["language"].AsString() : ""
            };

            if (dict.ContainsKey("timestamp"))
            {
                if (DateTime.TryParse(dict["timestamp"].AsString(), out var timestamp))
                {
                    snippet.Timestamp = timestamp;
                }
            }

            return snippet;
        }

        /// <summary>
        /// Determines the programming language from the file extension.
        /// </summary>
        private static string GetLanguageFromFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "";
            }

            var extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
            return extension switch
            {
                "cs" => "csharp",
                "gd" => "gdscript",
                "gdscript" => "gdscript",
                "py" => "python",
                "js" => "javascript",
                "ts" => "typescript",
                "json" => "json",
                "xml" => "xml",
                "html" => "html",
                "css" => "css",
                "yaml" or "yml" => "yaml",
                "md" => "markdown",
                "sh" => "bash",
                "bat" => "batch",
                "ps1" => "powershell",
                "c" => "c",
                "cpp" or "cc" or "cxx" => "cpp",
                "h" or "hpp" => "cpp",
                "java" => "java",
                "rs" => "rust",
                "go" => "go",
                "swift" => "swift",
                "kt" => "kotlin",
                "tres" or "tscn" => "ini",
                _ => extension
            };
        }

        /// <summary>
        /// Creates a snippet from a script resource and selection.
        /// </summary>
        /// <param name="scriptPath">The resource path to the script (e.g., "res://player.gd").</param>
        /// <param name="selectedText">The selected text content.</param>
        /// <param name="startLine">The starting line number (1-based).</param>
        /// <param name="endLine">The ending line number (1-based).</param>
        public static CodeSnippet FromSelection(string scriptPath, string selectedText, int startLine, int endLine)
        {
            // Remove res:// prefix if present
            var filePath = scriptPath;
            if (filePath.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                filePath = filePath.Substring(6);
            }

            return new CodeSnippet(filePath, selectedText, startLine, endLine);
        }
    }
}
#endif

