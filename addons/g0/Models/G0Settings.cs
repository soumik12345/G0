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
        /// The system prompt for the agent.
        /// </summary>
        public string SystemPrompt { get; set; } = 
            "You are G0, a helpful AI assistant for Godot Engine development. " +
            "You have access to the Godot documentation and can search it to provide accurate information. " +
            "You can also search the web for current information, tutorials, library updates, or general programming concepts. " +
            "When users ask about Godot classes, methods, or how to accomplish tasks in Godot, " +
            "use the documentation search tool to find relevant information. " +
            "For questions about recent updates, external libraries, or topics not covered in Godot docs, use web search. " +
            "Provide clear, concise answers with code examples when helpful. " +
            "If you're unsure about something, search the documentation or web first.";

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

