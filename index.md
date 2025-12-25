# G0 - AI Assistant for Godot

Welcome to the G0 documentation. G0 is an AI assistant Godot Editor plugin that provides an agentic AI chat interface directly within the Godot editor.

## Features

- **Multi-Provider AI Support**: Integrates with Gemini, OpenAI, and Azure OpenAI
- **Godot Documentation Search**: Built-in semantic search through Godot documentation
- **Web Search Integration**: Access to real-time web search via Serper API
- **Code Snippet Integration**: Select and attach code directly from Godot's script editor for context-aware analysis
- **Agentic Architecture**: AI autonomously uses tools to provide comprehensive assistance
- **Real-time Transparency**: See agent reasoning and tool execution in real-time

## Quick Start

1. Install the plugin in your Godot project
2. Configure your AI provider and API keys in the settings
3. Access the chat interface from the right dock in Godot Editor
4. Select code in the script editor and attach it as context for code-specific queries
5. Start asking questions about your project or Godot development

## Architecture Overview

The plugin is built around several core components:

- **[AgentClient](xref:G0.AgentClient)**: Main orchestrator implementing the agentic loop
- **[ChatPanel](xref:G0.ChatPanel)**: Primary UI component in Godot's dock
- **Tools System**: AI function calling framework for specialized tasks

## API Reference

Browse the complete [API documentation](api/index.md) for detailed information about all classes and methods.