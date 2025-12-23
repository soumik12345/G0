#if TOOLS
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Godot;
using Microsoft.Extensions.AI;
using G0.Models;

namespace G0.Tools
{
    /// <summary>
    /// A tool that can be called by AI agents to read files from the project directory.
    /// Implements the Microsoft.Extensions.AI tool pattern for agent integration.
    /// </summary>
    public class ReadFileTool
    {
        private readonly string _projectRoot;

        /// <summary>
        /// Maximum file size in bytes that can be read (100KB).
        /// </summary>
        private const int MaxFileSizeBytes = FileReference.MaxFileSizeBytes;

        public ReadFileTool(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new ArgumentNullException(nameof(projectRoot), "Project root path is required");
            }
            _projectRoot = projectRoot;
        }

        /// <summary>
        /// Reads the contents of a file from the project directory.
        /// Use this tool when you need to examine code, configuration, or other text files in the user's project.
        /// </summary>
        /// <param name="filePath">The relative path to the file from the project root (e.g., "addons/g0/ChatPanel.cs")</param>
        /// <returns>The file contents with line numbers, or an error message if the file cannot be read</returns>
        [Description("Reads the contents of a file from the project directory. Use this tool when you need to examine code, configuration, or other text files in the user's project to answer questions or provide assistance.")]
        public string ReadFile(
            [Description("The relative path to the file from the project root (e.g., 'addons/g0/ChatPanel.cs')")] string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "Error: Please provide a file path.";
            }

            try
            {
                // Normalize the path
                filePath = NormalizePath(filePath);

                // Construct the full path
                var fullPath = Path.Combine(_projectRoot, filePath);
                fullPath = Path.GetFullPath(fullPath);

                // Security check: ensure the path is within the project directory
                var normalizedProjectRoot = Path.GetFullPath(_projectRoot);
                if (!fullPath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Error: Access denied - path '{filePath}' is outside the project directory.";
                }

                // Check if file exists
                if (!File.Exists(fullPath))
                {
                    // Try to suggest similar files
                    var suggestions = FindSimilarFiles(filePath);
                    if (suggestions.Count > 0)
                    {
                        var suggestionList = string.Join("\n  - ", suggestions);
                        return $"Error: File not found: '{filePath}'\n\nDid you mean one of these?\n  - {suggestionList}";
                    }
                    return $"Error: File not found: '{filePath}'";
                }

                // Check file size
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MaxFileSizeBytes)
                {
                    return $"Error: File too large: '{filePath}' ({fileInfo.Length / 1024}KB exceeds {MaxFileSizeBytes / 1024}KB limit).\n\nTry reading a specific portion of the file or ask the user to reference specific sections.";
                }

                // Check if it's a binary file
                if (IsBinaryFile(fullPath))
                {
                    return $"Error: Cannot read binary file: '{filePath}'.\n\nThis appears to be a binary file (e.g., image, compiled code). Only text files can be read.";
                }

                // Read the file
                var content = File.ReadAllText(fullPath, Encoding.UTF8);
                var lines = content.Split('\n');

                // Format output with line numbers
                var sb = new StringBuilder();
                sb.AppendLine($"📄 **File: {filePath}**");
                sb.AppendLine($"Lines: {lines.Length} | Size: {fileInfo.Length} bytes");
                sb.AppendLine();
                sb.AppendLine("```" + GetLanguageFromExtension(Path.GetExtension(filePath)));

                for (int i = 0; i < lines.Length; i++)
                {
                    // Add line number prefix
                    sb.AppendLine($"{(i + 1).ToString().PadLeft(4)} | {lines[i]}");
                }

                sb.AppendLine("```");

                return sb.ToString();
            }
            catch (UnauthorizedAccessException)
            {
                return $"Error: Access denied to file: '{filePath}'";
            }
            catch (IOException ex)
            {
                return $"Error: Could not read file '{filePath}': {ex.Message}";
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: ReadFileTool error: {ex.Message}");
                return $"Error: Failed to read file '{filePath}': {ex.Message}";
            }
        }

        /// <summary>
        /// Lists files in the project directory, optionally filtered by a search pattern.
        /// Use this to discover what files are available in the project.
        /// </summary>
        /// <param name="directory">The directory to list (relative to project root, empty for root)</param>
        /// <param name="searchPattern">Optional search pattern to filter files (e.g., "*.cs", "*.gd")</param>
        /// <returns>A list of files in the specified directory</returns>
        [Description("Lists files in a project directory to discover available files. Use this when you need to explore the project structure or find specific files.")]
        public string ListFiles(
            [Description("The directory to list, relative to project root (empty string for root directory)")] string directory = "",
            [Description("Optional file pattern to filter results (e.g., '*.cs', '*.gd'). Leave empty for all files.")] string searchPattern = "")
        {
            try
            {
                // Normalize the path
                directory = NormalizePath(directory ?? "");

                // Construct the full path
                var fullPath = string.IsNullOrEmpty(directory) 
                    ? _projectRoot 
                    : Path.Combine(_projectRoot, directory);
                fullPath = Path.GetFullPath(fullPath);

                // Security check
                var normalizedProjectRoot = Path.GetFullPath(_projectRoot);
                if (!fullPath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return $"Error: Access denied - path '{directory}' is outside the project directory.";
                }

                if (!Directory.Exists(fullPath))
                {
                    return $"Error: Directory not found: '{directory}'";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"📁 **Directory: {(string.IsNullOrEmpty(directory) ? "/" : directory)}**");
                sb.AppendLine();

                // List subdirectories
                var subdirs = Directory.GetDirectories(fullPath);
                var visibleDirs = new List<string>();
                foreach (var dir in subdirs)
                {
                    var dirName = Path.GetFileName(dir);
                    // Skip hidden and excluded directories
                    if (!dirName.StartsWith(".") && !IsExcludedDirectory(dirName))
                    {
                        visibleDirs.Add(dirName);
                    }
                }

                if (visibleDirs.Count > 0)
                {
                    sb.AppendLine("**Directories:**");
                    foreach (var dir in visibleDirs)
                    {
                        sb.AppendLine($"  📁 {dir}/");
                    }
                    sb.AppendLine();
                }

                // List files
                var pattern = string.IsNullOrEmpty(searchPattern) ? "*" : searchPattern;
                var files = Directory.GetFiles(fullPath, pattern);
                var visibleFiles = new List<string>();
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    if (!fileName.StartsWith("."))
                    {
                        visibleFiles.Add(fileName);
                    }
                }

                if (visibleFiles.Count > 0)
                {
                    sb.AppendLine("**Files:**");
                    foreach (var file in visibleFiles)
                    {
                        var fileInfo = new FileInfo(Path.Combine(fullPath, file));
                        var size = fileInfo.Length < 1024 
                            ? $"{fileInfo.Length}B" 
                            : $"{fileInfo.Length / 1024}KB";
                        sb.AppendLine($"  📄 {file} ({size})");
                    }
                }
                else if (visibleDirs.Count == 0)
                {
                    sb.AppendLine("(Empty directory)");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: ListFiles error: {ex.Message}");
                return $"Error: Failed to list directory: {ex.Message}";
            }
        }

        /// <summary>
        /// Normalizes a file path.
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

            // Remove @ prefix if present (from file references)
            if (path.StartsWith("@"))
            {
                path = path.Substring(1);
            }

            return path;
        }

        /// <summary>
        /// Checks if a file appears to be binary.
        /// </summary>
        private static bool IsBinaryFile(string filePath)
        {
            // Check file extension first
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

            // Read first bytes and check for null bytes
            try
            {
                const int sampleSize = 8192;
                using var stream = new FileStream(filePath, FileMode.Open, System.IO.FileAccess.Read);
                var buffer = new byte[Math.Min(sampleSize, stream.Length)];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a directory should be excluded from listing.
        /// </summary>
        private static bool IsExcludedDirectory(string dirName)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git", ".godot", ".mono", ".vs", ".vscode", ".idea",
                "bin", "obj", "node_modules", "__pycache__"
            };
            return excluded.Contains(dirName);
        }

        /// <summary>
        /// Finds files with similar names to suggest alternatives.
        /// </summary>
        private List<string> FindSimilarFiles(string filePath)
        {
            var suggestions = new List<string>();
            var fileName = Path.GetFileName(filePath);

            try
            {
                var files = FileDiscovery.Instance.SearchFiles(fileName, 5);
                foreach (var file in files)
                {
                    if (!file.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        suggestions.Add(file);
                    }
                }
            }
            catch
            {
                // Ignore errors in suggestion generation
            }

            return suggestions;
        }

        /// <summary>
        /// Gets the language identifier for syntax highlighting.
        /// </summary>
        private static string GetLanguageFromExtension(string extension)
        {
            extension = extension.TrimStart('.').ToLowerInvariant();
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
        /// Creates the AI function definitions for this tool.
        /// </summary>
        public static IEnumerable<AIFunction> CreateAIFunctions(string projectRoot)
        {
            var tool = new ReadFileTool(projectRoot);

            yield return AIFunctionFactory.Create(
                tool.ReadFile,
                "read_file",
                "Reads the contents of a file from the project directory. Use this tool when you need to examine code, configuration, or other text files in the user's project to answer questions or provide assistance.");

            yield return AIFunctionFactory.Create(
                tool.ListFiles,
                "list_files",
                "Lists files in a project directory to discover available files. Use this when you need to explore the project structure or find specific files.");
        }
    }
}
#endif

