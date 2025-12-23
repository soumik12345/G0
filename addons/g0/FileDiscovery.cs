#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace G0
{
    /// <summary>
    /// Discovers and caches project files for autocomplete functionality.
    /// </summary>
    public class FileDiscovery
    {
        /// <summary>
        /// Cached list of project file paths (relative to project root).
        /// </summary>
        private List<string> _cachedFiles = new List<string>();

        /// <summary>
        /// The project root directory (absolute path).
        /// </summary>
        private string _projectRoot = "";

        /// <summary>
        /// Timestamp of the last cache refresh.
        /// </summary>
        private DateTime _lastRefresh = DateTime.MinValue;

        /// <summary>
        /// Minimum time between cache refreshes (in seconds).
        /// </summary>
        private const int CacheRefreshIntervalSeconds = 30;

        /// <summary>
        /// Directories to exclude from file discovery.
        /// </summary>
        private static readonly HashSet<string> ExcludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".godot",
            ".mono",
            ".vs",
            ".vscode",
            ".idea",
            "bin",
            "obj",
            "node_modules",
            "__pycache__",
            ".import",
            "android",
            "ios",
            "export_presets"
        };

        /// <summary>
        /// File extensions to prioritize in search results.
        /// </summary>
        private static readonly HashSet<string> PriorityExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".gd",
            ".gdscript",
            ".tscn",
            ".tres",
            ".cfg",
            ".godot"
        };

        /// <summary>
        /// All allowed file extensions for discovery.
        /// </summary>
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Godot
            ".cs", ".gd", ".gdscript", ".tscn", ".tres", ".cfg", ".godot", ".import",
            // Common code
            ".py", ".js", ".ts", ".json", ".xml", ".yaml", ".yml", ".toml",
            // Web
            ".html", ".css", ".scss", ".sass", ".less",
            // Documentation
            ".md", ".txt", ".rst",
            // Shell
            ".sh", ".bat", ".ps1",
            // C/C++
            ".c", ".cpp", ".cc", ".cxx", ".h", ".hpp",
            // Other languages
            ".java", ".rs", ".go", ".swift", ".kt", ".rb",
            // Config
            ".ini", ".env", ".gitignore", ".editorconfig",
            // Data
            ".csv", ".sql",
            // Project files
            ".csproj", ".sln", ".gradle", ".properties"
        };

        /// <summary>
        /// Maximum number of files to cache.
        /// </summary>
        private const int MaxCachedFiles = 5000;

        /// <summary>
        /// Singleton instance for global access.
        /// </summary>
        private static FileDiscovery _instance;
        public static FileDiscovery Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FileDiscovery();
                }
                return _instance;
            }
        }

        public FileDiscovery()
        {
            _projectRoot = ProjectSettings.GlobalizePath("res://");
        }

        /// <summary>
        /// Gets all project files, using cache if available.
        /// </summary>
        /// <param name="forceRefresh">Force a cache refresh.</param>
        /// <returns>List of relative file paths from project root.</returns>
        public List<string> GetProjectFiles(bool forceRefresh = false)
        {
            var now = DateTime.Now;
            var timeSinceRefresh = (now - _lastRefresh).TotalSeconds;

            if (forceRefresh || _cachedFiles.Count == 0 || timeSinceRefresh > CacheRefreshIntervalSeconds)
            {
                RefreshFileCache();
            }

            return _cachedFiles;
        }

        /// <summary>
        /// Refreshes the file cache by scanning the project directory.
        /// </summary>
        public void RefreshFileCache()
        {
            try
            {
                _cachedFiles.Clear();

                if (string.IsNullOrEmpty(_projectRoot) || !Directory.Exists(_projectRoot))
                {
                    GD.PrintErr("G0: Project root not found for file discovery");
                    return;
                }

                ScanDirectory(_projectRoot, "");

                // Sort files: priority extensions first, then alphabetically
                _cachedFiles = _cachedFiles
                    .OrderByDescending(f => PriorityExtensions.Contains(Path.GetExtension(f)))
                    .ThenBy(f => f)
                    .Take(MaxCachedFiles)
                    .ToList();

                _lastRefresh = DateTime.Now;
                GD.Print($"G0: File discovery cached {_cachedFiles.Count} files");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Error during file discovery: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively scans a directory for files.
        /// </summary>
        private void ScanDirectory(string absolutePath, string relativePath)
        {
            try
            {
                // Stop if we've hit the limit
                if (_cachedFiles.Count >= MaxCachedFiles)
                {
                    return;
                }

                // Get all files in the current directory
                foreach (var file in Directory.GetFiles(absolutePath))
                {
                    if (_cachedFiles.Count >= MaxCachedFiles)
                    {
                        break;
                    }

                    var fileName = Path.GetFileName(file);
                    var extension = Path.GetExtension(file);

                    // Skip hidden files (except config files like .gitignore)
                    if (fileName.StartsWith(".") && !AllowedExtensions.Contains(fileName))
                    {
                        continue;
                    }

                    // Check if extension is allowed
                    if (!AllowedExtensions.Contains(extension) && 
                        !AllowedExtensions.Contains(fileName)) // For files like .gitignore
                    {
                        continue;
                    }

                    var relativeFilePath = string.IsNullOrEmpty(relativePath)
                        ? fileName
                        : $"{relativePath}/{fileName}";

                    _cachedFiles.Add(relativeFilePath);
                }

                // Recursively scan subdirectories
                foreach (var dir in Directory.GetDirectories(absolutePath))
                {
                    if (_cachedFiles.Count >= MaxCachedFiles)
                    {
                        break;
                    }

                    var dirName = Path.GetFileName(dir);

                    // Skip excluded directories
                    if (ExcludedDirectories.Contains(dirName))
                    {
                        continue;
                    }

                    // Skip hidden directories
                    if (dirName.StartsWith("."))
                    {
                        continue;
                    }

                    var relativeSubPath = string.IsNullOrEmpty(relativePath)
                        ? dirName
                        : $"{relativePath}/{dirName}";

                    ScanDirectory(dir, relativeSubPath);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Error scanning directory {absolutePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Searches for files matching a query using fuzzy matching.
        /// </summary>
        /// <param name="query">The search query.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <returns>List of matching file paths, sorted by relevance.</returns>
        public List<string> SearchFiles(string query, int maxResults = 10)
        {
            var files = GetProjectFiles();

            if (string.IsNullOrWhiteSpace(query))
            {
                // Return most recently used or priority files when query is empty
                return files.Take(maxResults).ToList();
            }

            // Normalize query
            query = query.ToLowerInvariant().Trim();

            // Score and rank files
            var scoredFiles = files
                .Select(f => new { Path = f, Score = CalculateMatchScore(f, query) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Path.Length) // Prefer shorter paths
                .Take(maxResults)
                .Select(x => x.Path)
                .ToList();

            return scoredFiles;
        }

        /// <summary>
        /// Calculates a match score for a file path against a query.
        /// Higher scores indicate better matches.
        /// </summary>
        private int CalculateMatchScore(string filePath, string query)
        {
            var lowerPath = filePath.ToLowerInvariant();
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();

            int score = 0;

            // Exact filename match (highest priority)
            if (fileNameWithoutExt == query)
            {
                score += 1000;
            }
            // Filename starts with query
            else if (fileNameWithoutExt.StartsWith(query))
            {
                score += 500;
            }
            // Filename contains query
            else if (fileName.Contains(query))
            {
                score += 200;
            }
            // Path contains query
            else if (lowerPath.Contains(query))
            {
                score += 100;
            }
            // Fuzzy matching - all query characters appear in order
            else if (FuzzyMatch(lowerPath, query))
            {
                score += 50;
            }

            // Bonus for priority file types
            if (score > 0 && PriorityExtensions.Contains(Path.GetExtension(filePath)))
            {
                score += 25;
            }

            return score;
        }

        /// <summary>
        /// Performs fuzzy matching - checks if all characters in the query
        /// appear in order in the target string.
        /// </summary>
        private bool FuzzyMatch(string target, string query)
        {
            int queryIndex = 0;
            
            foreach (char c in target)
            {
                if (queryIndex < query.Length && c == query[queryIndex])
                {
                    queryIndex++;
                }
            }

            return queryIndex == query.Length;
        }

        /// <summary>
        /// Gets the absolute path for a relative project path.
        /// </summary>
        public string GetAbsolutePath(string relativePath)
        {
            return Path.Combine(_projectRoot, relativePath);
        }

        /// <summary>
        /// Gets the project root directory.
        /// </summary>
        public string ProjectRoot => _projectRoot;

        /// <summary>
        /// Checks if a file exists in the project.
        /// </summary>
        public bool FileExists(string relativePath)
        {
            var absolutePath = GetAbsolutePath(relativePath);
            return File.Exists(absolutePath);
        }
    }
}
#endif

