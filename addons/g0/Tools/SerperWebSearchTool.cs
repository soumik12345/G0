#if TOOLS
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Microsoft.Extensions.AI;

namespace G0.Tools
{
    /// <summary>
    /// A tool that can be called by AI agents to search the web using the Serper API.
    /// Implements the Microsoft.Extensions.AI tool pattern for agent integration.
    /// </summary>
    public class SerperWebSearchTool
    {
        private const string SerperApiUrl = "https://google.serper.dev/search";
        private readonly string _apiKey;
        private readonly System.Net.Http.HttpClient _httpClient;

        public SerperWebSearchTool(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey), "Serper API key is required");
            }
            
            _apiKey = apiKey;
            _httpClient = new System.Net.Http.HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Searches the web for information using the Serper API (Google Search).
        /// Use this tool when you need current information, external tutorials, library documentation,
        /// or topics not covered in Godot's built-in documentation.
        /// </summary>
        /// <param name="query">The search query to find information about</param>
        /// <param name="numResults">Maximum number of search results to return (default: 5)</param>
        /// <returns>Formatted search results with titles, snippets, and links</returns>
        [Description("Searches the web for current information, tutorials, library documentation, or general programming topics. Use this when the user asks about topics not covered in Godot documentation, recent updates, external libraries, or needs current web information.")]
        public async Task<string> SearchWeb(
            [Description("The search query to find information about")] string query,
            [Description("Maximum number of search results to return")] int numResults = 5)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Error: Please provide a search query.";
            }

            try
            {
                // Clamp numResults to reasonable range
                numResults = Math.Clamp(numResults, 1, 10);

                // Create request body
                var requestBody = new
                {
                    q = query,
                    num = numResults
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Create request with Serper API key header
                var request = new HttpRequestMessage(HttpMethod.Post, SerperApiUrl)
                {
                    Content = content
                };
                request.Headers.Add("X-API-KEY", _apiKey);

                // Send request
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    GD.PrintErr($"G0: Serper API error: {response.StatusCode} - {errorContent}");
                    return $"Error: Web search failed with status {response.StatusCode}. Please check your Serper API key.";
                }

                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
                var searchResults = JsonSerializer.Deserialize<SerperSearchResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (searchResults == null || searchResults.Organic == null || searchResults.Organic.Count == 0)
                {
                    return $"No results found for query: \"{query}\". Try rephrasing your search.";
                }

                // Format results
                var sb = new StringBuilder();
                sb.AppendLine($"Found {searchResults.Organic.Count} web result(s) for \"{query}\":\n");

                for (int i = 0; i < searchResults.Organic.Count; i++)
                {
                    var result = searchResults.Organic[i];
                    sb.AppendLine($"---");
                    sb.AppendLine($"**{i + 1}. {result.Title}**");
                    if (!string.IsNullOrEmpty(result.Snippet))
                    {
                        sb.AppendLine(result.Snippet);
                    }
                    sb.AppendLine($"Link: {result.Link}");
                    sb.AppendLine();
                }

                // Include knowledge graph if available
                if (searchResults.KnowledgeGraph != null && !string.IsNullOrEmpty(searchResults.KnowledgeGraph.Description))
                {
                    sb.AppendLine("---");
                    sb.AppendLine("**Quick Answer:**");
                    if (!string.IsNullOrEmpty(searchResults.KnowledgeGraph.Title))
                    {
                        sb.AppendLine($"**{searchResults.KnowledgeGraph.Title}**");
                    }
                    sb.AppendLine(searchResults.KnowledgeGraph.Description);
                    sb.AppendLine();
                }

                // Include answer box if available
                if (searchResults.AnswerBox != null && !string.IsNullOrEmpty(searchResults.AnswerBox.Answer))
                {
                    sb.AppendLine("---");
                    sb.AppendLine("**Featured Answer:**");
                    sb.AppendLine(searchResults.AnswerBox.Answer);
                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (TaskCanceledException)
            {
                return "Error: Web search timed out. Please try again.";
            }
            catch (HttpRequestException ex)
            {
                GD.PrintErr($"G0: Web search network error: {ex.Message}");
                return $"Error: Network error during web search. Please check your internet connection.";
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Web search error: {ex.Message}");
                return $"Error: Failed to search the web: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates the AI function definitions for this tool.
        /// </summary>
        public static IEnumerable<AIFunction> CreateAIFunctions(string apiKey)
        {
            var tool = new SerperWebSearchTool(apiKey);

            yield return AIFunctionFactory.Create(
                tool.SearchWeb,
                "search_web",
                "Searches the web for current information, tutorials, library documentation, or general programming topics. Use this when the user asks about topics not covered in Godot documentation, recent updates, external libraries, or needs current web information.");
        }
    }

    #region Serper API Response Models

    /// <summary>
    /// Response structure from Serper API
    /// </summary>
    internal class SerperSearchResponse
    {
        public List<SerperOrganicResult> Organic { get; set; }
        public SerperKnowledgeGraph KnowledgeGraph { get; set; }
        public SerperAnswerBox AnswerBox { get; set; }
    }

    internal class SerperOrganicResult
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Snippet { get; set; }
        public int Position { get; set; }
    }

    internal class SerperKnowledgeGraph
    {
        public string Title { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string Website { get; set; }
    }

    internal class SerperAnswerBox
    {
        public string Title { get; set; }
        public string Answer { get; set; }
        public string Snippet { get; set; }
        public string Link { get; set; }
    }

    #endregion
}
#endif

