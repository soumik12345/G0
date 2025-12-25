#if TOOLS
using Godot;
using System;

/// <summary>
/// Main entry point for the G0 AI Assistant plugin.
/// This EditorPlugin class manages the lifecycle of the AI chat interface in Godot Editor.
/// </summary>
[Tool]
public partial class g0 : EditorPlugin
{
    /// <summary>
    /// The main chat panel instance that provides the AI assistant interface.
    /// </summary>
    private G0.ChatPanel _chatPanel;

    /// <summary>
    /// Called when the plugin is enabled. Sets up the chat panel in Godot's dock.
    /// </summary>
    public override void _EnterTree()
    {
        // Create the chat panel
        _chatPanel = new G0.ChatPanel();
        _chatPanel.Name = "G0 Chat";

        // Add it to the right dock (upper-left slot of right panel)
        AddControlToDock(DockSlot.RightUl, _chatPanel);

        GD.Print("G0: AI Chat Panel loaded");
    }

    /// <summary>
    /// Called when the plugin is disabled. Cleans up the chat panel and resources.
    /// </summary>
    public override void _ExitTree()
    {
        // Clean up the panel
        if (_chatPanel != null)
        {
            RemoveControlFromDocks(_chatPanel);
            _chatPanel.QueueFree();
            _chatPanel = null;
        }

        GD.Print("G0: AI Chat Panel unloaded");
    }
}
#endif
