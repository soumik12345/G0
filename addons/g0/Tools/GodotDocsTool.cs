#if TOOLS
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using G0.Documentation;

namespace G0.Tools
{
    /// <summary>
    /// A tool that can be called by AI agents to search the Godot Engine documentation.
    /// Implements the Microsoft.Extensions.AI tool pattern for agent integration.
    /// </summary>
    public class GodotDocsTool
    {
        private readonly DocumentationIndex _index;

        public GodotDocsTool(DocumentationIndex index)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
        }

        /// <summary>
        /// Searches the Godot Engine documentation for information about classes, methods, tutorials, and best practices.
        /// Use this tool when the user asks about Godot-specific concepts, APIs, or how to accomplish tasks in Godot.
        /// </summary>
        /// <param name="query">The search query describing what documentation to find</param>
        /// <param name="maxResults">Maximum number of documentation sections to return (default: 3)</param>
        /// <returns>Formatted documentation results with relevant sections and code examples</returns>
        [Description("Searches the Godot Engine documentation for information about classes, methods, tutorials, and best practices. Use this tool when the user asks about Godot-specific concepts, APIs, or how to accomplish tasks in Godot.")]
        public string SearchDocumentation(
            [Description("The search query describing what documentation to find")] string query,
            [Description("Maximum number of documentation sections to return")] int maxResults = 3)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Error: Please provide a search query.";
            }

            if (_index == null || _index.Entries.Count == 0)
            {
                return "Error: Documentation index is not available. Please download the documentation first.";
            }

            var results = _index.Search(query, maxResults);

            if (results.Count == 0)
            {
                return $"No documentation found for query: \"{query}\". Try rephrasing your search or using different keywords.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} relevant documentation section(s) for \"{query}\":\n");

            for (int i = 0; i < results.Count; i++)
            {
                var entry = results[i];
                sb.AppendLine($"---");
                sb.AppendLine(entry.ToFormattedString(includeCode: true));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets information about a specific Godot class or node type.
        /// </summary>
        /// <param name="className">The name of the Godot class (e.g., "Node2D", "CharacterBody3D")</param>
        /// <returns>Documentation about the specified class</returns>
        [Description("Gets detailed information about a specific Godot class or node type. Use this when the user asks about a specific class, its methods, properties, or usage.")]
        public string GetClassInfo(
            [Description("The name of the Godot class (e.g., 'Node2D', 'CharacterBody3D')")] string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return "Error: Please provide a class name.";
            }

            if (_index == null || _index.Entries.Count == 0)
            {
                return "Error: Documentation index is not available. Please download the documentation first.";
            }

            // Normalize the class name
            var normalizedName = className.Trim();
            if (!normalizedName.StartsWith("class_", StringComparison.OrdinalIgnoreCase))
            {
                normalizedName = "class_" + normalizedName.ToLowerInvariant();
            }

            // Search for entries matching this class
            var matchingEntries = new List<DocumentationEntry>();
            foreach (var entry in _index.Entries)
            {
                if (entry.Url.Contains(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                    entry.Title.Contains(className, StringComparison.OrdinalIgnoreCase))
                {
                    matchingEntries.Add(entry);
                }
            }

            if (matchingEntries.Count == 0)
            {
                // Fall back to general search
                return SearchDocumentation(className, 3);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Documentation for class '{className}':\n");

            // Limit to first 3 most relevant entries
            var entriesToShow = matchingEntries.Count > 3 ? matchingEntries.GetRange(0, 3) : matchingEntries;

            foreach (var entry in entriesToShow)
            {
                sb.AppendLine("---");
                sb.AppendLine(entry.ToFormattedString(includeCode: true));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Lists available topics and categories in the documentation.
        /// </summary>
        /// <returns>A summary of available documentation categories</returns>
        [Description("Lists the main topics and categories available in the Godot documentation. Use this to help users discover what documentation is available.")]
        public string ListTopics()
        {
            if (_index == null || _index.Entries.Count == 0)
            {
                return "Error: Documentation index is not available. Please download the documentation first.";
            }

            // Collect unique categories
            var categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _index.Entries)
            {
                var category = entry.Category;
                if (!string.IsNullOrEmpty(category))
                {
                    if (categories.ContainsKey(category))
                        categories[category]++;
                    else
                        categories[category] = 1;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Available documentation topics:\n");

            var sortedCategories = new List<KeyValuePair<string, int>>(categories);
            sortedCategories.Sort((a, b) => b.Value.CompareTo(a.Value));

            foreach (var kvp in sortedCategories)
            {
                var displayName = FormatCategoryName(kvp.Key);
                sb.AppendLine($"- **{displayName}** ({kvp.Value} sections)");
            }

            sb.AppendLine($"\nTotal: {_index.Entries.Count} documentation sections indexed.");
            sb.AppendLine($"Last updated: {_index.LastUpdated:yyyy-MM-dd HH:mm} UTC");

            return sb.ToString();
        }

        private string FormatCategoryName(string category)
        {
            // Convert snake_case to Title Case
            var words = category.Split('_');
            var result = new StringBuilder();
            foreach (var word in words)
            {
                if (word.Length > 0)
                {
                    result.Append(char.ToUpper(word[0]));
                    if (word.Length > 1)
                        result.Append(word.Substring(1).ToLower());
                    result.Append(" ");
                }
            }
            return result.ToString().Trim();
        }

        /// <summary>
        /// Creates the AI function definitions for this tool.
        /// </summary>
        public static IEnumerable<AIFunction> CreateAIFunctions(DocumentationIndex index)
        {
            var tool = new GodotDocsTool(index);

            yield return AIFunctionFactory.Create(
                tool.SearchDocumentation,
                "search_godot_docs",
                "Searches the Godot Engine documentation for information about classes, methods, tutorials, and best practices. Use this tool when the user asks about Godot-specific concepts, APIs, or how to accomplish tasks in Godot.");

            yield return AIFunctionFactory.Create(
                tool.GetClassInfo,
                "get_godot_class_info",
                "Gets detailed information about a specific Godot class or node type. Use this when the user asks about a specific class, its methods, properties, or usage.");

            yield return AIFunctionFactory.Create(
                tool.ListTopics,
                "list_godot_doc_topics",
                "Lists the main topics and categories available in the Godot documentation. Use this to help users discover what documentation is available.");
        }
    }
}
#endif

