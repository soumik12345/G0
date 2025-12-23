#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using G0.Models;
using G0.Documentation;

namespace G0
{
    [Tool]
    public partial class ChatPanel : Control
    {
        // UI Elements
        private VBoxContainer _mainContainer;
        private HBoxContainer _headerContainer;
        private OptionButton _modelSelector;
        private Button _settingsButton;
        private Button _clearButton;
        private ScrollContainer _scrollContainer;
        private VBoxContainer _messagesContainer;
        private RichTextLabel _currentStreamingLabel;
        private VBoxContainer _inputContainer;
        private TextEdit _inputField;
        private HBoxContainer _inputToolbar;
        private Label _charCountLabel;
        private Label _statusLabel;
        private Button _sendButton;
        private PopupMenu _contextMenu;

        // Components
        private SettingsManager _settingsManager;
        private AgentClient _agentClient;
        private GodotDocsIndexer _docsIndexer;
        private SettingsDialog _settingsDialog;

        // State
        private bool _isStreaming;
        private string _currentStreamingContent;
        private RichTextLabel _selectedMessageLabel;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(300, 400);

            // Initialize components
            _settingsManager = new SettingsManager();
            
            // Initialize documentation indexer
            _docsIndexer = new GodotDocsIndexer();
            AddChild(_docsIndexer);
            _docsIndexer.IndexingProgress += OnIndexingProgress;
            _docsIndexer.IndexingComplete += OnIndexingComplete;
            _docsIndexer.IndexingError += OnIndexingError;
            
            // Load existing documentation index
            _docsIndexer.LoadIndex();
            
            // Initialize agent client
            _agentClient = new AgentClient();
            AddChild(_agentClient);

            // Connect agent client signals
            _agentClient.ChunkReceived += OnChunkReceived;
            _agentClient.StreamComplete += OnStreamComplete;
            _agentClient.ErrorOccurred += OnErrorOccurred;
            _agentClient.ToolCalled += OnToolCalled;

            // Build UI
            BuildUI();

            // Load existing chat history
            LoadChatHistory();

            // Configure agent client with documentation
            ConfigureAgentClient();
        }
        
        private void ConfigureAgentClient()
        {
            _agentClient.Configure(_settingsManager.Settings, _docsIndexer.Index);
        }

        private void BuildUI()
        {
            // Main container
            _mainContainer = new VBoxContainer();
            _mainContainer.SetAnchorsPreset(LayoutPreset.FullRect);
            _mainContainer.AddThemeConstantOverride("separation", 8);
            AddChild(_mainContainer);

            // Header
            BuildHeader();

            // Chat display area
            BuildChatDisplay();

            // Input area
            BuildInputArea();

            // Context menu
            BuildContextMenu();

            // Settings dialog
            _settingsDialog = new SettingsDialog();
            _settingsDialog.SettingsSaved += OnSettingsSaved;
            _settingsDialog.SettingsReset += OnSettingsReset;
            _settingsDialog.DownloadDocumentationRequested += OnDownloadDocumentationRequested;
            AddChild(_settingsDialog);
        }
        
        private void OnDownloadDocumentationRequested()
        {
            StartDocumentationIndexing();
        }

        private void BuildHeader()
        {
            _headerContainer = new HBoxContainer();
            _headerContainer.AddThemeConstantOverride("separation", 4);
            _mainContainer.AddChild(_headerContainer);

            // Model selector
            _modelSelector = new OptionButton();
            _modelSelector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _modelSelector.TooltipText = "Select AI model";
            UpdateModelSelector();
            _modelSelector.ItemSelected += OnModelSelected;
            _headerContainer.AddChild(_modelSelector);

            // Settings button
            _settingsButton = new Button();
            _settingsButton.Text = "⚙";
            _settingsButton.TooltipText = "Settings";
            _settingsButton.Pressed += OnSettingsPressed;
            _headerContainer.AddChild(_settingsButton);

            // Clear button
            _clearButton = new Button();
            _clearButton.Text = "🗑";
            _clearButton.TooltipText = "Clear chat history";
            _clearButton.Pressed += OnClearPressed;
            _headerContainer.AddChild(_clearButton);
        }

        private void BuildChatDisplay()
        {
            _scrollContainer = new ScrollContainer();
            _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _scrollContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            _mainContainer.AddChild(_scrollContainer);

            _messagesContainer = new VBoxContainer();
            _messagesContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _messagesContainer.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            _messagesContainer.AddThemeConstantOverride("separation", 12);
            _scrollContainer.AddChild(_messagesContainer);
        }

        private void BuildInputArea()
        {
            _inputContainer = new VBoxContainer();
            _inputContainer.AddThemeConstantOverride("separation", 4);
            _mainContainer.AddChild(_inputContainer);

            // Status label (for showing agent activity)
            _statusLabel = new Label();
            _statusLabel.Text = "";
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            _statusLabel.Visible = false;
            _inputContainer.AddChild(_statusLabel);

            // Text input
            _inputField = new TextEdit();
            _inputField.PlaceholderText = "Type your message... (Ctrl+Enter to send)";
            _inputField.CustomMinimumSize = new Vector2(0, 80);
            _inputField.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _inputField.WrapMode = TextEdit.LineWrappingMode.Boundary;
            _inputField.TextChanged += OnInputTextChanged;
            _inputField.GuiInput += OnInputGuiInput;
            _inputContainer.AddChild(_inputField);

            // Input toolbar
            _inputToolbar = new HBoxContainer();
            _inputToolbar.AddThemeConstantOverride("separation", 8);
            _inputContainer.AddChild(_inputToolbar);

            // Character count
            _charCountLabel = new Label();
            _charCountLabel.Text = "0 chars";
            _charCountLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _charCountLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _inputToolbar.AddChild(_charCountLabel);

            // Send button
            _sendButton = new Button();
            _sendButton.Text = "Send";
            _sendButton.Pressed += OnSendPressed;
            _inputToolbar.AddChild(_sendButton);
        }

        private void BuildContextMenu()
        {
            _contextMenu = new PopupMenu();
            _contextMenu.AddItem("Copy", 0);
            _contextMenu.AddSeparator();
            _contextMenu.AddItem("Delete Message", 1);
            _contextMenu.IdPressed += OnContextMenuItemSelected;
            AddChild(_contextMenu);
        }

        private void UpdateModelSelector()
        {
            _modelSelector.Clear();
            var selectedIndex = 0;
            for (int i = 0; i < _settingsManager.Settings.AvailableModels.Count; i++)
            {
                var model = _settingsManager.Settings.AvailableModels[i];
                _modelSelector.AddItem(model);
                if (model == _settingsManager.Settings.SelectedModel)
                {
                    selectedIndex = i;
                }
            }
            _modelSelector.Selected = selectedIndex;
        }

        private void LoadChatHistory()
        {
            foreach (var message in _settingsManager.ChatHistory)
            {
                AddMessageToUI(message.Role, message.Content, false);
            }
            ScrollToBottom();
        }

        private void AddMessageToUI(string role, string content, bool save = true)
        {
            var messageContainer = new PanelContainer();
            messageContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // Style based on role
            var styleBox = new StyleBoxFlat();
            if (role == "user")
            {
                styleBox.BgColor = new Color(0.15f, 0.25f, 0.35f, 0.8f);
            }
            else
            {
                styleBox.BgColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);
            }
            styleBox.ContentMarginLeft = 12;
            styleBox.ContentMarginRight = 12;
            styleBox.ContentMarginTop = 8;
            styleBox.ContentMarginBottom = 8;
            styleBox.CornerRadiusTopLeft = 8;
            styleBox.CornerRadiusTopRight = 8;
            styleBox.CornerRadiusBottomLeft = 8;
            styleBox.CornerRadiusBottomRight = 8;
            messageContainer.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            messageContainer.AddChild(vbox);

            // Role label
            var roleLabel = new Label();
            roleLabel.Text = role == "user" ? "You" : "Assistant";
            roleLabel.AddThemeColorOverride("font_color", role == "user" 
                ? new Color(0.4f, 0.7f, 1.0f) 
                : new Color(0.6f, 0.8f, 0.6f));
            roleLabel.AddThemeFontSizeOverride("font_size", 12);
            vbox.AddChild(roleLabel);

            // Message content
            var contentLabel = new RichTextLabel();
            contentLabel.BbcodeEnabled = true;
            contentLabel.FitContent = true;
            contentLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            contentLabel.ScrollActive = false;
            contentLabel.SelectionEnabled = true;
            contentLabel.CustomMinimumSize = new Vector2(0, 20);
            // Clear and append text to ensure BBCode is parsed
            contentLabel.Clear();
            contentLabel.AppendText(MessageRenderer.RenderMessage(content));
            contentLabel.GuiInput += (InputEvent @event) => OnMessageGuiInput(@event, contentLabel, content);
            vbox.AddChild(contentLabel);

            _messagesContainer.AddChild(messageContainer);

            if (save)
            {
                _settingsManager.AddMessage(new ChatMessage(role, content));
            }
        }

        private void AddStreamingMessage()
        {
            var messageContainer = new PanelContainer();
            messageContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            messageContainer.Name = "StreamingMessage";

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.12f, 0.12f, 0.12f, 0.8f);
            styleBox.ContentMarginLeft = 12;
            styleBox.ContentMarginRight = 12;
            styleBox.ContentMarginTop = 8;
            styleBox.ContentMarginBottom = 8;
            styleBox.CornerRadiusTopLeft = 8;
            styleBox.CornerRadiusTopRight = 8;
            styleBox.CornerRadiusBottomLeft = 8;
            styleBox.CornerRadiusBottomRight = 8;
            messageContainer.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            messageContainer.AddChild(vbox);

            var roleLabel = new Label();
            roleLabel.Text = "Assistant";
            roleLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
            roleLabel.AddThemeFontSizeOverride("font_size", 12);
            vbox.AddChild(roleLabel);

            _currentStreamingLabel = new RichTextLabel();
            _currentStreamingLabel.BbcodeEnabled = true;
            _currentStreamingLabel.FitContent = true;
            _currentStreamingLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _currentStreamingLabel.ScrollActive = false;
            _currentStreamingLabel.SelectionEnabled = true;
            _currentStreamingLabel.CustomMinimumSize = new Vector2(0, 20);
            _currentStreamingLabel.Clear();
            _currentStreamingLabel.AppendText(MessageRenderer.RenderTypingIndicator());
            vbox.AddChild(_currentStreamingLabel);

            _messagesContainer.AddChild(messageContainer);
        }

        private void UpdateStreamingMessage(string content)
        {
            if (_currentStreamingLabel != null)
            {
                _currentStreamingLabel.Clear();
                _currentStreamingLabel.AppendText(MessageRenderer.RenderMessage(content));
            }
        }

        private void FinalizeStreamingMessage(string fullContent)
        {
            // Remove the streaming message container
            var streamingMessage = _messagesContainer.GetNodeOrNull("StreamingMessage");
            if (streamingMessage != null)
            {
                streamingMessage.QueueFree();
            }
            _currentStreamingLabel = null;

            // Add as a regular message
            AddMessageToUI("assistant", fullContent, true);
        }

        private void ScrollToBottom()
        {
            CallDeferred(nameof(DeferredScrollToBottom));
        }

        private void DeferredScrollToBottom()
        {
            _scrollContainer.ScrollVertical = (int)_scrollContainer.GetVScrollBar().MaxValue;
        }

        // Event Handlers
        private void OnModelSelected(long index)
        {
            var model = _modelSelector.GetItemText((int)index);
            _settingsManager.Settings.SelectedModel = model;
            _settingsManager.SaveSettings();
            // Reconfigure the agent client with the new model
            ConfigureAgentClient();
        }

        private void OnSettingsPressed()
        {
            _settingsDialog.ShowDialog(_settingsManager.Settings.Clone(), _docsIndexer);
        }

        private void OnClearPressed()
        {
            // Clear UI
            foreach (Node child in _messagesContainer.GetChildren())
            {
                child.QueueFree();
            }

            // Clear history
            _settingsManager.ClearHistory();
        }

        private void OnInputTextChanged()
        {
            var charCount = _inputField.Text.Length;
            _charCountLabel.Text = $"{charCount} chars";
        }

        private void OnInputGuiInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                if (keyEvent.Keycode == Key.Enter && keyEvent.CtrlPressed)
                {
                    OnSendPressed();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private async void OnSendPressed()
        {
            GD.Print($"G0: OnSendPressed called, _isStreaming={_isStreaming}");
            
            var message = _inputField.Text.Trim();
            if (string.IsNullOrEmpty(message))
            {
                GD.Print("G0: Message is empty, returning");
                return;
            }
            
            if (_isStreaming)
            {
                GD.Print("G0: Already streaming, returning");
                return;
            }

            // Check for API key
            if (string.IsNullOrEmpty(_settingsManager.Settings.ApiKey))
            {
                AddErrorMessage("API key is not configured. Please set it in settings.");
                return;
            }

            // Clear input
            _inputField.Text = "";
            OnInputTextChanged();

            // Add user message
            AddMessageToUI("user", message, true);
            ScrollToBottom();

            // Start streaming
            _isStreaming = true;
            _currentStreamingContent = "";
            _sendButton.Disabled = true;
            _inputField.Editable = false;

            // Add streaming message placeholder
            AddStreamingMessage();
            ScrollToBottom();

            try
            {
                // Configure client and send request
                ConfigureAgentClient();

                var messages = _settingsManager.GetMessagesForApi();
                await _agentClient.SendChatCompletionAsync(messages);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Exception in OnSendPressed: {ex.Message}\n{ex.StackTrace}");
                // Reset state on exception
                _isStreaming = false;
                _sendButton.Disabled = false;
                _inputField.Editable = true;
                
                // Remove streaming message if exists
                var streamingMessage = _messagesContainer.GetNodeOrNull("StreamingMessage");
                if (streamingMessage != null)
                {
                    streamingMessage.QueueFree();
                }
                _currentStreamingLabel = null;
                
                AddErrorMessage($"Error: {ex.Message}");
            }
        }
        
        private void OnToolCalled(string toolName, string arguments)
        {
            // Show status when agent calls a tool
            _statusLabel.Text = $"🔧 Calling {toolName}...";
            _statusLabel.Visible = true;
        }
        
        private void OnIndexingProgress(int current, int total, string currentPage)
        {
            _statusLabel.Text = $"📚 Indexing documentation: {current}/{total}";
            _statusLabel.Visible = true;
            
            // Update settings dialog if open
            _settingsDialog.UpdateDocumentationProgress(current, total, currentPage);
        }
        
        private void OnIndexingComplete(int totalEntries)
        {
            _statusLabel.Text = $"✓ Documentation indexed ({totalEntries} entries)";
            _statusLabel.Visible = true;
            
            // Update settings
            _settingsManager.Settings.DocumentationIndexed = true;
            _settingsManager.Settings.LastDocumentationUpdate = DateTime.UtcNow.ToString("o");
            _settingsManager.SaveSettings();
            
            // Reconfigure agent with new documentation
            ConfigureAgentClient();
            
            // Update settings dialog if open
            _settingsDialog.OnDocumentationComplete(totalEntries);
            
            // Hide status after a delay
            var timer = GetTree().CreateTimer(3.0);
            timer.Timeout += () => _statusLabel.Visible = false;
        }
        
        private void OnIndexingError(string errorMessage)
        {
            _statusLabel.Text = $"✗ Indexing error: {errorMessage}";
            _statusLabel.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.5f));
            _statusLabel.Visible = true;
            
            // Update settings dialog if open
            _settingsDialog.OnDocumentationError(errorMessage);
        }

        private void OnChunkReceived(string chunk)
        {
            // Hide tool status when response starts streaming
            _statusLabel.Visible = false;
            
            _currentStreamingContent += chunk;
            UpdateStreamingMessage(_currentStreamingContent);
            ScrollToBottom();
        }

        private void OnStreamComplete(string fullResponse)
        {
            GD.Print($"G0: OnStreamComplete called, response length={fullResponse?.Length ?? 0}");
            _isStreaming = false;
            _sendButton.Disabled = false;
            _inputField.Editable = true;

            FinalizeStreamingMessage(fullResponse);
            ScrollToBottom();
            GD.Print("G0: Stream complete, UI reset for next message");
        }

        private void OnErrorOccurred(string errorMessage)
        {
            GD.Print($"G0: OnErrorOccurred called: {errorMessage}");
            _isStreaming = false;
            _sendButton.Disabled = false;
            _inputField.Editable = true;

            // Remove streaming message if exists
            var streamingMessage = _messagesContainer.GetNodeOrNull("StreamingMessage");
            if (streamingMessage != null)
            {
                streamingMessage.QueueFree();
            }
            _currentStreamingLabel = null;

            AddErrorMessage(errorMessage);
            GD.Print("G0: Error handled, UI reset for next message");
        }

        private void AddErrorMessage(string errorMessage)
        {
            var errorContainer = new PanelContainer();
            errorContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.4f, 0.1f, 0.1f, 0.8f);
            styleBox.ContentMarginLeft = 12;
            styleBox.ContentMarginRight = 12;
            styleBox.ContentMarginTop = 8;
            styleBox.ContentMarginBottom = 8;
            styleBox.CornerRadiusTopLeft = 8;
            styleBox.CornerRadiusTopRight = 8;
            styleBox.CornerRadiusBottomLeft = 8;
            styleBox.CornerRadiusBottomRight = 8;
            errorContainer.AddThemeStyleboxOverride("panel", styleBox);

            var errorLabel = new RichTextLabel();
            errorLabel.BbcodeEnabled = true;
            errorLabel.FitContent = true;
            errorLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            errorLabel.ScrollActive = false;
            errorLabel.CustomMinimumSize = new Vector2(0, 20);
            errorLabel.Clear();
            errorLabel.AppendText(MessageRenderer.RenderError(errorMessage));
            errorContainer.AddChild(errorLabel);

            _messagesContainer.AddChild(errorContainer);
            ScrollToBottom();
        }

        private void OnSettingsSaved(G0Settings newSettings)
        {
            _settingsManager.UpdateSettings(newSettings);
            ConfigureAgentClient();
            UpdateModelSelector();
        }

        private void OnSettingsReset()
        {
            // Reset the settings manager to clear old settings files
            _settingsManager.ResetSettings();
            
            // Reconfigure the client with default settings
            ConfigureAgentClient();
            
            // Update the model selector
            UpdateModelSelector();
            
            GD.Print("G0: Settings have been reset");
        }

        private void OnMessageGuiInput(InputEvent @event, RichTextLabel label, string content)
        {
            if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
            {
                if (mouseEvent.ButtonIndex == MouseButton.Right)
                {
                    _selectedMessageLabel = label;
                    _contextMenu.Position = (Vector2I)GetGlobalMousePosition();
                    _contextMenu.Popup();
                }
            }
        }

        private void OnContextMenuItemSelected(long id)
        {
            switch (id)
            {
                case 0: // Copy
                    if (_selectedMessageLabel != null)
                    {
                        var selectedText = _selectedMessageLabel.GetSelectedText();
                        if (!string.IsNullOrEmpty(selectedText))
                        {
                            DisplayServer.ClipboardSet(selectedText);
                        }
                        else
                        {
                            DisplayServer.ClipboardSet(_selectedMessageLabel.Text);
                        }
                    }
                    break;
                case 1: // Delete
                    if (_selectedMessageLabel != null)
                    {
                        var messageContainer = _selectedMessageLabel.GetParent().GetParent();
                        if (messageContainer != null)
                        {
                            messageContainer.QueueFree();
                        }
                    }
                    break;
            }
            _selectedMessageLabel = null;
        }

        public override void _ExitTree()
        {
            if (_agentClient != null)
            {
                _agentClient.ChunkReceived -= OnChunkReceived;
                _agentClient.StreamComplete -= OnStreamComplete;
                _agentClient.ErrorOccurred -= OnErrorOccurred;
                _agentClient.ToolCalled -= OnToolCalled;
            }
            
            if (_docsIndexer != null)
            {
                _docsIndexer.IndexingProgress -= OnIndexingProgress;
                _docsIndexer.IndexingComplete -= OnIndexingComplete;
                _docsIndexer.IndexingError -= OnIndexingError;
            }

            if (_settingsDialog != null)
            {
                _settingsDialog.SettingsSaved -= OnSettingsSaved;
                _settingsDialog.SettingsReset -= OnSettingsReset;
                _settingsDialog.DownloadDocumentationRequested -= OnDownloadDocumentationRequested;
            }
        }
        
        /// <summary>
        /// Starts downloading and indexing the Godot documentation.
        /// Called from settings dialog.
        /// </summary>
        public async void StartDocumentationIndexing()
        {
            if (_docsIndexer.IsIndexing)
            {
                GD.Print("G0: Already indexing documentation");
                return;
            }
            
            _statusLabel.Text = "📚 Starting documentation indexing...";
            _statusLabel.Visible = true;
            
            await _docsIndexer.StartIndexingAsync();
        }
        
        /// <summary>
        /// Gets the documentation indexer for use by the settings dialog.
        /// </summary>
        public GodotDocsIndexer DocsIndexer => _docsIndexer;
    }
}
#endif

