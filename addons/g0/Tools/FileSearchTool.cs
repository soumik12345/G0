#if TOOLS
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Microsoft.Extensions.AI;

namespace G0.Tools
{
    /// <summary>
    /// A tool that can be called by AI agents to search files in the project using ripgrep.
    /// Implements the Microsoft.Extensions.AI tool pattern for agent integration.
    /// </summary>
    public class FileSearchTool
    {
        private readonly string _projectRoot;
        private readonly string _pluginPath;
        private static string _cachedRipgrepPath;
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// Maximum number of search results to return by default.
        /// </summary>
        private const int DefaultMaxResults = 50;

        /// <summary>
        /// Timeout for ripgrep process execution in milliseconds.
        /// </summary>
        private const int ProcessTimeoutMs = 30000;

        public FileSearchTool(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new ArgumentNullException(nameof(projectRoot), "Project root path is required");
            }
            _projectRoot = projectRoot;
            _pluginPath = Path.Combine(projectRoot, "addons", "g0");
        }

        /// <summary>
        /// Searches for files and content matching the specified pattern in the project.
        /// Use this tool to find code patterns, search for specific text, or locate files containing certain content.
        /// </summary>
        /// <param name="pattern">The search pattern (supports regex when useRegex is true)</param>
        /// <param name="fileTypes">Comma-separated file extensions to include (e.g., "cs,gd,json"). Leave empty for all files.</param>
        /// <param name="excludePatterns">Comma-separated glob patterns to exclude (e.g., ".godot,*.import")</param>
        /// <param name="maxResults">Maximum number of matches to return (default: 50)</param>
        /// <param name="contextLines">Number of context lines before and after each match (default: 2)</param>
        /// <param name="caseSensitive">Whether the search is case sensitive (default: false)</param>
        /// <param name="useRegex">Whether to interpret the pattern as a regex (default: true)</param>
        /// <param name="wholeWord">Whether to match whole words only (default: false)</param>
        /// <returns>Formatted search results grouped by file with context</returns>
        [Description("Searches for files and content matching a pattern in the project. Use this to find code patterns, search for specific text, locate usages of functions/classes, or find files containing certain content. Supports regex patterns, file type filtering, and context lines.")]
        public async Task<string> SearchFiles(
            [Description("The search pattern to find (supports regex). Examples: 'Node2D', 'func _ready', 'class.*Controller'")] string pattern,
            [Description("Comma-separated file extensions to search (e.g., 'cs,gd,json'). Leave empty for all text files.")] string fileTypes = "",
            [Description("Comma-separated glob patterns to exclude (e.g., '.godot,*.import,bin')")] string excludePatterns = "",
            [Description("Maximum number of matches to return")] int maxResults = 50,
            [Description("Number of context lines before and after each match")] int contextLines = 2,
            [Description("Whether the search is case sensitive")] bool caseSensitive = false,
            [Description("Whether to interpret the pattern as a regex")] bool useRegex = true,
            [Description("Whether to match whole words only")] bool wholeWord = false)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return "Error: Please provide a search pattern.";
            }

            try
            {
                // Validate and clamp parameters
                maxResults = Math.Clamp(maxResults, 1, 500);
                contextLines = Math.Clamp(contextLines, 0, 10);

                // Get ripgrep binary path
                var rgPath = GetRipgrepPath();
                if (string.IsNullOrEmpty(rgPath))
                {
                    return "Error: Could not find or extract ripgrep binary. Please ensure the plugin is properly installed.";
                }

                // Build ripgrep arguments
                var args = BuildRipgrepArguments(
                    pattern, fileTypes, excludePatterns, maxResults,
                    contextLines, caseSensitive, useRegex, wholeWord);

                // Execute ripgrep
                var result = await ExecuteRipgrepAsync(rgPath, args);

                if (result.ExitCode != 0 && result.ExitCode != 1)
                {
                    // Exit code 1 means no matches, which is fine
                    if (!string.IsNullOrEmpty(result.StdErr))
                    {
                        GD.PrintErr($"G0: Ripgrep error: {result.StdErr}");
                        return $"Error: Search failed: {result.StdErr}";
                    }
                }

                // Parse and format results
                return FormatSearchResults(result.StdOut, pattern, maxResults);
            }
            catch (TimeoutException)
            {
                return "Error: Search timed out after 30 seconds. Try narrowing your search with more specific patterns or file type filters.";
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: FileSearchTool error: {ex.Message}");
                return $"Error: Search failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Lists files matching a glob pattern in the project.
        /// Use this to find files by name or extension without searching their content.
        /// </summary>
        /// <param name="globPattern">Glob pattern to match files (e.g., "*.cs", "**/*Controller*")</param>
        /// <param name="maxFiles">Maximum number of files to return (default: 50)</param>
        /// <returns>List of matching file paths</returns>
        [Description("Lists files matching a glob pattern in the project. Use this to find files by name or extension without searching content. Examples: '*.cs' for all C# files, '*Controller*' for files with Controller in the name.")]
        public async Task<string> FindFiles(
            [Description("Glob pattern to match files (e.g., '*.cs', '**/*Controller*', '*.gd')")] string globPattern,
            [Description("Maximum number of files to return")] int maxFiles = 50)
        {
            if (string.IsNullOrWhiteSpace(globPattern))
            {
                return "Error: Please provide a glob pattern.";
            }

            try
            {
                maxFiles = Math.Clamp(maxFiles, 1, 200);

                var rgPath = GetRipgrepPath();
                if (string.IsNullOrEmpty(rgPath))
                {
                    return "Error: Could not find or extract ripgrep binary.";
                }

                // Use ripgrep's --files mode with glob
                var args = new StringBuilder();
                args.Append("--files ");
                args.Append($"--glob \"{globPattern}\" ");
                args.Append("--hidden "); // Include hidden files
                args.Append($"--glob \"!.git\" "); // Exclude .git
                args.Append($"--glob \"!.godot\" "); // Exclude .godot
                args.Append($"--glob \"!*.import\" "); // Exclude import files
                args.Append($"-- \"{_projectRoot}\"");

                var result = await ExecuteRipgrepAsync(rgPath, args.ToString());

                var files = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                if (files.Length == 0)
                {
                    return $"No files found matching pattern: \"{globPattern}\"";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"📁 Found {Math.Min(files.Length, maxFiles)} file(s) matching \"{globPattern}\":");
                sb.AppendLine();

                var count = 0;
                foreach (var file in files)
                {
                    if (count >= maxFiles) break;
                    
                    var relativePath = GetRelativePath(file.Trim());
                    var fileInfo = new FileInfo(file.Trim());
                    var size = fileInfo.Exists 
                        ? (fileInfo.Length < 1024 ? $"{fileInfo.Length}B" : $"{fileInfo.Length / 1024}KB")
                        : "";
                    
                    sb.AppendLine($"  📄 {relativePath} {(string.IsNullOrEmpty(size) ? "" : $"({size})")}");
                    count++;
                }

                if (files.Length > maxFiles)
                {
                    sb.AppendLine();
                    sb.AppendLine($"... and {files.Length - maxFiles} more files");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: FindFiles error: {ex.Message}");
                return $"Error: Failed to find files: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the path to the ripgrep binary, extracting it if necessary.
        /// </summary>
        private string GetRipgrepPath()
        {
            lock (_cacheLock)
            {
                if (!string.IsNullOrEmpty(_cachedRipgrepPath) && File.Exists(_cachedRipgrepPath))
                {
                    return _cachedRipgrepPath;
                }

                // Determine platform-specific binary name
                string binaryName;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    binaryName = "rg.exe";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // Check architecture for macOS
                    binaryName = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 
                        ? "rg_macos_arm64" 
                        : "rg_macos_x64";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    binaryName = "rg_linux";
                }
                else
                {
                    GD.PrintErr("G0: Unsupported platform for ripgrep");
                    return null;
                }

                // Look for the binary in the plugin's Binaries folder
                var binaryPath = Path.Combine(_pluginPath, "Binaries", binaryName);
                
                if (File.Exists(binaryPath))
                {
                    // Ensure executable permission on Unix systems
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        try
                        {
                            var process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "chmod",
                                    Arguments = $"+x \"{binaryPath}\"",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                }
                            };
                            process.Start();
                            process.WaitForExit(5000);
                        }
                        catch (Exception ex)
                        {
                            GD.PrintErr($"G0: Failed to set executable permission: {ex.Message}");
                        }
                    }

                    _cachedRipgrepPath = binaryPath;
                    return binaryPath;
                }

                // Try to find ripgrep in PATH as fallback
                var pathRg = FindInPath("rg");
                if (!string.IsNullOrEmpty(pathRg))
                {
                    _cachedRipgrepPath = pathRg;
                    return pathRg;
                }

                GD.PrintErr($"G0: Ripgrep binary not found at {binaryPath}");
                return null;
            }
        }

        /// <summary>
        /// Finds an executable in the system PATH.
        /// </summary>
        private static string FindInPath(string executable)
        {
            var pathEnv = System.Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
                return null;

            var paths = pathEnv.Split(Path.PathSeparator);
            var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[] { ".exe", ".cmd", ".bat", "" }
                : new[] { "" };

            foreach (var path in paths)
            {
                foreach (var ext in extensions)
                {
                    var fullPath = Path.Combine(path, executable + ext);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Builds ripgrep command line arguments.
        /// </summary>
        private string BuildRipgrepArguments(
            string pattern, string fileTypes, string excludePatterns,
            int maxResults, int contextLines, bool caseSensitive,
            bool useRegex, bool wholeWord)
        {
            var args = new StringBuilder();

            // Output format - use line-based output for easier parsing
            args.Append("--line-number ");
            args.Append("--with-filename ");
            args.Append("--heading ");
            
            // Context lines
            if (contextLines > 0)
            {
                args.Append($"--context {contextLines} ");
            }

            // Case sensitivity
            if (!caseSensitive)
            {
                args.Append("--ignore-case ");
            }

            // Regex mode
            if (!useRegex)
            {
                args.Append("--fixed-strings ");
            }

            // Whole word matching
            if (wholeWord)
            {
                args.Append("--word-regexp ");
            }

            // Max results (using max-count per file and limiting total output)
            args.Append($"--max-count 50 "); // Limit matches per file
            
            // File type filtering
            if (!string.IsNullOrWhiteSpace(fileTypes))
            {
                var types = fileTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var type in types)
                {
                    var trimmed = type.Trim().TrimStart('.');
                    args.Append($"--glob \"*.{trimmed}\" ");
                }
            }

            // Exclude patterns - always exclude some defaults
            args.Append("--glob \"!.git/**\" ");
            args.Append("--glob \"!.godot/**\" ");
            args.Append("--glob \"!*.import\" ");
            args.Append("--glob \"!*.uid\" ");

            if (!string.IsNullOrWhiteSpace(excludePatterns))
            {
                var excludes = excludePatterns.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var exclude in excludes)
                {
                    var trimmed = exclude.Trim();
                    if (!trimmed.StartsWith("!"))
                    {
                        trimmed = "!" + trimmed;
                    }
                    args.Append($"--glob \"{trimmed}\" ");
                }
            }

            // Pattern and path
            args.Append($"-- \"{EscapePattern(pattern)}\" \"{_projectRoot}\"");

            return args.ToString();
        }

        /// <summary>
        /// Escapes special characters in the pattern for shell safety.
        /// </summary>
        private static string EscapePattern(string pattern)
        {
            // Escape double quotes for shell
            return pattern.Replace("\"", "\\\"");
        }

        /// <summary>
        /// Executes ripgrep and returns the result.
        /// </summary>
        private async Task<ProcessResult> ExecuteRipgrepAsync(string rgPath, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = rgPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _projectRoot
            };

            using var process = new Process { StartInfo = psi };
            var stdOutBuilder = new StringBuilder();
            var stdErrBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    stdOutBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    stdErrBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(ProcessTimeoutMs);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(true);
                }
                catch { }
                throw new TimeoutException("Ripgrep process timed out");
            }

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                StdOut = stdOutBuilder.ToString(),
                StdErr = stdErrBuilder.ToString()
            };
        }

        /// <summary>
        /// Formats ripgrep output into a readable format.
        /// </summary>
        private string FormatSearchResults(string output, string pattern, int maxResults)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return $"No matches found for pattern: \"{pattern}\"";
            }

            var sb = new StringBuilder();
            var lines = output.Split('\n');
            var currentFile = "";
            var fileMatches = new Dictionary<string, List<string>>();
            var totalMatches = 0;
            var currentFileLines = new List<string>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Blank line separates files in --heading mode
                    if (!string.IsNullOrEmpty(currentFile) && currentFileLines.Count > 0)
                    {
                        fileMatches[currentFile] = new List<string>(currentFileLines);
                        currentFileLines.Clear();
                    }
                    currentFile = "";
                    continue;
                }

                // Check if this is a filename (no leading number or special chars for context)
                if (!char.IsDigit(line[0]) && !line.StartsWith("-") && !line.Contains(":"))
                {
                    // This is a file path
                    if (!string.IsNullOrEmpty(currentFile) && currentFileLines.Count > 0)
                    {
                        fileMatches[currentFile] = new List<string>(currentFileLines);
                        currentFileLines.Clear();
                    }
                    currentFile = line.Trim();
                }
                else
                {
                    // This is a match line or context line
                    currentFileLines.Add(line);
                    if (line.Contains(":") && char.IsDigit(line[0]))
                    {
                        totalMatches++;
                    }
                }
            }

            // Don't forget the last file
            if (!string.IsNullOrEmpty(currentFile) && currentFileLines.Count > 0)
            {
                fileMatches[currentFile] = new List<string>(currentFileLines);
            }

            if (fileMatches.Count == 0)
            {
                return $"No matches found for pattern: \"{pattern}\"";
            }

            // Build formatted output
            var displayedMatches = 0;
            sb.AppendLine($"🔍 Found {totalMatches} match(es) in {fileMatches.Count} file(s) for \"{pattern}\":");
            sb.AppendLine();

            foreach (var kvp in fileMatches)
            {
                if (displayedMatches >= maxResults)
                {
                    sb.AppendLine($"... truncated (showing first {maxResults} results)");
                    break;
                }

                var filePath = kvp.Key;
                var matchLines = kvp.Value;
                var relativePath = GetRelativePath(filePath);
                var language = GetLanguageFromExtension(Path.GetExtension(filePath));

                sb.AppendLine($"📄 **{relativePath}**");
                sb.AppendLine($"```{language}");

                foreach (var matchLine in matchLines)
                {
                    sb.AppendLine(matchLine);
                    if (matchLine.Contains(":") && char.IsDigit(matchLine[0]))
                    {
                        displayedMatches++;
                    }
                }

                sb.AppendLine("```");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a relative path from the project root.
        /// </summary>
        private string GetRelativePath(string fullPath)
        {
            try
            {
                if (fullPath.StartsWith(_projectRoot))
                {
                    return fullPath.Substring(_projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, '/');
                }
                return Path.GetRelativePath(_projectRoot, fullPath);
            }
            catch
            {
                return fullPath;
            }
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
                "cfg" or "godot" => "ini",
                _ => ""
            };
        }

        /// <summary>
        /// Creates the AI function definitions for this tool.
        /// </summary>
        public static IEnumerable<AIFunction> CreateAIFunctions(string projectRoot)
        {
            var tool = new FileSearchTool(projectRoot);

            yield return AIFunctionFactory.Create(
                tool.SearchFiles,
                "search_files",
                "Searches for files and content matching a pattern in the project. Use this to find code patterns, search for specific text, locate usages of functions/classes, or find files containing certain content. Supports regex patterns, file type filtering, and context lines. Examples: search for 'Node2D' to find all usages, 'func _ready' to find ready functions, or 'class.*Controller' for controller classes.");

            yield return AIFunctionFactory.Create(
                tool.FindFiles,
                "find_files",
                "Lists files matching a glob pattern in the project. Use this to find files by name or extension without searching content. Examples: '*.cs' for all C# files, '*Controller*' for files with Controller in the name, '*.gd' for all GDScript files.");
        }

        /// <summary>
        /// Result from executing a process.
        /// </summary>
        private class ProcessResult
        {
            public int ExitCode { get; set; }
            public string StdOut { get; set; }
            public string StdErr { get; set; }
        }
    }
}
#endif

