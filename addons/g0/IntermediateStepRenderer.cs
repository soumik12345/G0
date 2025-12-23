#if TOOLS
using Godot;
using System.Text;
using System.Text.Json;
using G0.Models;

namespace G0
{
    /// <summary>
    /// Renders intermediate agent steps with BBCode formatting for display in RichTextLabel.
    /// Provides distinct styling for iterations, reasoning, tool calls, and tool results.
    /// </summary>
    public static class IntermediateStepRenderer
    {
        // Colors for different step types
        private static class StepColors
        {
            public const string Iteration = "#58A6FF";      // Blue for iteration headers
            public const string Reasoning = "#8B949E";      // Gray for reasoning text
            public const string ToolName = "#D2A8FF";       // Purple for tool names
            public const string ToolArgs = "#A5D6FF";       // Light blue for arguments
            public const string ToolResult = "#7EE787";     // Green for results
            public const string ToolError = "#F85149";      // Red for errors
            public const string Dimmed = "#6E7681";         // Dimmed gray for labels
        }

        /// <summary>
        /// Renders the header for an agent iteration step.
        /// </summary>
        public static string RenderIterationHeader(int iteration, int maxIterations)
        {
            return $"[color={StepColors.Iteration}]🔄 Agent Iteration {iteration}/{maxIterations}[/color]";
        }

        /// <summary>
        /// Renders a tool call header with the tool name.
        /// </summary>
        public static string RenderToolCallHeader(string toolName)
        {
            return $"[color={StepColors.ToolName}]🔧 Calling: {EscapeBBCode(toolName)}[/color]";
        }

        /// <summary>
        /// Renders tool call arguments in a formatted way.
        /// </summary>
        public static string RenderToolArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return $"[color={StepColors.Dimmed}]No arguments[/color]";
            }

            // Try to pretty-print JSON arguments
            var formatted = TryFormatJson(arguments);
            return $"[color={StepColors.Dimmed}]Arguments:[/color]\n[bgcolor=#1E1E1E][code][color={StepColors.ToolArgs}]{EscapeBBCode(formatted)}[/color][/code][/bgcolor]";
        }

        /// <summary>
        /// Renders a tool result.
        /// </summary>
        public static string RenderToolResult(string result, bool isError = false)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return $"[color={StepColors.Dimmed}]No result returned[/color]";
            }

            var color = isError ? StepColors.ToolError : StepColors.ToolResult;
            var icon = isError ? "❌" : "✓";
            var label = isError ? "Error" : "Result";

            // Truncate long results for display
            var displayResult = result.Length > 500 
                ? result.Substring(0, 500) + "... (truncated)" 
                : result;

            return $"[color={StepColors.Dimmed}]{icon} {label}:[/color]\n[bgcolor=#1E1E1E][code][color={color}]{EscapeBBCode(displayResult)}[/color][/code][/bgcolor]";
        }

        /// <summary>
        /// Renders reasoning text from the agent.
        /// </summary>
        public static string RenderReasoning(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            return $"[color={StepColors.Reasoning}]💭 {EscapeBBCode(text)}[/color]";
        }

        /// <summary>
        /// Renders a complete tool call step with header, arguments, and result.
        /// </summary>
        public static string RenderToolCallComplete(string toolName, string arguments, string result, bool isError = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine(RenderToolCallHeader(toolName));
            sb.AppendLine(RenderToolArguments(arguments));
            sb.AppendLine(RenderToolResult(result, isError));
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// Renders a compact summary for a collapsed step.
        /// </summary>
        public static string RenderCollapsedToolCall(string toolName, bool hasResult)
        {
            var status = hasResult ? "✓" : "...";
            return $"[color={StepColors.ToolName}]🔧 {EscapeBBCode(toolName)}[/color] [color={StepColors.Dimmed}][{status}][/color]";
        }

        /// <summary>
        /// Renders a collapsed iteration summary.
        /// </summary>
        public static string RenderCollapsedIteration(int iteration, int maxIterations, int toolCallCount)
        {
            var toolsText = toolCallCount > 0 ? $" ({toolCallCount} tool call{(toolCallCount > 1 ? "s" : "")})" : "";
            return $"[color={StepColors.Iteration}]🔄 Iteration {iteration}/{maxIterations}{toolsText}[/color]";
        }

        /// <summary>
        /// Renders a progress indicator for an ongoing operation.
        /// </summary>
        public static string RenderProgress(string operation)
        {
            return $"[color={StepColors.Dimmed}][wave amp=10 freq=3]⏳ {EscapeBBCode(operation)}...[/wave][/color]";
        }

        /// <summary>
        /// Renders the expand/collapse indicator.
        /// </summary>
        public static string RenderExpandIndicator(bool isExpanded)
        {
            return isExpanded ? "▼" : "▶";
        }

        /// <summary>
        /// Attempts to format a JSON string for better readability.
        /// </summary>
        private static string TryFormatJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            try
            {
                // Try to parse and re-serialize with indentation
                using var doc = JsonDocument.Parse(input);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
            }
            catch
            {
                // Not valid JSON, return as-is
                return input;
            }
        }

        /// <summary>
        /// Escapes BBCode special characters to prevent formatting issues.
        /// </summary>
        private static string EscapeBBCode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return text
                .Replace("[", "[lb]")
                .Replace("]", "[rb]");
        }

        /// <summary>
        /// Renders an AgentStep based on its type.
        /// </summary>
        public static string RenderStep(AgentStep step, bool isExpanded = false)
        {
            switch (step.Type)
            {
                case AgentStepType.IterationStart:
                    return RenderIterationHeader(step.Iteration, 5);

                case AgentStepType.Reasoning:
                    return isExpanded ? RenderReasoning(step.Content) : "";

                case AgentStepType.ToolCall:
                    if (isExpanded)
                    {
                        return RenderToolCallHeader(step.ToolName) + "\n" + RenderToolArguments(step.ToolArguments);
                    }
                    return RenderCollapsedToolCall(step.ToolName, false);

                case AgentStepType.ToolResult:
                    if (isExpanded)
                    {
                        var isError = step.Content?.StartsWith("Error") ?? false;
                        return RenderToolResult(step.Content, isError);
                    }
                    return RenderCollapsedToolCall(step.ToolName, true);

                default:
                    return step.Content ?? "";
            }
        }
    }
}
#endif

