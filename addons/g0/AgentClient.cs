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
        // Existing signals for backward compatibility
        [Signal]
        public delegate void ChunkReceivedEventHandler(string chunk);

        [Signal]
        public delegate void StreamCompleteEventHandler(string fullResponse);

        [Signal]
        public delegate void ErrorOccurredEventHandler(string errorMessage);

        [Signal]
        public delegate void ToolCalledEventHandler(string toolName, string arguments);

        // New signals for intermediate agentic step streaming
        [Signal]
        public delegate void AgentStepStartedEventHandler(int iteration, int maxIterations);

        [Signal]
        public delegate void ReasoningChunkReceivedEventHandler(string chunk, int iteration);

        [Signal]
        public delegate void ToolCallStartedEventHandler(string toolName, string arguments, int iteration);

        [Signal]
        public delegate void ToolCallCompletedEventHandler(string toolName, string result, int iteration);

        private Models.G0Settings _settings;
        private IChatClient _chatClient;
        private List<AITool> _tools;
        private StringBuilder _currentResponse;
        private StringBuilder _currentIterationReasoning;
        private bool _isStreaming;
        private int _currentIteration;
        private const int MaxToolIterations = 5;
        private CancellationTokenSource _cancellationTokenSource;
        private DocumentationIndex _documentationIndex;

        public bool IsStreaming => _isStreaming;

        public override void _Ready()
        {
            _currentResponse = new StringBuilder();
            _currentIterationReasoning = new StringBuilder();
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
                bool hasTools = _tools.Count > 0;
                if (hasTools)
                {
                    options.Tools = new List<AITool>(_tools);
                    options.ToolMode = ChatToolMode.Auto;
                }

                // Process with potential tool calls in a loop
                _currentIteration = 0;
                bool hadToolCalls = false;

                while (_currentIteration < MaxToolIterations)
                {
                    token.ThrowIfCancellationRequested();
                    
                    _currentIteration++;
                    _currentIterationReasoning.Clear();

                    // If we've had tool calls, clear the current response so only final response is kept
                    if (hadToolCalls)
                    {
                        _currentResponse.Clear();
                    }

                    // Emit iteration start for agent mode with tools
                    if (hasTools)
                    {
                        CallDeferred(nameof(EmitAgentStepStartedSignal), _currentIteration, MaxToolIterations);
                        GD.Print($"G0: Starting agent iteration {_currentIteration}/{MaxToolIterations}");
                    }

                    // Get completion (with streaming if supported)
                    // Pass whether this could be an intermediate iteration (has tools and not confirmed final)
                    bool couldBeIntermediate = hasTools;
                    var response = await GetCompletionWithStreamingAsync(chatMessages, options, couldBeIntermediate, token);

                    if (response == null)
                    {
                        break;
                    }

                    // Check for tool calls
                    var toolCalls = ExtractToolCalls(response);
                    if (toolCalls.Count == 0)
                    {
                        // No more tool calls, we're done - the response IS the final response
                        break;
                    }

                    // Mark that we've had tool calls - next iteration's streaming should not go to main response
                    hadToolCalls = true;
                    GD.Print($"G0: Processing {toolCalls.Count} tool call(s), iteration {_currentIteration}");

                    // Add assistant message with tool calls
                    chatMessages.Add(response);

                    // Execute each tool and add results
                    foreach (var toolCall in toolCalls)
                    {
                        var toolArgs = toolCall.Arguments?.ToString() ?? "";
                        
                        // Emit both old signal (for backward compatibility) and new detailed signal
                        CallDeferred(nameof(EmitToolCalledSignal), toolCall.Name, toolArgs);
                        CallDeferred(nameof(EmitToolCallStartedSignal), toolCall.Name, toolArgs, _currentIteration);

                        try
                        {
                            var result = await ExecuteToolAsync(toolCall, token);
                            
                            // Emit tool completion with result
                            CallDeferred(nameof(EmitToolCallCompletedSignal), toolCall.Name, result, _currentIteration);
                            
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
                            
                            // Emit tool completion with error
                            CallDeferred(nameof(EmitToolCallCompletedSignal), toolCall.Name, errorResult, _currentIteration);
                            
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
            bool isAgentMode,
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

                    // Check for function calls first to determine if this is intermediate reasoning
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

                    if (update.Text != null)
                    {
                        responseBuilder.Append(update.Text);
                        _currentIterationReasoning.Append(update.Text);
                        
                        // In agent mode during iterations with tool calls, emit as reasoning
                        // Otherwise emit as regular chunks (final response)
                        if (isAgentMode && _currentIteration < MaxToolIterations)
                        {
                            // This could be intermediate reasoning before tool calls
                            // We'll emit both reasoning and regular chunks - UI can decide which to show
                            CallDeferred(nameof(EmitReasoningChunkSignal), update.Text, _currentIteration);
                        }
                        
                        // Always accumulate in current response and emit chunk for final text
                        _currentResponse.Append(update.Text);
                        CallDeferred(nameof(EmitChunkSignal), update.Text);
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
                    _currentIterationReasoning.Append(result.Message.Text);
                    _currentResponse.Append(result.Message.Text);
                    
                    if (isAgentMode && _currentIteration < MaxToolIterations)
                    {
                        CallDeferred(nameof(EmitReasoningChunkSignal), result.Message.Text, _currentIteration);
                    }
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

        // New emit methods for intermediate agentic step streaming
        private void EmitAgentStepStartedSignal(int iteration, int maxIterations)
        {
            EmitSignal(SignalName.AgentStepStarted, iteration, maxIterations);
        }

        private void EmitReasoningChunkSignal(string chunk, int iteration)
        {
            EmitSignal(SignalName.ReasoningChunkReceived, chunk, iteration);
        }

        private void EmitToolCallStartedSignal(string toolName, string arguments, int iteration)
        {
            EmitSignal(SignalName.ToolCallStarted, toolName, arguments, iteration);
        }

        private void EmitToolCallCompletedSignal(string toolName, string result, int iteration)
        {
            EmitSignal(SignalName.ToolCallCompleted, toolName, result, iteration);
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
    /// This adapter properly maintains conversation context by using Gemini's chat session API.
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
            // Build a formatted prompt from all messages
            var prompt = BuildPromptFromMessages(chatMessages);
            var response = await _model.GenerateContentAsync(prompt, cancellationToken);
            var text = response.Text() ?? "";
            return new ChatCompletion(new AIChatMessage(ChatRole.Assistant, text));
        }

        public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
            IList<AIChatMessage> chatMessages,
            ChatOptions options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Build a formatted prompt from all messages
            var prompt = BuildPromptFromMessages(chatMessages);
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
        /// Builds a formatted prompt string from the messages list.
        /// This approach works well with Gemini's simple API.
        /// </summary>
        private string BuildPromptFromMessages(IList<AIChatMessage> messages)
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
                        promptBuilder.AppendLine($"System: {text}");
                        promptBuilder.AppendLine();
                        break;
                    case "user":
                        promptBuilder.AppendLine($"User: {text}");
                        promptBuilder.AppendLine();
                        break;
                    case "assistant":
                        promptBuilder.AppendLine($"Assistant: {text}");
                        promptBuilder.AppendLine();
                        break;
                    case "tool":
                        promptBuilder.AppendLine($"Tool Result: {text}");
                        promptBuilder.AppendLine();
                        break;
                }
            }
            
            // Add prompt for assistant to continue
            promptBuilder.Append("Assistant: ");
            
            return promptBuilder.ToString();
        }
    }
}
#endif
