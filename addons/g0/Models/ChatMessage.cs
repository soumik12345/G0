#if TOOLS
using System;

namespace G0.Models
{
    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public ChatMessage() { }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
        }

        public Godot.Collections.Dictionary ToDictionary()
        {
            return new Godot.Collections.Dictionary
            {
                { "role", Role },
                { "content", Content },
                { "timestamp", Timestamp.ToString("o") }
            };
        }

        public static ChatMessage FromDictionary(Godot.Collections.Dictionary dict)
        {
            return new ChatMessage
            {
                Role = dict.ContainsKey("role") ? dict["role"].AsString() : "user",
                Content = dict.ContainsKey("content") ? dict["content"].AsString() : "",
                Timestamp = dict.ContainsKey("timestamp") 
                    ? DateTime.Parse(dict["timestamp"].AsString()) 
                    : DateTime.Now
            };
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

