#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using GenerativeAI;
using G0.Documentation;
using G0.Tools;

// Alias to avoid ambiguity with G0.Models.ChatMessage
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace G0
{
    /// <summary>
    /// AI Agent client that uses Microsoft.Extensions.AI with tool-calling capabilities.
    /// Supports multiple model providers (Gemini, OpenAI, Azure OpenAI) and integrates
    /// the Godot documentation search tool.
    /// </summary>
    public partial class AgentClient : Node
    {
        [Signal]
        public delegate void ChunkReceivedEventHandler(string chunk);

        [Signal]
        public delegate void StreamCompleteEventHandler(string fullResponse);

        [Signal]
        public delegate void ErrorOccurredEventHandler(string errorMessage);

        [Signal]
        public delegate void ToolCalledEventHandler(string toolName, string arguments);

        [Signal]
        public delegate void AgentThinkingEventHandler(string thinking);

        [Signal]
        public delegate void ToolExecutingEventHandler(string toolName, string arguments);

        [Signal]
        public delegate void ToolResultReceivedEventHandler(string toolName, string result);

        private Models.G0Settings _settings;
        private IChatClient _chatClient;
        private List<AITool> _tools;
        private StringBuilder _currentResponse;
        private bool _isStreaming;
        private CancellationTokenSource _cancellationTokenSource;
        private DocumentationIndex _documentationIndex;

        public bool IsStreaming => _isStreaming;

        public override void _Ready()
        {
            _currentResponse = new StringBuilder();
            _tools = new List<AITool>();
        }

        /// <summary>
        /// Configures the agent with the provided settings and documentation index.
        /// </summary>
        public void Configure(Models.G0Settings settings, DocumentationIndex documentationIndex = null)
        {
            _settings = settings;
            _documentationIndex = documentationIndex;

            if (string.IsNullOrEmpty(_settings.ApiKey))
            {
                GD.PrintErr("G0: API key is not configured");
                return;
            }

            try
            {
                // Create the appropriate chat client based on provider
                _chatClient = CreateChatClient();

                // Set up tools if agent mode is enabled
                _tools.Clear();
                if (_settings.UseAgent)
                {
                    // Add Godot documentation search tool
                    if (_documentationIndex != null && _documentationIndex.Entries.Count > 0)
                    {
                        foreach (AITool tool in GodotDocsTool.CreateAIFunctions(_documentationIndex))
                        {
                            _tools.Add(tool);
                        }
                    }

                    // Add web search tool if Serper API key is configured
                    if (!string.IsNullOrEmpty(_settings.SerperApiKey))
                    {
                        foreach (AITool tool in SerperWebSearchTool.CreateAIFunctions(_settings.SerperApiKey))
                        {
                            _tools.Add(tool);
                        }
                    }

                    if (_tools.Count > 0)
                    {
                        GD.Print($"G0: Agent configured with {_tools.Count} tools");
                    }
                    else
                    {
                        GD.Print("G0: Agent mode enabled but no tools configured (missing documentation and/or Serper API key)");
                    }
                }
                else
                {
                    GD.Print("G0: Agent configured without tools (direct model mode)");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Failed to configure agent: {ex.Message}");
                _chatClient = null;
            }
        }

        private IChatClient CreateChatClient()
        {
            switch (_settings.Provider)
            {
                case Models.ModelProvider.OpenAI:
                    return CreateOpenAIChatClient();

                case Models.ModelProvider.AzureOpenAI:
                    return CreateAzureOpenAIChatClient();

                case Models.ModelProvider.Gemini:
                default:
                    return CreateGeminiChatClient();
            }
        }

        private IChatClient CreateGeminiChatClient()
        {
            // Use the Google GenerativeAI SDK wrapped in IChatClient
            var googleAi = new GoogleAi(_settings.ApiKey);
            var model = googleAi.CreateGenerativeModel($"models/{_settings.SelectedModel}");
            
            // Wrap in a custom adapter that implements IChatClient
            return new GeminiChatClientAdapter(model, _settings.SelectedModel);
        }

        private IChatClient CreateOpenAIChatClient()
        {
            // Use OpenAI via Microsoft.Extensions.AI.OpenAI
            var endpoint = string.IsNullOrEmpty(_settings.ModelEndpoint) 
                ? new Uri("https://api.openai.com/v1") 
                : new Uri(_settings.ModelEndpoint);

            var client = new OpenAI.OpenAIClient(_settings.ApiKey);
            return client.AsChatClient(_settings.SelectedModel);
        }

        private IChatClient CreateAzureOpenAIChatClient()
        {
            // Azure OpenAI requires Azure.AI.OpenAI package which is not currently included
            // Fall back to Gemini for now
            GD.PrintErr("G0: Azure OpenAI is not currently supported. Falling back to Gemini.");
            return CreateGeminiChatClient();
        }

        /// <summary>
        /// Sends a chat completion request with optional tool calling.
        /// </summary>
        public async Task SendChatCompletionAsync(List<Models.ChatMessage> messages)
        {
            GD.Print($"G0: SendChatCompletionAsync called with {messages.Count} messages");

            if (_isStreaming)
            {
                GD.PrintErr("G0: Already streaming a response");
                return;
            }

            if (string.IsNullOrEmpty(_settings?.ApiKey))
            {
                GD.PrintErr("G0: API key is empty");
                CallDeferred(nameof(EmitErrorSignal), "API key is not configured. Please set it in settings.");
                return;
            }

            if (_chatClient == null)
            {
                Configure(_settings, _documentationIndex);
                if (_chatClient == null)
                {
                    GD.PrintErr("G0: Failed to create chat client");
                    CallDeferred(nameof(EmitErrorSignal), "Failed to initialize AI model.");
                    return;
                }
            }

            _isStreaming = true;
            _currentResponse.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                GD.Print("G0: Starting agent completion...");
                await ProcessChatWithToolsAsync(messages, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                GD.Print("G0: Request was cancelled");
            }
            catch (Exception ex)
            {
                _isStreaming = false;
                GD.PrintErr($"G0: Request exception: {ex.Message}\n{ex.StackTrace}");
                CallDeferred(nameof(EmitErrorSignal), $"Request failed: {ex.Message}");
            }
        }

        private async Task ProcessChatWithToolsAsync(List<Models.ChatMessage> messages, CancellationToken token)
        {
            try
            {
                // Build the chat messages list
                var chatMessages = new List<AIChatMessage>();

                // Add system message if configured
                if (!string.IsNullOrEmpty(_settings.SystemPrompt))
                {
                    chatMessages.Add(new AIChatMessage(ChatRole.System, _settings.SystemPrompt));
                }

                // Add conversation history
                foreach (var msg in messages)
                {
                    var role = msg.Role == "user" ? ChatRole.User : ChatRole.Assistant;
                    chatMessages.Add(new AIChatMessage(role, msg.Content));
                }

                // Create chat options with tools if available
                var options = new ChatOptions();
                if (_tools.Count > 0)
                {
                    options.Tools = new List<AITool>(_tools);
                    options.ToolMode = ChatToolMode.Auto;
                }

                // Process with potential tool calls in a loop
                int maxToolIterations = _settings.MaxAgentIterations;
                int toolIterations = 0;

                while (toolIterations < maxToolIterations)
                {
                    token.ThrowIfCancellationRequested();

                    // Get completion (with streaming if supported)
                    var response = await GetCompletionWithStreamingAsync(chatMessages, options, token);

                    if (response == null)
                    {
                        break;
                    }

                    // Check for tool calls
                    var toolCalls = ExtractToolCalls(response);
                    if (toolCalls.Count == 0)
                    {
                        // No more tool calls, we're done
                        break;
                    }

                    toolIterations++;
                    GD.Print($"G0: Processing {toolCalls.Count} tool call(s), iteration {toolIterations}");

                    // Emit agent thinking signal with any text content from the response
                    var thinkingText = response.Text;
                    if (!string.IsNullOrEmpty(thinkingText))
                    {
                        CallDeferred(nameof(EmitAgentThinkingSignal), thinkingText);
                    }

                    // Add assistant message with tool calls
                    chatMessages.Add(response);

                    // Execute each tool and add results
                    foreach (var toolCall in toolCalls)
                    {
                        var argumentsStr = toolCall.Arguments?.ToString() ?? "{}";
                        CallDeferred(nameof(EmitToolCalledSignal), toolCall.Name, argumentsStr);
                        CallDeferred(nameof(EmitToolExecutingSignal), toolCall.Name, argumentsStr);

                        try
                        {
                            var result = await ExecuteToolAsync(toolCall, token);
                            
                            // Emit tool result signal
                            CallDeferred(nameof(EmitToolResultReceivedSignal), toolCall.Name, result);
                            
                            // Add tool result as a message
                            var toolResultMessage = new AIChatMessage(ChatRole.Tool, result);
                            toolResultMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                            toolResultMessage.AdditionalProperties["tool_call_id"] = toolCall.CallId;
                            chatMessages.Add(toolResultMessage);
                        }
                        catch (Exception ex)
                        {
                            GD.PrintErr($"G0: Tool execution error: {ex.Message}");
                            var errorResult = $"Error executing tool: {ex.Message}";
                            
                            // Emit error as tool result
                            CallDeferred(nameof(EmitToolResultReceivedSignal), toolCall.Name, errorResult);
                            
                            var errorMessage = new AIChatMessage(ChatRole.Tool, errorResult);
                            errorMessage.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                            errorMessage.AdditionalProperties["tool_call_id"] = toolCall.CallId;
                            chatMessages.Add(errorMessage);
                        }
                    }
                }

                _isStreaming = false;
                var fullResponse = _currentResponse.ToString();
                GD.Print($"G0: Response complete ({fullResponse.Length} chars)");
                CallDeferred(nameof(EmitStreamCompleteSignal), fullResponse);
            }
            catch (Exception ex)
            {
                _isStreaming = false;
                GD.PrintErr($"G0: Agent processing error: {ex.Message}\n{ex.StackTrace}");
                CallDeferred(nameof(EmitErrorSignal), $"Agent processing failed: {ex.Message}");
            }
        }

        private async Task<AIChatMessage> GetCompletionWithStreamingAsync(
            List<AIChatMessage> messages, 
            ChatOptions options, 
            CancellationToken token)
        {
            try
            {
                // Try streaming first
                var streamingResult = _chatClient.CompleteStreamingAsync(messages, options, token);
                var responseBuilder = new StringBuilder();
                AIChatMessage lastMessage = null;

                await foreach (var update in streamingResult)
                {
                    token.ThrowIfCancellationRequested();

                    if (update.Text != null)
                    {
                        responseBuilder.Append(update.Text);
                        _currentResponse.Append(update.Text);
                        CallDeferred(nameof(EmitChunkSignal), update.Text);
                    }

                    // Capture the message for tool call detection
                    if (update.Contents != null)
                    {
                        foreach (var content in update.Contents)
                        {
                            if (content is FunctionCallContent)
                            {
                                // Has function calls, build the complete message
                                if (lastMessage == null)
                                {
                                    lastMessage = new AIChatMessage(ChatRole.Assistant, "");
                                }
                            }
                        }
                    }
                }

                // Return the complete message
                return lastMessage ?? new AIChatMessage(ChatRole.Assistant, responseBuilder.ToString());
            }
            catch (NotSupportedException)
            {
                // Streaming not supported, fall back to non-streaming
                GD.Print("G0: Streaming not supported, using non-streaming completion");
                var result = await _chatClient.CompleteAsync(messages, options, token);
                
                if (result.Message != null && !string.IsNullOrEmpty(result.Message.Text))
                {
                    _currentResponse.Append(result.Message.Text);
                    CallDeferred(nameof(EmitChunkSignal), result.Message.Text);
                }

                return result.Message;
            }
        }

        private List<FunctionCallContent> ExtractToolCalls(AIChatMessage message)
        {
            var toolCalls = new List<FunctionCallContent>();

            if (message?.Contents == null)
                return toolCalls;

            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent functionCall)
                {
                    toolCalls.Add(functionCall);
                }
            }

            return toolCalls;
        }

        private async Task<string> ExecuteToolAsync(FunctionCallContent toolCall, CancellationToken token)
        {
            GD.Print($"G0: Executing tool: {toolCall.Name}");

            // Find the matching tool (AIFunction is the concrete type we use)
            AIFunction matchingTool = null;
            foreach (var tool in _tools)
            {
                if (tool is AIFunction func && func.Metadata.Name == toolCall.Name)
                {
                    matchingTool = func;
                    break;
                }
            }

            if (matchingTool == null)
            {
                return $"Error: Unknown tool '{toolCall.Name}'";
            }

            try
            {
                // Execute the tool
                var result = await matchingTool.InvokeAsync(toolCall.Arguments, token);
                return result?.ToString() ?? "Tool executed successfully but returned no result.";
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Tool execution failed: {ex.Message}");
                return $"Error executing tool: {ex.Message}";
            }
        }

        private void EmitChunkSignal(string chunk)
        {
            EmitSignal(SignalName.ChunkReceived, chunk);
        }

        private void EmitStreamCompleteSignal(string fullResponse)
        {
            EmitSignal(SignalName.StreamComplete, fullResponse);
        }

        private void EmitErrorSignal(string errorMessage)
        {
            EmitSignal(SignalName.ErrorOccurred, errorMessage);
        }

        private void EmitToolCalledSignal(string toolName, string arguments)
        {
            EmitSignal(SignalName.ToolCalled, toolName, arguments);
        }

        private void EmitAgentThinkingSignal(string thinking)
        {
            EmitSignal(SignalName.AgentThinking, thinking);
        }

        private void EmitToolExecutingSignal(string toolName, string arguments)
        {
            EmitSignal(SignalName.ToolExecuting, toolName, arguments);
        }

        private void EmitToolResultReceivedSignal(string toolName, string result)
        {
            EmitSignal(SignalName.ToolResultReceived, toolName, result);
        }

        public void CancelRequest()
        {
            _cancellationTokenSource?.Cancel();
            _isStreaming = false;
        }

        public override void _ExitTree()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Adapter to wrap the Google GenerativeAI SDK as an IChatClient.
    /// This adapter builds conversation context as a formatted prompt.
    /// </summary>
    internal class GeminiChatClientAdapter : IChatClient
    {
        private readonly GenerativeModel _model;
        private readonly string _modelId;

        public GeminiChatClientAdapter(GenerativeModel model, string modelId)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _modelId = modelId;
        }

        public ChatClientMetadata Metadata => new ChatClientMetadata("Gemini", new Uri("https://generativelanguage.googleapis.com"), _modelId);

        public async Task<ChatCompletion> CompleteAsync(
            IList<AIChatMessage> chatMessages,
            ChatOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var prompt = BuildFullPrompt(chatMessages);
            var response = await _model.GenerateContentAsync(prompt, cancellationToken);
            var text = response.Text() ?? "";
            return new ChatCompletion(new AIChatMessage(ChatRole.Assistant, text));
        }

        public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
            IList<AIChatMessage> chatMessages,
            ChatOptions options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var prompt = BuildFullPrompt(chatMessages);
            var streamingResponse = _model.StreamContentAsync(prompt, cancellationToken: cancellationToken);
            
            await foreach (var chunk in streamingResponse)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = chunk.Text();
                if (!string.IsNullOrEmpty(text))
                {
                    yield return new StreamingChatCompletionUpdate
                    {
                        Role = ChatRole.Assistant,
                        Text = text
                    };
                }
            }
        }

        public object GetService(Type serviceType, object serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }

        /// <summary>
        /// Builds a full prompt string from the chat messages, including system instruction and history.
        /// </summary>
        private string BuildFullPrompt(IList<AIChatMessage> messages)
        {
            var promptBuilder = new StringBuilder();
            
            foreach (var message in messages)
            {
                var text = message.Text ?? "";
                if (string.IsNullOrWhiteSpace(text))
                    continue;
                
                switch (message.Role.Value)
                {
                    case "system":
                        promptBuilder.AppendLine("[System Instructions]");
                        promptBuilder.AppendLine(text);
                        promptBuilder.AppendLine();
                        break;
                        
                    case "user":
                        promptBuilder.AppendLine("[User]");
                        promptBuilder.AppendLine(text);
                        promptBuilder.AppendLine();
                        break;
                        
                    case "assistant":
                        promptBuilder.AppendLine("[Assistant]");
                        promptBuilder.AppendLine(text);
                        promptBuilder.AppendLine();
                        break;
                        
                    case "tool":
                        promptBuilder.AppendLine("[Tool Result]");
                        promptBuilder.AppendLine(text);
                        promptBuilder.AppendLine();
                        break;
                }
            }
            
            // Add instruction for the assistant to respond
            promptBuilder.AppendLine("[Assistant]");
            
            return promptBuilder.ToString();
        }
    }
}
#endif
