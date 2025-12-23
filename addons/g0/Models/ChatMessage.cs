#if TOOLS
using System;
using System.Collections.Generic;

namespace G0.Models
{
    /// <summary>
    /// Represents the type of an agent step in the agentic loop.
    /// </summary>
    public enum AgentStepType
    {
        IterationStart,   // Start of a new agent iteration
        Reasoning,        // Agent reasoning/thinking text
        ToolCall,         // Tool invocation with arguments
        ToolResult        // Result from tool execution
    }

    /// <summary>
    /// Represents a single step in the agent's reasoning/execution process.
    /// </summary>
    public class AgentStep
    {
        public AgentStepType Type { get; set; }
        public int Iteration { get; set; }
        public string ToolName { get; set; }
        public string ToolArguments { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsCollapsed { get; set; } = true;

        public AgentStep() { }

        public AgentStep(AgentStepType type, string content, int iteration = 0)
        {
            Type = type;
            Content = content;
            Iteration = iteration;
            Timestamp = DateTime.Now;
        }

        public static AgentStep CreateIterationStart(int iteration)
        {
            return new AgentStep
            {
                Type = AgentStepType.IterationStart,
                Iteration = iteration,
                Content = $"Agent Iteration {iteration}",
                IsCollapsed = false
            };
        }

        public static AgentStep CreateReasoning(string content, int iteration)
        {
            return new AgentStep
            {
                Type = AgentStepType.Reasoning,
                Iteration = iteration,
                Content = content,
                IsCollapsed = true
            };
        }

        public static AgentStep CreateToolCall(string toolName, string arguments, int iteration)
        {
            return new AgentStep
            {
                Type = AgentStepType.ToolCall,
                ToolName = toolName,
                ToolArguments = arguments,
                Iteration = iteration,
                Content = $"Calling {toolName}",
                IsCollapsed = false
            };
        }

        public static AgentStep CreateToolResult(string toolName, string result, int iteration)
        {
            return new AgentStep
            {
                Type = AgentStepType.ToolResult,
                ToolName = toolName,
                Iteration = iteration,
                Content = result,
                IsCollapsed = true
            };
        }

        public Godot.Collections.Dictionary ToDictionary()
        {
            return new Godot.Collections.Dictionary
            {
                { "type", (int)Type },
                { "iteration", Iteration },
                { "tool_name", ToolName ?? "" },
                { "tool_arguments", ToolArguments ?? "" },
                { "content", Content ?? "" },
                { "timestamp", Timestamp.ToString("o") },
                { "is_collapsed", IsCollapsed }
            };
        }

        public static AgentStep FromDictionary(Godot.Collections.Dictionary dict)
        {
            return new AgentStep
            {
                Type = dict.ContainsKey("type") ? (AgentStepType)dict["type"].AsInt32() : AgentStepType.Reasoning,
                Iteration = dict.ContainsKey("iteration") ? dict["iteration"].AsInt32() : 0,
                ToolName = dict.ContainsKey("tool_name") ? dict["tool_name"].AsString() : "",
                ToolArguments = dict.ContainsKey("tool_arguments") ? dict["tool_arguments"].AsString() : "",
                Content = dict.ContainsKey("content") ? dict["content"].AsString() : "",
                Timestamp = dict.ContainsKey("timestamp") 
                    ? DateTime.Parse(dict["timestamp"].AsString()) 
                    : DateTime.Now,
                IsCollapsed = dict.ContainsKey("is_collapsed") && dict["is_collapsed"].AsBool()
            };
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public List<AgentStep> AgentSteps { get; set; } = new List<AgentStep>();

        public ChatMessage() { }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
        }

        public Godot.Collections.Dictionary ToDictionary()
        {
            var stepsArray = new Godot.Collections.Array();
            foreach (var step in AgentSteps)
            {
                stepsArray.Add(step.ToDictionary());
            }

            return new Godot.Collections.Dictionary
            {
                { "role", Role },
                { "content", Content },
                { "timestamp", Timestamp.ToString("o") },
                { "agent_steps", stepsArray }
            };
        }

        public static ChatMessage FromDictionary(Godot.Collections.Dictionary dict)
        {
            var message = new ChatMessage
            {
                Role = dict.ContainsKey("role") ? dict["role"].AsString() : "user",
                Content = dict.ContainsKey("content") ? dict["content"].AsString() : "",
                Timestamp = dict.ContainsKey("timestamp") 
                    ? DateTime.Parse(dict["timestamp"].AsString()) 
                    : DateTime.Now
            };

            if (dict.ContainsKey("agent_steps"))
            {
                var stepsArray = dict["agent_steps"].AsGodotArray();
                foreach (var stepVar in stepsArray)
                {
                    var stepDict = stepVar.AsGodotDictionary();
                    message.AgentSteps.Add(AgentStep.FromDictionary(stepDict));
                }
            }

            return message;
        }

        public Godot.Collections.Dictionary ToApiFormat()
        {
            return new Godot.Collections.Dictionary
            {
                { "role", Role },
                { "content", Content }
            };
        }
    }
}
#endif

