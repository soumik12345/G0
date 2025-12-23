#if TOOLS
using Godot;
using System;

[Tool]
public partial class g0 : EditorPlugin
{
    private G0.ChatPanel _chatPanel;

    public override void _EnterTree()
    {
        // Create the chat panel
        _chatPanel = new G0.ChatPanel();
        _chatPanel.Name = "G0 Chat";

        // Add it to the right dock (upper-left slot of right panel)
        AddControlToDock(DockSlot.RightUl, _chatPanel);

        GD.Print("G0: AI Chat Panel loaded");
    }

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
