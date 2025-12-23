#if TOOLS
using Godot;
using System;
using G0.Models;
using G0.Documentation;

namespace G0
{
    [Tool]
    public partial class SettingsDialog : AcceptDialog
    {
        // Use C# event instead of Godot signal for custom class types
        public event Action<G0Settings> SettingsSaved;
        public event Action SettingsReset;
        public event Action DownloadDocumentationRequested;

        private TabContainer _tabContainer;
        
        // API Tab
        private VBoxContainer _apiContainer;
        private OptionButton _providerSelector;
        private LineEdit _apiKeyInput;
        private LineEdit _endpointInput;
        private TextEdit _modelsInput;
        private Button _testConnectionButton;
        private Label _connectionStatusLabel;
        
        // Agent Tab
        private VBoxContainer _agentContainer;
        private CheckBox _useAgentCheckbox;
        private SpinBox _maxIterationsInput;
        private TextEdit _systemPromptInput;
        private Button _downloadDocsButton;
        private Label _docsStatusLabel;
        private ProgressBar _docsProgressBar;
        private LineEdit _serperApiKeyInput;
        
        // General Tab
        private VBoxContainer _generalContainer;
        private SpinBox _maxHistoryInput;
        private Button _resetSettingsButton;

        private G0Settings _currentSettings;
        private GodotDocsIndexer _docsIndexer;

        public override void _Ready()
        {
            Title = "G0 Settings";
            Size = new Vector2I(520, 620);
            Exclusive = true;
            Unresizable = false;

            // Tab container
            _tabContainer = new TabContainer();
            _tabContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _tabContainer.CustomMinimumSize = new Vector2(500, 550);
            AddChild(_tabContainer);

            // Build tabs
            BuildApiTab();
            BuildAgentTab();
            BuildGeneralTab();

            // Dialog buttons
            OkButtonText = "Save";
            AddCancelButton("Cancel");

            Confirmed += OnConfirmed;
        }

        private void BuildApiTab()
        {
            _apiContainer = new VBoxContainer();
            _apiContainer.Name = "API";
            _apiContainer.AddThemeConstantOverride("separation", 12);
            _tabContainer.AddChild(_apiContainer);

            // Provider selection
            var providerContainer = new VBoxContainer();
            providerContainer.AddThemeConstantOverride("separation", 4);
            _apiContainer.AddChild(providerContainer);

            var providerLabel = new Label();
            providerLabel.Text = "AI Provider:";
            providerContainer.AddChild(providerLabel);

            _providerSelector = new OptionButton();
            _providerSelector.AddItem("Google Gemini", (int)ModelProvider.Gemini);
            _providerSelector.AddItem("OpenAI", (int)ModelProvider.OpenAI);
            _providerSelector.AddItem("Azure OpenAI", (int)ModelProvider.AzureOpenAI);
            _providerSelector.ItemSelected += OnProviderSelected;
            providerContainer.AddChild(_providerSelector);

            // API Key
            var apiKeyContainer = new VBoxContainer();
            apiKeyContainer.AddThemeConstantOverride("separation", 4);
            _apiContainer.AddChild(apiKeyContainer);

            var apiKeyLabel = new Label();
            apiKeyLabel.Text = "API Key:";
            apiKeyContainer.AddChild(apiKeyLabel);

            var apiKeyHbox = new HBoxContainer();
            apiKeyHbox.AddThemeConstantOverride("separation", 4);
            apiKeyContainer.AddChild(apiKeyHbox);

            _apiKeyInput = new LineEdit();
            _apiKeyInput.PlaceholderText = "Enter your API key...";
            _apiKeyInput.TooltipText = "Your API key for the selected provider";
            _apiKeyInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _apiKeyInput.Secret = true;
            apiKeyHbox.AddChild(_apiKeyInput);

            var toggleButton = new Button();
            toggleButton.Text = "👁";
            toggleButton.TooltipText = "Toggle visibility";
            toggleButton.Pressed += () => _apiKeyInput.Secret = !_apiKeyInput.Secret;
            apiKeyHbox.AddChild(toggleButton);

            // Endpoint (for Azure OpenAI)
            var endpointContainer = new VBoxContainer();
            endpointContainer.Name = "EndpointContainer";
            endpointContainer.AddThemeConstantOverride("separation", 4);
            _apiContainer.AddChild(endpointContainer);

            var endpointLabel = new Label();
            endpointLabel.Text = "Endpoint URL (Azure only):";
            endpointContainer.AddChild(endpointLabel);

            _endpointInput = new LineEdit();
            _endpointInput.PlaceholderText = "https://your-resource.openai.azure.com/";
            _endpointInput.TooltipText = "Your Azure OpenAI endpoint URL";
            _endpointInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            endpointContainer.AddChild(_endpointInput);

            // Models
            var modelsContainer = new VBoxContainer();
            modelsContainer.AddThemeConstantOverride("separation", 4);
            _apiContainer.AddChild(modelsContainer);

            var modelsLabel = new Label();
            modelsLabel.Text = "Available Models (one per line):";
            modelsContainer.AddChild(modelsLabel);

            _modelsInput = new TextEdit();
            _modelsInput.PlaceholderText = "gemini-2.5-flash\ngemini-2.0-flash";
            _modelsInput.TooltipText = "List of available models, one per line";
            _modelsInput.CustomMinimumSize = new Vector2(0, 80);
            _modelsInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _modelsInput.WrapMode = TextEdit.LineWrappingMode.None;
            modelsContainer.AddChild(_modelsInput);

            // Test Connection
            var testContainer = new HBoxContainer();
            testContainer.AddThemeConstantOverride("separation", 8);
            _apiContainer.AddChild(testContainer);

            _testConnectionButton = new Button();
            _testConnectionButton.Text = "Test Connection";
            _testConnectionButton.Pressed += OnTestConnectionPressed;
            testContainer.AddChild(_testConnectionButton);

            _connectionStatusLabel = new Label();
            _connectionStatusLabel.Text = "";
            _connectionStatusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            testContainer.AddChild(_connectionStatusLabel);
        }

        private void BuildAgentTab()
        {
            _agentContainer = new VBoxContainer();
            _agentContainer.Name = "Agent";
            _agentContainer.AddThemeConstantOverride("separation", 12);
            _tabContainer.AddChild(_agentContainer);

            // Use Agent checkbox
            _useAgentCheckbox = new CheckBox();
            _useAgentCheckbox.Text = "Enable AI Agent with Tool Calling";
            _useAgentCheckbox.TooltipText = "When enabled, the AI can automatically search Godot documentation to provide accurate answers";
            _agentContainer.AddChild(_useAgentCheckbox);

            // Max Iterations section
            var iterationsSection = new VBoxContainer();
            iterationsSection.AddThemeConstantOverride("separation", 4);
            _agentContainer.AddChild(iterationsSection);

            var iterationsLabel = new Label();
            iterationsLabel.Text = "Max Agent Iterations:";
            iterationsSection.AddChild(iterationsLabel);

            var iterationsDesc = new Label();
            iterationsDesc.Text = "Maximum reasoning and tool-calling loops per request.";
            iterationsDesc.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            iterationsDesc.AddThemeFontSizeOverride("font_size", 12);
            iterationsSection.AddChild(iterationsDesc);

            _maxIterationsInput = new SpinBox();
            _maxIterationsInput.MinValue = 1;
            _maxIterationsInput.MaxValue = 100;
            _maxIterationsInput.Step = 1;
            _maxIterationsInput.Value = 50;
            _maxIterationsInput.TooltipText = "Maximum iterations for agent tool-calling loop (1-100)";
            _maxIterationsInput.CustomMinimumSize = new Vector2(100, 0);
            iterationsSection.AddChild(_maxIterationsInput);

            // Documentation section
            var docsContainer = new VBoxContainer();
            docsContainer.AddThemeConstantOverride("separation", 8);
            _agentContainer.AddChild(docsContainer);

            var docsLabel = new Label();
            docsLabel.Text = "Godot Documentation:";
            docsContainer.AddChild(docsLabel);

            var docsDesc = new Label();
            docsDesc.Text = "Download the Godot documentation for the agent to search.";
            docsDesc.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            docsDesc.AddThemeFontSizeOverride("font_size", 12);
            docsContainer.AddChild(docsDesc);

            var docsButtonContainer = new HBoxContainer();
            docsButtonContainer.AddThemeConstantOverride("separation", 8);
            docsContainer.AddChild(docsButtonContainer);

            _downloadDocsButton = new Button();
            _downloadDocsButton.Text = "Download Documentation";
            _downloadDocsButton.Pressed += OnDownloadDocsPressed;
            docsButtonContainer.AddChild(_downloadDocsButton);

            _docsStatusLabel = new Label();
            _docsStatusLabel.Text = "";
            _docsStatusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            docsButtonContainer.AddChild(_docsStatusLabel);

            _docsProgressBar = new ProgressBar();
            _docsProgressBar.MinValue = 0;
            _docsProgressBar.MaxValue = 100;
            _docsProgressBar.Value = 0;
            _docsProgressBar.Visible = false;
            _docsProgressBar.CustomMinimumSize = new Vector2(0, 20);
            docsContainer.AddChild(_docsProgressBar);

            // Web Search section (Serper API)
            var webSearchContainer = new VBoxContainer();
            webSearchContainer.AddThemeConstantOverride("separation", 4);
            _agentContainer.AddChild(webSearchContainer);

            var webSearchLabel = new Label();
            webSearchLabel.Text = "Web Search (Serper API):";
            webSearchContainer.AddChild(webSearchLabel);

            var webSearchDesc = new Label();
            webSearchDesc.Text = "Get a free API key at serper.dev to enable web search.";
            webSearchDesc.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            webSearchDesc.AddThemeFontSizeOverride("font_size", 12);
            webSearchContainer.AddChild(webSearchDesc);

            var serperKeyHbox = new HBoxContainer();
            serperKeyHbox.AddThemeConstantOverride("separation", 4);
            webSearchContainer.AddChild(serperKeyHbox);

            _serperApiKeyInput = new LineEdit();
            _serperApiKeyInput.PlaceholderText = "Enter your Serper API key...";
            _serperApiKeyInput.TooltipText = "API key from serper.dev for web search";
            _serperApiKeyInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _serperApiKeyInput.Secret = true;
            serperKeyHbox.AddChild(_serperApiKeyInput);

            var serperToggleButton = new Button();
            serperToggleButton.Text = "👁";
            serperToggleButton.TooltipText = "Toggle visibility";
            serperToggleButton.Pressed += () => _serperApiKeyInput.Secret = !_serperApiKeyInput.Secret;
            serperKeyHbox.AddChild(serperToggleButton);

            // System Prompt
            var promptContainer = new VBoxContainer();
            promptContainer.AddThemeConstantOverride("separation", 4);
            _agentContainer.AddChild(promptContainer);

            var promptLabel = new Label();
            promptLabel.Text = "System Prompt:";
            promptContainer.AddChild(promptLabel);

            _systemPromptInput = new TextEdit();
            _systemPromptInput.PlaceholderText = "Enter the system prompt for the AI agent...";
            _systemPromptInput.TooltipText = "The system prompt defines how the AI agent should behave";
            _systemPromptInput.CustomMinimumSize = new Vector2(0, 120);
            _systemPromptInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _systemPromptInput.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _systemPromptInput.WrapMode = TextEdit.LineWrappingMode.Boundary;
            promptContainer.AddChild(_systemPromptInput);
        }

        private void BuildGeneralTab()
        {
            _generalContainer = new VBoxContainer();
            _generalContainer.Name = "General";
            _generalContainer.AddThemeConstantOverride("separation", 12);
            _tabContainer.AddChild(_generalContainer);

            // Max History
            var historyContainer = new HBoxContainer();
            historyContainer.AddThemeConstantOverride("separation", 8);
            _generalContainer.AddChild(historyContainer);

            var historyLabel = new Label();
            historyLabel.Text = "Max Chat History:";
            historyContainer.AddChild(historyLabel);

            _maxHistoryInput = new SpinBox();
            _maxHistoryInput.MinValue = 10;
            _maxHistoryInput.MaxValue = 1000;
            _maxHistoryInput.Step = 10;
            _maxHistoryInput.Value = 100;
            _maxHistoryInput.TooltipText = "Maximum number of messages to keep in history";
            _maxHistoryInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            historyContainer.AddChild(_maxHistoryInput);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _generalContainer.AddChild(spacer);

            // Reset section
            var separator = new HSeparator();
            _generalContainer.AddChild(separator);

            var resetContainer = new VBoxContainer();
            resetContainer.AddThemeConstantOverride("separation", 8);
            _generalContainer.AddChild(resetContainer);

            var warningLabel = new Label();
            warningLabel.Text = "Reset to clear old settings and start fresh:";
            warningLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            warningLabel.AddThemeFontSizeOverride("font_size", 12);
            resetContainer.AddChild(warningLabel);

            _resetSettingsButton = new Button();
            _resetSettingsButton.Text = "Reset All Settings";
            _resetSettingsButton.TooltipText = "Clear all settings and restore defaults (API key will be cleared)";
            _resetSettingsButton.Pressed += OnResetSettingsPressed;
            resetContainer.AddChild(_resetSettingsButton);
        }

        private void OnProviderSelected(long index)
        {
            var provider = (ModelProvider)_providerSelector.GetItemId((int)index);
            
            // Show/hide endpoint field based on provider
            var endpointContainer = _apiContainer.GetNode<VBoxContainer>("EndpointContainer");
            endpointContainer.Visible = provider == ModelProvider.AzureOpenAI;

            // Update placeholder text and models
            switch (provider)
            {
                case ModelProvider.OpenAI:
                    _apiKeyInput.PlaceholderText = "sk-...";
                    _modelsInput.PlaceholderText = "gpt-4o\ngpt-4o-mini\ngpt-3.5-turbo";
                    break;
                case ModelProvider.AzureOpenAI:
                    _apiKeyInput.PlaceholderText = "Your Azure OpenAI key...";
                    _modelsInput.PlaceholderText = "gpt-4o\ngpt-4o-mini";
                    break;
                case ModelProvider.Gemini:
                default:
                    _apiKeyInput.PlaceholderText = "AIza...";
                    _modelsInput.PlaceholderText = "gemini-2.5-flash\ngemini-2.0-flash\ngemini-1.5-pro";
                    break;
            }
        }

        private void OnDownloadDocsPressed()
        {
            _downloadDocsButton.Disabled = true;
            _docsStatusLabel.Text = "Starting...";
            _docsProgressBar.Value = 0;
            _docsProgressBar.Visible = true;
            
            DownloadDocumentationRequested?.Invoke();
        }

        public void UpdateDocumentationProgress(int current, int total, string currentPage)
        {
            _docsProgressBar.MaxValue = total;
            _docsProgressBar.Value = current;
            _docsStatusLabel.Text = $"{current}/{total}";
        }

        public void OnDocumentationComplete(int totalEntries)
        {
            _downloadDocsButton.Disabled = false;
            _docsProgressBar.Visible = false;
            _docsStatusLabel.Text = $"✓ {totalEntries} entries indexed";
            _docsStatusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1, 0.5f));
        }

        public void OnDocumentationError(string error)
        {
            _downloadDocsButton.Disabled = false;
            _docsProgressBar.Visible = false;
            _docsStatusLabel.Text = $"✗ Error: {error}";
            _docsStatusLabel.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.5f));
        }

        private void OnResetSettingsPressed()
        {
            var confirmDialog = new ConfirmationDialog();
            confirmDialog.Title = "Reset Settings";
            confirmDialog.DialogText = "This will clear all settings including your API key and restore defaults.\n\nAre you sure?";
            confirmDialog.OkButtonText = "Reset";
            confirmDialog.Confirmed += () =>
            {
                var defaultSettings = new G0Settings();
                PopulateFields(defaultSettings);
                _currentSettings = defaultSettings;
                
                _connectionStatusLabel.Text = "Settings reset";
                _connectionStatusLabel.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.4f));
                
                SettingsReset?.Invoke();
                SettingsSaved?.Invoke(defaultSettings);
                
                confirmDialog.QueueFree();
            };
            confirmDialog.Canceled += () => confirmDialog.QueueFree();
            
            AddChild(confirmDialog);
            confirmDialog.PopupCentered();
        }

        public void ShowDialog(G0Settings settings, GodotDocsIndexer docsIndexer = null)
        {
            _currentSettings = settings;
            _docsIndexer = docsIndexer;
            
            PopulateFields(settings);

            // Connect to docs indexer if available
            if (_docsIndexer != null)
            {
                // Update docs status
                if (_docsIndexer.IndexExists() && _docsIndexer.Index != null)
                {
                    _docsStatusLabel.Text = $"✓ {_docsIndexer.Index.Entries.Count} entries";
                    _docsStatusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1, 0.5f));
                }
                else
                {
                    _docsStatusLabel.Text = "Not downloaded";
                    _docsStatusLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
                }
            }

            PopupCentered();
        }

        private void PopulateFields(G0Settings settings)
        {
            // API Tab
            _providerSelector.Selected = (int)settings.Provider;
            OnProviderSelected(_providerSelector.Selected);
            _apiKeyInput.Text = settings.ApiKey;
            _endpointInput.Text = settings.ModelEndpoint;
            _modelsInput.Text = string.Join("\n", settings.AvailableModels);
            _connectionStatusLabel.Text = "";

            // Agent Tab
            _useAgentCheckbox.ButtonPressed = settings.UseAgent;
            _maxIterationsInput.Value = settings.MaxAgentIterations;
            _systemPromptInput.Text = settings.SystemPrompt;
            _serperApiKeyInput.Text = settings.SerperApiKey;

            // General Tab
            _maxHistoryInput.Value = settings.MaxHistorySize;
        }

        private void OnConfirmed()
        {
            var newSettings = new G0Settings
            {
                Provider = (ModelProvider)_providerSelector.GetSelectedId(),
                ApiKey = _apiKeyInput.Text.Trim(),
                ModelEndpoint = _endpointInput.Text.Trim(),
                MaxHistorySize = (int)_maxHistoryInput.Value,
                SelectedModel = _currentSettings.SelectedModel,
                UseAgent = _useAgentCheckbox.ButtonPressed,
                MaxAgentIterations = (int)_maxIterationsInput.Value,
                SystemPrompt = _systemPromptInput.Text.Trim(),
                SerperApiKey = _serperApiKeyInput.Text.Trim(),
                DocumentationIndexed = _currentSettings.DocumentationIndexed,
                LastDocumentationUpdate = _currentSettings.LastDocumentationUpdate
            };

            // Parse models
            var modelsText = _modelsInput.Text.Trim();
            if (!string.IsNullOrEmpty(modelsText))
            {
                newSettings.AvailableModels.Clear();
                var lines = modelsText.Split('\n');
                foreach (var line in lines)
                {
                    var model = line.Trim();
                    if (!string.IsNullOrEmpty(model))
                    {
                        newSettings.AvailableModels.Add(model);
                    }
                }
            }

            // Validate selected model is in list
            if (!newSettings.AvailableModels.Contains(newSettings.SelectedModel) 
                && newSettings.AvailableModels.Count > 0)
            {
                newSettings.SelectedModel = newSettings.AvailableModels[0];
            }

            SettingsSaved?.Invoke(newSettings);
        }

        private async void OnTestConnectionPressed()
        {
            _testConnectionButton.Disabled = true;
            _connectionStatusLabel.Text = "Testing...";
            _connectionStatusLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0.5f));

            try
            {
                var apiKey = _apiKeyInput.Text.Trim();
                var provider = (ModelProvider)_providerSelector.GetSelectedId();

                if (string.IsNullOrEmpty(apiKey))
                {
                    _connectionStatusLabel.Text = "Please enter API key";
                    _connectionStatusLabel.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.5f));
                    _testConnectionButton.Disabled = false;
                    return;
                }

                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                string testUrl;
                switch (provider)
                {
                    case ModelProvider.OpenAI:
                        testUrl = "https://api.openai.com/v1/models";
                        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                        break;
                    case ModelProvider.AzureOpenAI:
                        var endpoint = _endpointInput.Text.Trim().TrimEnd('/');
                        if (string.IsNullOrEmpty(endpoint))
                        {
                            _connectionStatusLabel.Text = "Please enter endpoint URL";
                            _connectionStatusLabel.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.5f));
                            _testConnectionButton.Disabled = false;
                            return;
                        }
                        testUrl = $"{endpoint}/openai/models?api-version=2024-02-15-preview";
                        httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
                        break;
                    case ModelProvider.Gemini:
                    default:
                        testUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                        break;
                }

                var response = await httpClient.GetAsync(testUrl);

                if (response.IsSuccessStatusCode)
                {
                    _connectionStatusLabel.Text = "✓ Connection successful!";
                    _connectionStatusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1, 0.5f));
                }
                else
                {
                    _connectionStatusLabel.Text = $"✗ Error: {response.StatusCode}";
                    _connectionStatusLabel.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.5f));
                }
            }
            catch (Exception ex)
            {
                _connectionStatusLabel.Text = $"✗ Failed: {ex.Message}";
                _connectionStatusLabel.AddThemeColorOverride("font_color", new Color(1, 0.5f, 0.5f));
            }

            _testConnectionButton.Disabled = false;
        }
    }
}
#endif
