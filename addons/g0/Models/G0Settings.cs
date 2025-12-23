#if TOOLS
using System.Collections.Generic;

namespace G0.Models
{
    /// <summary>
    /// Supported AI model providers.
    /// </summary>
    public enum ModelProvider
    {
        /// <summary>
        /// Google Gemini models via Google AI Studio API.
        /// </summary>
        Gemini,

        /// <summary>
        /// OpenAI models (GPT-4, etc.) via OpenAI API.
        /// </summary>
        OpenAI,

        /// <summary>
        /// Azure OpenAI Service.
        /// </summary>
        AzureOpenAI
    }

    public class G0Settings
    {
        // API Configuration
        public string ApiKey { get; set; } = "";
        public string SelectedModel { get; set; } = "gemini-2.5-flash";
        public int MaxHistorySize { get; set; } = 100;
        public List<string> AvailableModels { get; set; } = new List<string>
        {
            "gemini-2.5-flash",
            "gemini-2.0-flash",
            "gemini-1.5-pro"
        };

        // Model Provider Configuration
        public ModelProvider Provider { get; set; } = ModelProvider.Gemini;

        /// <summary>
        /// Custom endpoint URL for Azure OpenAI or other hosted models.
        /// Leave empty to use default endpoints.
        /// </summary>
        public string ModelEndpoint { get; set; } = "";

        // Agent Configuration
        /// <summary>
        /// Whether to use the AI agent with tool-calling capabilities.
        /// When enabled, the agent can search Godot documentation automatically.
        /// </summary>
        public bool UseAgent { get; set; } = true;

        /// <summary>
        /// Maximum number of iterations the agent can perform when using tools.
        /// Each iteration includes reasoning, tool calls, and processing tool results.
        /// </summary>
        public int MaxAgentIterations { get; set; } = 50;

        /// <summary>
        /// The system prompt for the agent.
        /// </summary>
        public string SystemPrompt { get; set; } = 
            "You are G0, a helpful AI agent designed to assist developers building games with the Godot Engine. " +
            "You have access to the following tools:\n\n" +
            
            "## Godot Documentation Tools\n" +
            "- **search_godot_docs**: Search the Godot Engine documentation for classes, methods, tutorials, and best practices. " +
            "Use this when users ask about Godot-specific concepts, APIs, or how to accomplish tasks in Godot.\n" +
            "- **get_godot_class_info**: Get detailed information about a specific Godot class or node type (e.g., 'Node2D', 'CharacterBody3D').\n" +
            "- **list_godot_doc_topics**: List available documentation topics and categories.\n\n" +
            
            "## File Tools\n" +
            "- **read_file**: Read the contents of a file from the project. Use this to examine code, configuration, or text files.\n" +
            "- **list_files**: List files and directories in the project. Use this to explore the project structure.\n" +
            "- **search_files**: Search for code patterns across project files using regex. Supports file type filtering, " +
            "context lines, case sensitivity, and whole word matching. Use this to find usages, locate code patterns, or search for specific text.\n" +
            "- **find_files**: Find files by name or extension using glob patterns (e.g., '*.cs', '*Controller*').\n\n" +
            
            "## Web Search Tool\n" +
            "- **search_web**: Search the web for current information, tutorials, library documentation, or general programming topics. " +
            "Use this for recent updates, external libraries, or topics not covered in Godot documentation.\n\n" +
            
            "## Guidelines\n" +
            "- When users ask about Godot classes, methods, or engine features, use the documentation tools first.\n" +
            "- When users ask about their project code, use the file tools to read and search their codebase.\n" +
            "- For questions about recent updates, external libraries, or general programming, use web search.\n" +
            "- Provide clear, concise answers with code examples when helpful.\n" +
            "- If you're unsure about something, search the documentation or project files before guessing.";

        // Web Search Configuration
        /// <summary>
        /// API key for Serper web search service (serper.dev).
        /// When configured, the agent can search the web for additional information.
        /// </summary>
        public string SerperApiKey { get; set; } = "";

        // Documentation Configuration
        /// <summary>
        /// Whether the documentation index has been downloaded.
        /// </summary>
        public bool DocumentationIndexed { get; set; } = false;

        /// <summary>
        /// When the documentation was last indexed.
        /// </summary>
        public string LastDocumentationUpdate { get; set; } = "";

        public G0Settings() { }

        public G0Settings Clone()
        {
            return new G0Settings
            {
                ApiKey = ApiKey,
                SelectedModel = SelectedModel,
                MaxHistorySize = MaxHistorySize,
                AvailableModels = new List<string>(AvailableModels),
                Provider = Provider,
                ModelEndpoint = ModelEndpoint,
                UseAgent = UseAgent,
                MaxAgentIterations = MaxAgentIterations,
                SystemPrompt = SystemPrompt,
                SerperApiKey = SerperApiKey,
                DocumentationIndexed = DocumentationIndexed,
                LastDocumentationUpdate = LastDocumentationUpdate
            };
        }

        /// <summary>
        /// Gets the available models based on the current provider.
        /// </summary>
        public List<string> GetModelsForProvider()
        {
            return Provider switch
            {
                ModelProvider.OpenAI => new List<string>
                {
                    "gpt-4o",
                    "gpt-4o-mini",
                    "gpt-4-turbo",
                    "gpt-3.5-turbo"
                },
                ModelProvider.AzureOpenAI => new List<string>
                {
                    "gpt-4o",
                    "gpt-4o-mini",
                    "gpt-4-turbo"
                },
                _ => new List<string>
                {
                    "gemini-2.5-flash",
                    "gemini-2.0-flash",
                    "gemini-1.5-pro"
                }
            };
        }
    }
}
#endif

