#if TOOLS
using Godot;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace G0
{
    public static class MessageRenderer
    {
        // Theme colors for syntax highlighting (matching common editor themes)
        private static class SyntaxColors
        {
            public const string Keyword = "#FF7B72";      // Red-orange for keywords
            public const string String = "#A5D6FF";       // Light blue for strings
            public const string Number = "#79C0FF";       // Blue for numbers
            public const string Comment = "#8B949E";      // Gray for comments
            public const string Function = "#D2A8FF";     // Purple for functions
            public const string Type = "#7EE787";         // Green for types
            public const string Operator = "#FF7B72";     // Red for operators
            public const string Variable = "#FFA657";     // Orange for variables
        }

        // Common keywords for various languages
        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            // C#
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate",
            "do", "double", "else", "enum", "event", "explicit", "extern", "false",
            "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
            "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private",
            "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
            "var", "async", "await", "partial", "get", "set", "add", "remove", "yield",
            // GDScript
            "func", "var", "const", "extends", "class_name", "signal", "export",
            "onready", "tool", "master", "puppet", "slave", "remote", "sync",
            "pass", "and", "or", "not", "elif", "match", "preload", "self",
            // Python
            "def", "lambda", "import", "from", "with", "assert", "global", "nonlocal",
            "raise", "except", "None", "True", "False",
            // JavaScript/TypeScript
            "function", "let", "const", "var", "async", "await", "import", "export",
            "default", "from", "require", "module", "typeof", "instanceof", "undefined",
        };

        private static readonly HashSet<string> Types = new HashSet<string>
        {
            "int", "float", "double", "string", "bool", "void", "var", "object",
            "Array", "Dictionary", "List", "Vector2", "Vector3", "Transform",
            "Node", "Control", "Sprite", "PackedScene", "Resource", "String",
            "Color", "Rect2", "Basis", "Quaternion", "AABB", "Plane", "RID"
        };

        public static string RenderMessage(string content)
        {
            // Process markdown for both user and assistant messages
            // Markdown formatting is now applied equally to both
            return ProcessMarkdown(content);
        }

        private static string ProcessMarkdown(string content)
        {
            var result = new StringBuilder();
            var lines = content.Split('\n');
            var inCodeBlock = false;
            var codeBlockContent = new StringBuilder();
            var codeBlockLanguage = "";

            foreach (var line in lines)
            {
                if (line.StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        // Start of code block
                        inCodeBlock = true;
                        codeBlockLanguage = line.Length > 3 ? line.Substring(3).Trim() : "";
                        codeBlockContent.Clear();
                    }
                    else
                    {
                        // End of code block
                        inCodeBlock = false;
                        var highlightedCode = HighlightCode(codeBlockContent.ToString(), codeBlockLanguage);
                        result.Append($"\n[bgcolor=#1E1E1E][code]{highlightedCode}[/code][/bgcolor]\n");
                    }
                }
                else if (inCodeBlock)
                {
                    if (codeBlockContent.Length > 0)
                    {
                        codeBlockContent.Append("\n");
                    }
                    codeBlockContent.Append(line);
                }
                else
                {
                    // Process inline markdown
                    var processedLine = ProcessInlineMarkdown(line);
                    result.Append(processedLine);
                    result.Append("\n");
                }
            }

            // Handle unclosed code block
            if (inCodeBlock)
            {
                var highlightedCode = HighlightCode(codeBlockContent.ToString(), codeBlockLanguage);
                result.Append($"\n[bgcolor=#1E1E1E][code]{highlightedCode}[/code][/bgcolor]\n");
            }

            return result.ToString().TrimEnd('\n');
        }

        private static string ProcessInlineMarkdown(string line)
        {
            var result = line;

            // Blockquotes: > text
            if (result.StartsWith("> "))
            {
                result = $"[color=#8B949E]│ {result.Substring(2)}[/color]";
                return result;
            }

            // Horizontal rule: --- or ***
            if (result.Trim() == "---" || result.Trim() == "***" || result.Trim() == "___")
            {
                result = "[color=#3D3D3D]────────────────────────────────────────[/color]";
                return result;
            }

            // Headers: # ## ### #### (must be processed before bold/italic to avoid conflicts)
            if (result.StartsWith("#### "))
            {
                result = $"[b][font_size=14]{result.Substring(5)}[/font_size][/b]";
                return result;
            }
            else if (result.StartsWith("### "))
            {
                result = $"[b][font_size=16]{result.Substring(4)}[/font_size][/b]";
                return result;
            }
            else if (result.StartsWith("## "))
            {
                result = $"[b][font_size=18]{result.Substring(3)}[/font_size][/b]";
                return result;
            }
            else if (result.StartsWith("# "))
            {
                result = $"[b][font_size=20]{result.Substring(2)}[/font_size][/b]";
                return result;
            }

            // Bullet points
            if (result.StartsWith("- ") || result.StartsWith("* "))
            {
                result = $"  • {result.Substring(2)}";
            }

            // Numbered lists (simple)
            var numberedMatch = Regex.Match(result, @"^(\d+)\. (.+)$");
            if (numberedMatch.Success)
            {
                result = $"  {numberedMatch.Groups[1].Value}. {numberedMatch.Groups[2].Value}";
            }

            // Links: [text](url) - Godot's RichTextLabel supports [url] tag
            result = Regex.Replace(result, @"\[([^\]]+)\]\(([^\)]+)\)", "[url=$2][color=#58A6FF][u]$1[/u][/color][/url]");

            // Bold: **text** or __text__ (must be before italic to handle ***text***)
            result = Regex.Replace(result, @"\*\*(.+?)\*\*", "[b]$1[/b]");
            result = Regex.Replace(result, @"__(.+?)__", "[b]$1[/b]");

            // Italic: *text* or _text_
            result = Regex.Replace(result, @"\*([^*]+?)\*", "[i]$1[/i]");
            result = Regex.Replace(result, @"(?<!\w)_([^_]+?)_(?!\w)", "[i]$1[/i]");

            // Bold+Italic: ***text*** or ___text___
            result = Regex.Replace(result, @"\*\*\*(.+?)\*\*\*", "[b][i]$1[/i][/b]");
            result = Regex.Replace(result, @"___(.+?)___", "[b][i]$1[/i][/b]");

            // Inline code: `code`
            result = Regex.Replace(result, @"`([^`]+)`", "[bgcolor=#2D2D2D][code]$1[/code][/bgcolor]");

            // Strikethrough: ~~text~~
            result = Regex.Replace(result, @"~~(.+?)~~", "[s]$1[/s]");

            // File references: @filepath - render with distinctive styling
            result = Regex.Replace(result, @"@(?:""([^""]+)""|([^\s,;:!?\[\](){}]+\.[a-zA-Z0-9]+))", 
                match => RenderFileReference(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value));

            return result;
        }

        private static string HighlightCode(string code, string language)
        {
            if (string.IsNullOrEmpty(code))
            {
                return code;
            }

            var result = new StringBuilder();
            var lines = code.Split('\n');

            foreach (var line in lines)
            {
                if (result.Length > 0)
                {
                    result.Append("\n");
                }
                result.Append(HighlightLine(line, language));
            }

            return result.ToString();
        }

        private static string HighlightLine(string line, string language)
        {
            var result = new StringBuilder();
            var i = 0;

            while (i < line.Length)
            {
                // Skip whitespace
                if (char.IsWhiteSpace(line[i]))
                {
                    result.Append(line[i]);
                    i++;
                    continue;
                }

                // Check for comments
                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
                {
                    // Single line comment - rest of line
                    result.Append($"[color={SyntaxColors.Comment}]{EscapeBBCode(line.Substring(i))}[/color]");
                    break;
                }

                // Check for # comments (Python, GDScript)
                if (line[i] == '#' && (language == "python" || language == "gdscript" || language == "gd"))
                {
                    result.Append($"[color={SyntaxColors.Comment}]{EscapeBBCode(line.Substring(i))}[/color]");
                    break;
                }

                // Check for strings
                if (line[i] == '"' || line[i] == '\'')
                {
                    var quote = line[i];
                    var stringEnd = FindStringEnd(line, i + 1, quote);
                    var stringContent = line.Substring(i, stringEnd - i + 1);
                    result.Append($"[color={SyntaxColors.String}]{EscapeBBCode(stringContent)}[/color]");
                    i = stringEnd + 1;
                    continue;
                }

                // Check for numbers
                if (char.IsDigit(line[i]) || (line[i] == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
                {
                    var numEnd = i;
                    while (numEnd < line.Length && (char.IsDigit(line[numEnd]) || line[numEnd] == '.' || line[numEnd] == 'f' || line[numEnd] == 'x'))
                    {
                        numEnd++;
                    }
                    var number = line.Substring(i, numEnd - i);
                    result.Append($"[color={SyntaxColors.Number}]{number}[/color]");
                    i = numEnd;
                    continue;
                }

                // Check for identifiers and keywords
                if (char.IsLetter(line[i]) || line[i] == '_')
                {
                    var identEnd = i;
                    while (identEnd < line.Length && (char.IsLetterOrDigit(line[identEnd]) || line[identEnd] == '_'))
                    {
                        identEnd++;
                    }
                    var identifier = line.Substring(i, identEnd - i);

                    if (Keywords.Contains(identifier))
                    {
                        result.Append($"[color={SyntaxColors.Keyword}]{identifier}[/color]");
                    }
                    else if (Types.Contains(identifier))
                    {
                        result.Append($"[color={SyntaxColors.Type}]{identifier}[/color]");
                    }
                    else if (identEnd < line.Length && line[identEnd] == '(')
                    {
                        result.Append($"[color={SyntaxColors.Function}]{identifier}[/color]");
                    }
                    else
                    {
                        result.Append(identifier);
                    }
                    i = identEnd;
                    continue;
                }

                // Operators and other characters
                result.Append(line[i]);
                i++;
            }

            return result.ToString();
        }

        private static int FindStringEnd(string line, int start, char quote)
        {
            var i = start;
            while (i < line.Length)
            {
                if (line[i] == '\\' && i + 1 < line.Length)
                {
                    i += 2; // Skip escaped character
                    continue;
                }
                if (line[i] == quote)
                {
                    return i;
                }
                i++;
            }
            return line.Length - 1;
        }

        private static string EscapeBBCode(string text)
        {
            // Escape BBCode special characters
            return text
                .Replace("[", "[lb]")
                .Replace("]", "[rb]");
        }

        public static string RenderTypingIndicator()
        {
            return "[color=#8B949E][wave amp=20 freq=5]...[/wave][/color]";
        }

        public static string RenderError(string errorMessage)
        {
            return $"[color=#F85149][b]Error:[/b] {EscapeBBCode(errorMessage)}[/color]";
        }

        public static string RenderSystemMessage(string message)
        {
            return $"[color=#8B949E][i]{EscapeBBCode(message)}[/i][/color]";
        }

        /// <summary>
        /// Renders agent thinking/reasoning content with a distinct style.
        /// </summary>
        public static string RenderAgentThinking(string thinking)
        {
            if (string.IsNullOrEmpty(thinking))
            {
                return "[color=#A78BFA][i]Thinking...[/i][/color]";
            }
            
            var truncated = TruncateText(thinking, 500);
            return $"[color=#A78BFA][i]{EscapeBBCode(truncated)}[/i][/color]";
        }

        /// <summary>
        /// Renders a tool call with tool name and abbreviated arguments.
        /// </summary>
        public static string RenderToolCall(string toolName, string arguments)
        {
            var argsPreview = TruncateText(arguments, 150);
            return $"[color=#F59E0B][b]🔧 {EscapeBBCode(toolName)}[/b][/color]\n[color=#8B949E][code]{EscapeBBCode(argsPreview)}[/code][/color]";
        }

        /// <summary>
        /// Renders a tool execution status (in progress).
        /// </summary>
        public static string RenderToolExecuting(string toolName)
        {
            return $"[color=#F59E0B][wave amp=10 freq=3]⚙️ Executing {EscapeBBCode(toolName)}...[/wave][/color]";
        }

        /// <summary>
        /// Renders a tool result with tool name and abbreviated result.
        /// </summary>
        public static string RenderToolResult(string toolName, string result)
        {
            var isError = result.StartsWith("Error");
            var icon = isError ? "❌" : "✅";
            var color = isError ? "#EF4444" : "#10B981";
            var resultPreview = TruncateText(result, 300);
            
            return $"[color={color}][b]{icon} {EscapeBBCode(toolName)}[/b][/color]\n[color=#8B949E]{EscapeBBCode(resultPreview)}[/color]";
        }

        /// <summary>
        /// Truncates text to a maximum length with ellipsis.
        /// </summary>
        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }
            
            return text.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Renders a file reference with distinctive styling.
        /// </summary>
        /// <param name="filePath">The file path (without the @ prefix).</param>
        /// <returns>BBCode formatted file reference.</returns>
        public static string RenderFileReference(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "@";
            }

            // Use a distinctive blue color for file references
            return $"[color=#58A6FF]📄 @{EscapeBBCode(filePath)}[/color]";
        }

        /// <summary>
        /// Renders an attached files summary.
        /// </summary>
        /// <param name="fileCount">Number of attached files.</param>
        /// <param name="successCount">Number of successfully read files.</param>
        /// <returns>BBCode formatted attachment summary.</returns>
        public static string RenderAttachmentSummary(int fileCount, int successCount)
        {
            if (fileCount == 0)
            {
                return "";
            }

            var color = successCount == fileCount ? "#58A6FF" : "#F59E0B";
            var icon = successCount == fileCount ? "📎" : "⚠️";
            
            if (successCount == fileCount)
            {
                return $"[color={color}]{icon} {fileCount} file{(fileCount == 1 ? "" : "s")} attached[/color]";
            }
            else
            {
                return $"[color={color}]{icon} {successCount}/{fileCount} files attached[/color]";
            }
        }

        /// <summary>
        /// Renders a code snippet summary for display in chat history.
        /// </summary>
        /// <param name="snippet">The code snippet to render.</param>
        /// <returns>BBCode formatted snippet display.</returns>
        public static string RenderCodeSnippet(G0.Models.CodeSnippet snippet)
        {
            if (snippet == null)
            {
                return "";
            }

            var displaySummary = snippet.GetDisplaySummary();
            var languageColor = GetLanguageColor(snippet.Language);
            
            return $"[color={languageColor}]✂️ {EscapeBBCode(displaySummary)}[/color]";
        }

        /// <summary>
        /// Renders a code snippet with its full content for display.
        /// </summary>
        /// <param name="snippet">The code snippet to render.</param>
        /// <returns>BBCode formatted snippet with code.</returns>
        public static string RenderCodeSnippetWithContent(G0.Models.CodeSnippet snippet)
        {
            if (snippet == null)
            {
                return "";
            }

            var sb = new StringBuilder();
            var languageColor = GetLanguageColor(snippet.Language);
            
            // Header with file info
            sb.Append($"[color={languageColor}]✂️ {EscapeBBCode(snippet.FilePath)}");
            sb.Append($" (lines {snippet.StartLine}-{snippet.EndLine})[/color]\n");
            
            // Code content
            if (!string.IsNullOrEmpty(snippet.Content))
            {
                var highlightedCode = HighlightCode(snippet.Content, snippet.Language);
                sb.Append($"[bgcolor=#1E1E1E][code]{highlightedCode}[/code][/bgcolor]");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Renders a summary of attached snippets.
        /// </summary>
        /// <param name="snippetCount">Number of attached snippets.</param>
        /// <returns>BBCode formatted snippet summary.</returns>
        public static string RenderSnippetSummary(int snippetCount)
        {
            if (snippetCount == 0)
            {
                return "";
            }

            return $"[color=#A78BFA]✂️ {snippetCount} snippet{(snippetCount == 1 ? "" : "s")} attached[/color]";
        }

        /// <summary>
        /// Gets a color based on the programming language.
        /// </summary>
        private static string GetLanguageColor(string language)
        {
            return (language?.ToLowerInvariant()) switch
            {
                "csharp" or "cs" => "#61AFEF",      // C# blue
                "gdscript" or "gd" => "#4A90C8",    // GDScript blue
                "python" or "py" => "#F0C840",      // Python yellow
                "javascript" or "js" => "#F0DB4F", // JS yellow
                "typescript" or "ts" => "#3178C6", // TS blue
                "rust" or "rs" => "#DE6A2E",        // Rust orange
                "go" => "#00ADD8",                  // Go cyan
                "java" => "#F07830",                // Java orange
                _ => "#A78BFA"                      // Default purple
            };
        }
    }
}
#endif

