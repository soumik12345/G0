#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using G0.Models;

namespace G0
{
    public partial class SettingsManager : RefCounted
    {
        private const string SettingsPath = "user://g0_settings.cfg";
        private const string HistoryPath = "user://g0_history.json";

        public G0Settings Settings { get; private set; } = new G0Settings();
        public List<ChatMessage> ChatHistory { get; private set; } = new List<ChatMessage>();

        public SettingsManager()
        {
            LoadSettings();
            LoadChatHistory();
        }

        public void LoadSettings()
        {
            var config = new ConfigFile();
            var error = config.Load(SettingsPath);

            if (error == Error.Ok)
            {
                Settings.ApiKey = (string)config.GetValue("api", "key", Settings.ApiKey);
                Settings.SelectedModel = (string)config.GetValue("api", "model", Settings.SelectedModel);
                Settings.MaxHistorySize = (int)config.GetValue("settings", "max_history", Settings.MaxHistorySize);

                var modelsArray = config.GetValue("api", "available_models", new Godot.Collections.Array());
                if (modelsArray.VariantType == Variant.Type.Array)
                {
                    var arr = modelsArray.AsGodotArray();
                    if (arr.Count > 0)
                    {
                        Settings.AvailableModels.Clear();
                        foreach (var model in arr)
                        {
                            Settings.AvailableModels.Add(model.AsString());
                        }
                    }
                }

                // Load agent settings
                var providerInt = (int)config.GetValue("agent", "provider", (int)Settings.Provider);
                Settings.Provider = (ModelProvider)providerInt;
                Settings.ModelEndpoint = (string)config.GetValue("agent", "endpoint", Settings.ModelEndpoint);
                Settings.UseAgent = (bool)config.GetValue("agent", "use_agent", Settings.UseAgent);
                Settings.MaxAgentIterations = (int)config.GetValue("agent", "max_iterations", Settings.MaxAgentIterations);
                Settings.SystemPrompt = (string)config.GetValue("agent", "system_prompt", Settings.SystemPrompt);
                Settings.SerperApiKey = (string)config.GetValue("agent", "serper_api_key", Settings.SerperApiKey);
                Settings.DocumentationIndexed = (bool)config.GetValue("documentation", "indexed", Settings.DocumentationIndexed);
                Settings.LastDocumentationUpdate = (string)config.GetValue("documentation", "last_update", Settings.LastDocumentationUpdate);
            }
        }

        public void SaveSettings()
        {
            var config = new ConfigFile();

            config.SetValue("api", "key", Settings.ApiKey);
            config.SetValue("api", "model", Settings.SelectedModel);
            config.SetValue("settings", "max_history", Settings.MaxHistorySize);

            var modelsArray = new Godot.Collections.Array();
            foreach (var model in Settings.AvailableModels)
            {
                modelsArray.Add(model);
            }
            config.SetValue("api", "available_models", modelsArray);

            // Save agent settings
            config.SetValue("agent", "provider", (int)Settings.Provider);
            config.SetValue("agent", "endpoint", Settings.ModelEndpoint);
            config.SetValue("agent", "use_agent", Settings.UseAgent);
            config.SetValue("agent", "max_iterations", Settings.MaxAgentIterations);
            config.SetValue("agent", "system_prompt", Settings.SystemPrompt);
            config.SetValue("agent", "serper_api_key", Settings.SerperApiKey);
            config.SetValue("documentation", "indexed", Settings.DocumentationIndexed);
            config.SetValue("documentation", "last_update", Settings.LastDocumentationUpdate);

            var error = config.Save(SettingsPath);
            if (error != Error.Ok)
            {
                GD.PrintErr($"G0: Failed to save settings: {error}");
            }
        }

        public void LoadChatHistory()
        {
            ChatHistory.Clear();

            if (!FileAccess.FileExists(HistoryPath))
            {
                return;
            }

            using var file = FileAccess.Open(HistoryPath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"G0: Failed to open chat history file");
                return;
            }

            var jsonString = file.GetAsText();
            if (string.IsNullOrEmpty(jsonString))
            {
                return;
            }

            var json = new Json();
            var parseResult = json.Parse(jsonString);

            if (parseResult != Error.Ok)
            {
                GD.PrintErr($"G0: Failed to parse chat history JSON: {json.GetErrorMessage()}");
                return;
            }

            var data = json.Data.AsGodotArray();
            foreach (var item in data)
            {
                var dict = item.AsGodotDictionary();
                ChatHistory.Add(ChatMessage.FromDictionary(dict));
            }

            // Trim to max size
            while (ChatHistory.Count > Settings.MaxHistorySize)
            {
                ChatHistory.RemoveAt(0);
            }
        }

        public void SaveChatHistory()
        {
            // Trim to max size before saving
            while (ChatHistory.Count > Settings.MaxHistorySize)
            {
                ChatHistory.RemoveAt(0);
            }

            var historyArray = new Godot.Collections.Array();
            foreach (var message in ChatHistory)
            {
                historyArray.Add(message.ToDictionary());
            }

            var jsonString = Json.Stringify(historyArray, "\t");

            using var file = FileAccess.Open(HistoryPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"G0: Failed to open chat history file for writing");
                return;
            }

            file.StoreString(jsonString);
        }

        public void AddMessage(ChatMessage message)
        {
            ChatHistory.Add(message);
            SaveChatHistory();
        }

        public void ClearHistory()
        {
            ChatHistory.Clear();
            SaveChatHistory();
        }

        public void UpdateSettings(G0Settings newSettings)
        {
            Settings = newSettings;
            SaveSettings();
        }

        public void ResetSettings()
        {
            // Delete the settings file if it exists
            if (FileAccess.FileExists(SettingsPath))
            {
                var error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SettingsPath));
                if (error != Error.Ok)
                {
                    GD.PrintErr($"G0: Failed to delete settings file: {error}");
                }
            }

            // Reset to default settings
            Settings = new G0Settings();
            SaveSettings();
            
            GD.Print("G0: Settings have been reset to defaults");
        }

        public List<ChatMessage> GetMessagesForApi()
        {
            return new List<ChatMessage>(ChatHistory);
        }
    }
}
#endif

