#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Godot;

namespace G0.Models
{
    /// <summary>
    /// Represents a file reference in a chat message, typically prefixed with '@'.
    /// </summary>
    public class FileReference
    {
        /// <summary>
        /// Maximum file size in bytes that can be read (100KB).
        /// </summary>
        public const int MaxFileSizeBytes = 100 * 1024;

        /// <summary>
        /// The relative file path from the project root.
        /// </summary>
        public string FilePath { get; set; } = "";

        /// <summary>
        /// The content of the file, if successfully read.
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// Whether the file exists and was successfully read.
        /// </summary>
        public bool Exists { get; set; } = false;

        /// <summary>
        /// Optional line range (start line, end line) for partial file reads.
        /// </summary>
        public (int Start, int End)? LineRange { get; set; } = null;

        /// <summary>
        /// Error message if the file could not be read.
        /// </summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary>
        /// File size in bytes, if available.
        /// </summary>
        public long FileSize { get; set; } = 0;

        public FileReference() { }

        public FileReference(string filePath)
        {
            FilePath = filePath;
        }

        /// <summary>
        /// Regex pattern to match @filepath references in text.
        /// Matches: @path/to/file.ext or @"path with spaces/file.ext"
        /// </summary>
        private static readonly Regex FileReferencePattern = new Regex(
            @"@(?:""([^""]+)""|([^\s,;:!?\[\](){}]+\.[a-zA-Z0-9]+))",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses file references from a message string.
        /// </summary>
        /// <param name="message">The message text to parse.</param>
        /// <returns>List of file references found in the message.</returns>
        public static List<FileReference> ParseFileReferences(string message)
        {
            var references = new List<FileReference>();
            
            if (string.IsNullOrEmpty(message))
            {
                return references;
            }

            var matches = FileReferencePattern.Matches(message);
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in matches)
            {
                // Group 1 is for quoted paths, Group 2 is for unquoted paths
                var filePath = !string.IsNullOrEmpty(match.Groups[1].Value) 
                    ? match.Groups[1].Value 
                    : match.Groups[2].Value;

                // Normalize the path
                filePath = NormalizePath(filePath);

                // Skip duplicates
                if (seenPaths.Contains(filePath))
                {
                    continue;
                }
                seenPaths.Add(filePath);

                references.Add(new FileReference(filePath));
            }

            return references;
        }

        /// <summary>
        /// Normalizes a file path to use forward slashes and remove leading slashes.
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            // Replace backslashes with forward slashes
            path = path.Replace('\\', '/');

            // Remove leading slashes
            path = path.TrimStart('/');

            // Remove res:// prefix if present
            if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(6);
            }

            return path;
        }

        /// <summary>
        /// Reads the file content from the project directory.
        /// </summary>
        /// <param name="projectRoot">The absolute path to the project root directory.</param>
        /// <returns>True if the file was successfully read, false otherwise.</returns>
        public bool ReadFileContent(string projectRoot)
        {
            try
            {
                // Construct the full path
                var fullPath = Path.Combine(projectRoot, FilePath);
                fullPath = Path.GetFullPath(fullPath);

                // Security check: ensure the path is within the project directory
                var normalizedProjectRoot = Path.GetFullPath(projectRoot);
                if (!fullPath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "Access denied: Path is outside the project directory.";
                    Exists = false;
                    return false;
                }

                // Check if file exists
                if (!File.Exists(fullPath))
                {
                    ErrorMessage = $"File not found: {FilePath}";
                    Exists = false;
                    return false;
                }

                // Check file size
                var fileInfo = new FileInfo(fullPath);
                FileSize = fileInfo.Length;

                if (FileSize > MaxFileSizeBytes)
                {
                    ErrorMessage = $"File too large: {FilePath} ({FileSize / 1024}KB exceeds {MaxFileSizeBytes / 1024}KB limit)";
                    Exists = true;
                    return false;
                }

                // Check if it's a binary file
                if (IsBinaryFile(fullPath))
                {
                    ErrorMessage = $"Binary file: {FilePath}";
                    Content = "[Binary file - content not displayed]";
                    Exists = true;
                    return false;
                }

                // Read the file content
                Content = File.ReadAllText(fullPath, Encoding.UTF8);

                // Apply line range if specified
                if (LineRange.HasValue)
                {
                    Content = ExtractLineRange(Content, LineRange.Value.Start, LineRange.Value.End);
                }

                Exists = true;
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                ErrorMessage = $"Access denied: {FilePath}";
                Exists = false;
                return false;
            }
            catch (IOException ex)
            {
                ErrorMessage = $"IO error reading {FilePath}: {ex.Message}";
                Exists = false;
                return false;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error reading {FilePath}: {ex.Message}";
                Exists = false;
                return false;
            }
        }

        /// <summary>
        /// Checks if a file appears to be binary by examining its first bytes.
        /// </summary>
        private static bool IsBinaryFile(string filePath)
        {
            try
            {
                // Check file extension first (quick check)
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var textExtensions = new HashSet<string>
                {
                    ".cs", ".gd", ".gdscript", ".txt", ".md", ".json", ".xml", ".yaml", ".yml",
                    ".toml", ".cfg", ".ini", ".tres", ".tscn", ".godot", ".import", ".html",
                    ".css", ".js", ".ts", ".py", ".sh", ".bat", ".ps1", ".c", ".cpp", ".h",
                    ".hpp", ".java", ".rs", ".go", ".swift", ".kt", ".gradle", ".properties",
                    ".gitignore", ".gitattributes", ".editorconfig", ".csproj", ".sln"
                };

                if (textExtensions.Contains(extension))
                {
                    return false;
                }

                // Read first 8KB and check for null bytes
                const int sampleSize = 8192;
                using var stream = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read);
                var buffer = new byte[Math.Min(sampleSize, stream.Length)];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < bytesRead; i++)
                {
                    // Null byte typically indicates binary
                    if (buffer[i] == 0)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                // If we can't read the file, assume it's not binary
                return false;
            }
        }

        /// <summary>
        /// Extracts a range of lines from the content.
        /// </summary>
        private static string ExtractLineRange(string content, int startLine, int endLine)
        {
            var lines = content.Split('\n');
            var start = Math.Max(0, startLine - 1); // Convert to 0-based
            var end = Math.Min(lines.Length, endLine);

            if (start >= lines.Length)
            {
                return "";
            }

            var selectedLines = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                if (selectedLines.Length > 0)
                {
                    selectedLines.Append('\n');
                }
                selectedLines.Append(lines[i]);
            }

            return selectedLines.ToString();
        }

        /// <summary>
        /// Formats the file content for inclusion in a chat message context.
        /// </summary>
        /// <returns>Formatted string with file path and content.</returns>
        public string ToFormattedContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"File: @{FilePath}");

            if (!Exists)
            {
                sb.AppendLine($"[Error: {ErrorMessage}]");
                return sb.ToString();
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                sb.AppendLine($"[Warning: {ErrorMessage}]");
            }

            // Determine language for syntax highlighting
            var extension = Path.GetExtension(FilePath).TrimStart('.').ToLowerInvariant();
            var language = GetLanguageFromExtension(extension);

            sb.AppendLine($"```{language}");
            sb.AppendLine(Content);
            sb.AppendLine("```");

            return sb.ToString();
        }

        /// <summary>
        /// Gets the language identifier for syntax highlighting based on file extension.
        /// </summary>
        private static string GetLanguageFromExtension(string extension)
        {
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
                _ => ""
            };
        }

        /// <summary>
        /// Serializes the file reference to a dictionary for storage.
        /// </summary>
        public Godot.Collections.Dictionary ToDictionary()
        {
            var dict = new Godot.Collections.Dictionary
            {
                { "file_path", FilePath },
                { "exists", Exists },
                { "error_message", ErrorMessage },
                { "file_size", FileSize }
            };

            if (LineRange.HasValue)
            {
                dict["line_range_start"] = LineRange.Value.Start;
                dict["line_range_end"] = LineRange.Value.End;
            }

            // Note: We don't store Content to avoid bloating saved data
            return dict;
        }

        /// <summary>
        /// Deserializes a file reference from a dictionary.
        /// </summary>
        public static FileReference FromDictionary(Godot.Collections.Dictionary dict)
        {
            var reference = new FileReference
            {
                FilePath = dict.ContainsKey("file_path") ? dict["file_path"].AsString() : "",
                Exists = dict.ContainsKey("exists") && dict["exists"].AsBool(),
                ErrorMessage = dict.ContainsKey("error_message") ? dict["error_message"].AsString() : "",
                FileSize = dict.ContainsKey("file_size") ? dict["file_size"].AsInt64() : 0
            };

            if (dict.ContainsKey("line_range_start") && dict.ContainsKey("line_range_end"))
            {
                reference.LineRange = (
                    dict["line_range_start"].AsInt32(),
                    dict["line_range_end"].AsInt32()
                );
            }

            return reference;
        }

        /// <summary>
        /// Removes file reference patterns from a message, returning clean text.
        /// </summary>
        /// <param name="message">The message containing file references.</param>
        /// <returns>The message with file reference patterns removed.</returns>
        public static string RemoveFileReferences(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            return FileReferencePattern.Replace(message, "").Trim();
        }
    }
}
#endif

