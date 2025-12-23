#if TOOLS
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace G0.Documentation
{
    /// <summary>
    /// Represents a single indexed documentation entry from the Godot documentation.
    /// </summary>
    public class DocumentationEntry
    {
        /// <summary>
        /// The full URL to this documentation page.
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        /// <summary>
        /// The title of the documentation page.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        /// <summary>
        /// The section heading within the page (if applicable).
        /// </summary>
        [JsonPropertyName("section")]
        public string Section { get; set; } = "";

        /// <summary>
        /// The main text content of this entry.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";

        /// <summary>
        /// Code examples found in this section.
        /// </summary>
        [JsonPropertyName("code_examples")]
        public List<CodeExample> CodeExamples { get; set; } = new List<CodeExample>();

        /// <summary>
        /// Keywords extracted from the content for search matching.
        /// </summary>
        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new List<string>();

        /// <summary>
        /// The category/path of the documentation (e.g., "getting_started/step_by_step").
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        /// <summary>
        /// When this entry was last indexed.
        /// </summary>
        [JsonPropertyName("indexed_at")]
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Calculates a relevance score for this entry based on a search query.
        /// </summary>
        public double CalculateRelevance(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return 0;

            var queryLower = query.ToLowerInvariant();
            var queryTerms = queryLower.Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
            
            double score = 0;

            // Title match (highest weight)
            var titleLower = Title.ToLowerInvariant();
            foreach (var term in queryTerms)
            {
                if (titleLower.Contains(term))
                    score += 10;
                if (titleLower == term)
                    score += 20;
            }

            // Section match (high weight)
            var sectionLower = Section.ToLowerInvariant();
            foreach (var term in queryTerms)
            {
                if (sectionLower.Contains(term))
                    score += 5;
            }

            // Keyword match (medium weight)
            foreach (var keyword in Keywords)
            {
                var keywordLower = keyword.ToLowerInvariant();
                foreach (var term in queryTerms)
                {
                    if (keywordLower == term)
                        score += 3;
                    else if (keywordLower.Contains(term))
                        score += 1;
                }
            }

            // Content match (lower weight)
            var contentLower = Content.ToLowerInvariant();
            foreach (var term in queryTerms)
            {
                // Count occurrences
                var count = CountOccurrences(contentLower, term);
                score += Math.Min(count * 0.5, 5); // Cap at 5 points per term
            }

            // Bonus for having code examples if query seems code-related
            var codeKeywords = new[] { "code", "example", "how to", "gdscript", "c#", "function", "method", "class" };
            foreach (var codeKeyword in codeKeywords)
            {
                if (queryLower.Contains(codeKeyword) && CodeExamples.Count > 0)
                {
                    score += 5;
                    break;
                }
            }

            return score;
        }

        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        /// <summary>
        /// Returns a formatted string representation for display in agent responses.
        /// </summary>
        public string ToFormattedString(bool includeCode = true)
        {
            var result = $"## {Title}";
            
            if (!string.IsNullOrEmpty(Section))
                result += $" - {Section}";
            
            result += $"\n**URL:** {Url}\n\n";
            result += Content;

            if (includeCode && CodeExamples.Count > 0)
            {
                result += "\n\n### Code Examples:\n";
                foreach (var example in CodeExamples)
                {
                    result += $"\n```{example.Language}\n{example.Code}\n```\n";
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Represents a code example found in documentation.
    /// </summary>
    public class CodeExample
    {
        /// <summary>
        /// The programming language of the code (gdscript, csharp, etc.).
        /// </summary>
        [JsonPropertyName("language")]
        public string Language { get; set; } = "gdscript";

        /// <summary>
        /// The actual code content.
        /// </summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";
    }

    /// <summary>
    /// Represents the complete documentation index.
    /// </summary>
    public class DocumentationIndex
    {
        /// <summary>
        /// Version of the index format.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// The Godot version this documentation is for.
        /// </summary>
        [JsonPropertyName("godot_version")]
        public string GodotVersion { get; set; } = "stable";

        /// <summary>
        /// When the index was last updated.
        /// </summary>
        [JsonPropertyName("last_updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The base URL of the documentation.
        /// </summary>
        [JsonPropertyName("base_url")]
        public string BaseUrl { get; set; } = "https://docs.godotengine.org/en/stable/";

        /// <summary>
        /// All indexed documentation entries.
        /// </summary>
        [JsonPropertyName("entries")]
        public List<DocumentationEntry> Entries { get; set; } = new List<DocumentationEntry>();

        /// <summary>
        /// Total number of pages indexed.
        /// </summary>
        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }

        /// <summary>
        /// Searches the index for entries matching the query.
        /// </summary>
        public List<DocumentationEntry> Search(string query, int maxResults = 5)
        {
            if (string.IsNullOrWhiteSpace(query) || Entries.Count == 0)
                return new List<DocumentationEntry>();

            var scoredEntries = new List<(DocumentationEntry Entry, double Score)>();

            foreach (var entry in Entries)
            {
                var score = entry.CalculateRelevance(query);
                if (score > 0)
                {
                    scoredEntries.Add((entry, score));
                }
            }

            // Sort by score descending and take top results
            scoredEntries.Sort((a, b) => b.Score.CompareTo(a.Score));

            var results = new List<DocumentationEntry>();
            for (int i = 0; i < Math.Min(maxResults, scoredEntries.Count); i++)
            {
                results.Add(scoredEntries[i].Entry);
            }

            return results;
        }
    }
}
#endif

