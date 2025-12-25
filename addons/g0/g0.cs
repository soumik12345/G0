#if TOOLS
using Godot;
using System;
using G0.Models;

/// <summary>
/// Main entry point for the G0 AI Assistant plugin.
/// This EditorPlugin class manages the lifecycle of the AI chat interface in Godot Editor.
/// Provides integration with Godot's script editor for code snippet selection.
/// </summary>
[Tool]
public partial class g0 : EditorPlugin
{
    /// <summary>
    /// The main chat panel instance that provides the AI assistant interface.
    /// </summary>
    private G0.ChatPanel _chatPanel;

    /// <summary>
    /// Shortcut for sending selected code to G0 chat (Ctrl+Shift+G).
    /// </summary>
    private Shortcut _sendToG0Shortcut;

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

        // Set up keyboard shortcut
        SetupShortcut();

        // Add context menu item to script editor
        AddToolMenuItem("Send to G0 Chat", new Callable(this, MethodName.OnSendToG0MenuPressed));

        GD.Print("G0: AI Chat Panel loaded");
    }

    /// <summary>
    /// Called when the plugin is disabled. Cleans up the chat panel and resources.
    /// </summary>
    public override void _ExitTree()
    {
        // Remove menu item
        RemoveToolMenuItem("Send to G0 Chat");

        // Clean up the panel
        if (_chatPanel != null)
        {
            RemoveControlFromDocks(_chatPanel);
            _chatPanel.QueueFree();
            _chatPanel = null;
        }

        GD.Print("G0: AI Chat Panel unloaded");
    }

    /// <summary>
    /// Sets up the keyboard shortcut for sending code to G0.
    /// </summary>
    private void SetupShortcut()
    {
        var keyEvent = new InputEventKey();
        keyEvent.Keycode = Key.G;
        keyEvent.CtrlPressed = true;
        keyEvent.ShiftPressed = true;

        _sendToG0Shortcut = new Shortcut();
        var events = new Godot.Collections.Array();
        events.Add(keyEvent);
        _sendToG0Shortcut.Events = events;
    }

    /// <summary>
    /// Handles unhandled input to capture the keyboard shortcut.
    /// </summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.G && keyEvent.CtrlPressed && keyEvent.ShiftPressed)
            {
                CaptureAndSendSelection();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>
    /// Called when the "Send to G0 Chat" menu item is pressed.
    /// </summary>
    private void OnSendToG0MenuPressed()
    {
        CaptureAndSendSelection();
    }

    /// <summary>
    /// Captures the currently selected code from the script editor and sends it to the chat panel.
    /// </summary>
    private void CaptureAndSendSelection()
    {
        var scriptEditor = EditorInterface.Singleton.GetScriptEditor();
        if (scriptEditor == null)
        {
            GD.PrintErr("G0: Could not access script editor");
            return;
        }

        // Get the current script
        var currentScript = scriptEditor.GetCurrentScript();
        if (currentScript == null)
        {
            GD.Print("G0: No script currently open");
            return;
        }

        // Get the script path
        var scriptPath = currentScript.ResourcePath;
        if (string.IsNullOrEmpty(scriptPath))
        {
            GD.Print("G0: Script has no path");
            return;
        }

        // Find the current code editor
        var codeEdit = FindCurrentCodeEdit(scriptEditor);
        if (codeEdit == null)
        {
            GD.Print("G0: Could not find code editor");
            return;
        }

        // Check if there's a selection
        if (!codeEdit.HasSelection())
        {
            GD.Print("G0: No text selected. Please select code to send to G0 Chat.");
            return;
        }

        // Get selection info
        var selectedText = codeEdit.GetSelectedText();
        var startLine = codeEdit.GetSelectionFromLine() + 1; // Convert to 1-based
        var endLine = codeEdit.GetSelectionToLine() + 1;

        if (string.IsNullOrEmpty(selectedText))
        {
            GD.Print("G0: Selected text is empty");
            return;
        }

        // Create the code snippet
        var snippet = CodeSnippet.FromSelection(scriptPath, selectedText, startLine, endLine);

        // Send to chat panel
        if (_chatPanel != null)
        {
            _chatPanel.AddSnippet(snippet);
            GD.Print($"G0: Sent snippet from {snippet.GetDisplaySummary()} to chat");
        }
        else
        {
            GD.PrintErr("G0: Chat panel not available");
        }
    }

    /// <summary>
    /// Finds the currently active CodeEdit within the script editor.
    /// </summary>
    private CodeEdit FindCurrentCodeEdit(ScriptEditor scriptEditor)
    {
        // Get the current editor
        var currentEditor = scriptEditor.GetCurrentEditor();
        if (currentEditor == null)
        {
            return null;
        }

        // The ScriptEditorBase contains a CodeEdit as a child
        return FindCodeEditRecursive(currentEditor);
    }

    /// <summary>
    /// Recursively searches for a CodeEdit node in the given node's children.
    /// </summary>
    private CodeEdit FindCodeEditRecursive(Node node)
    {
        if (node is CodeEdit codeEdit)
        {
            return codeEdit;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindCodeEditRecursive(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
