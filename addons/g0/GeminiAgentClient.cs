#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using GenerativeAI;
using G0.Models;

namespace G0
{
    public partial class GeminiAgentClient : Node
    {
        [Signal]
        public delegate void ChunkReceivedEventHandler(string chunk);

        [Signal]
        public delegate void StreamCompleteEventHandler(string fullResponse);

        [Signal]
        public delegate void ErrorOccurredEventHandler(string errorMessage);

        private string _apiKey;
        private string _modelId;
        private GoogleAi _googleAI;
        private GenerativeModel _model;
        private StringBuilder _currentResponse;
        private bool _isStreaming;

        public bool IsStreaming => _isStreaming;

        public override void _Ready()
        {
            _currentResponse = new StringBuilder();
        }

        public void Configure(string apiKey, string modelId)
        {
            _apiKey = apiKey;
            _modelId = modelId;
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                try
                {
                    _googleAI = new GoogleAi(_apiKey);
                    _model = _googleAI.CreateGenerativeModel($"models/{_modelId}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"G0: Failed to initialize Gemini model: {ex.Message}");
                    _model = null;
                    _googleAI = null;
                }
            }
        }

        public async Task SendChatCompletionAsync(List<ChatMessage> messages)
        {
            GD.Print($"G0: SendChatCompletionAsync called with {messages.Count} messages");
            
            if (_isStreaming)
            {
                GD.PrintErr("G0: Already streaming a response");
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                GD.PrintErr("G0: API key is empty");
                EmitSignal(SignalName.ErrorOccurred, "API key is not configured. Please set it in settings.");
                return;
            }

            if (_model == null)
            {
                GD.Print($"G0: Model is null, configuring with model ID: {_modelId}");
                Configure(_apiKey, _modelId);
                if (_model == null)
                {
                    GD.PrintErr("G0: Failed to create model after configuration");
                    EmitSignal(SignalName.ErrorOccurred, "Failed to initialize Gemini model.");
                    return;
                }
            }

            _isStreaming = true;
            _currentResponse.Clear();

            try
            {
                GD.Print("G0: Starting stream...");
                await StreamChatAsync(messages);
            }
            catch (Exception ex)
            {
                _isStreaming = false;
                GD.PrintErr($"G0: Request exception: {ex.Message}\n{ex.StackTrace}");
                CallDeferred(nameof(EmitErrorSignal), $"Request failed: {ex.Message}");
            }
        }

        private async Task StreamChatAsync(List<ChatMessage> messages)
        {
            try
            {
                // Build the prompt with conversation history
                var promptBuilder = new StringBuilder();
                
                // Add conversation history as context
                if (messages.Count > 1)
                {
                    promptBuilder.AppendLine("Previous conversation:");
                    for (int i = 0; i < messages.Count - 1; i++)
                    {
                        var msg = messages[i];
                        var roleLabel = msg.Role == "user" ? "User" : "Assistant";
                        promptBuilder.AppendLine($"{roleLabel}: {msg.Content}");
                    }
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine("Continue the conversation. User's new message:");
                }
                
                // Add the current message
                var lastMessage = messages[messages.Count - 1];
                promptBuilder.Append(lastMessage.Content);
                
                var fullPrompt = promptBuilder.ToString();
                GD.Print($"G0: Sending prompt ({fullPrompt.Length} chars)");
                
                // Use streaming for the response
                var streamingResponse = _model.StreamContentAsync(fullPrompt);
                
                await foreach (var chunk in streamingResponse)
                {
                    var text = chunk.Text();
                    if (!string.IsNullOrEmpty(text))
                    {
                        _currentResponse.Append(text);
                        CallDeferred(nameof(EmitChunkSignal), text);
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
                GD.PrintErr($"G0: Streaming error: {ex.Message}\n{ex.StackTrace}");
                CallDeferred(nameof(EmitErrorSignal), $"Streaming failed: {ex.Message}");
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

        public void CancelRequest()
        {
            _isStreaming = false;
        }
    }
}
#endif

