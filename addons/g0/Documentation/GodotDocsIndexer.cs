#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace G0.Documentation
{
    /// <summary>
    /// Downloads and indexes Godot documentation for local searching.
    /// </summary>
    public partial class GodotDocsIndexer : Node
    {
        private const string BaseUrl = "https://docs.godotengine.org/en/stable/";
        private const string IndexPath = "user://godot_docs_index.json";
        private const int MaxConcurrentDownloads = 5;
        private const int MaxContentLength = 2000; // Max characters per entry content

        [Signal]
        public delegate void IndexingProgressEventHandler(int current, int total, string currentPage);

        [Signal]
        public delegate void IndexingCompleteEventHandler(int totalEntries);

        [Signal]
        public delegate void IndexingErrorEventHandler(string errorMessage);

        private System.Net.Http.HttpClient _httpClient;
        private DocumentationIndex _index;
        private bool _isIndexing;
        private CancellationTokenSource _cancellationTokenSource;

        public bool IsIndexing => _isIndexing;
        public DocumentationIndex Index => _index;

        // Key documentation pages to index
        private readonly List<string> _pagesToIndex = new List<string>
        {
            // Getting Started
            "getting_started/introduction/index.html",
            "getting_started/first_2d_game/index.html",
            "getting_started/first_3d_game/index.html",
            "getting_started/step_by_step/index.html",
            
            // Tutorials
            "tutorials/2d/index.html",
            "tutorials/3d/index.html",
            "tutorials/animation/index.html",
            "tutorials/audio/index.html",
            "tutorials/best_practices/index.html",
            "tutorials/editor/index.html",
            "tutorials/export/index.html",
            "tutorials/i18n/index.html",
            "tutorials/inputs/index.html",
            "tutorials/io/index.html",
            "tutorials/math/index.html",
            "tutorials/navigation/index.html",
            "tutorials/networking/index.html",
            "tutorials/performance/index.html",
            "tutorials/physics/index.html",
            "tutorials/platform/index.html",
            "tutorials/plugins/index.html",
            "tutorials/rendering/index.html",
            "tutorials/scripting/index.html",
            "tutorials/shaders/index.html",
            "tutorials/ui/index.html",
            "tutorials/xr/index.html",
            
            // Class reference (most commonly used)
            "classes/class_node.html",
            "classes/class_node2d.html",
            "classes/class_node3d.html",
            "classes/class_control.html",
            "classes/class_sprite2d.html",
            "classes/class_sprite3d.html",
            "classes/class_characterbody2d.html",
            "classes/class_characterbody3d.html",
            "classes/class_rigidbody2d.html",
            "classes/class_rigidbody3d.html",
            "classes/class_area2d.html",
            "classes/class_area3d.html",
            "classes/class_collisionshape2d.html",
            "classes/class_collisionshape3d.html",
            "classes/class_camera2d.html",
            "classes/class_camera3d.html",
            "classes/class_animationplayer.html",
            "classes/class_animationtree.html",
            "classes/class_tilemap.html",
            "classes/class_tilemaplayer.html",
            "classes/class_label.html",
            "classes/class_button.html",
            "classes/class_texturerect.html",
            "classes/class_canvaslayer.html",
            "classes/class_timer.html",
            "classes/class_tween.html",
            "classes/class_audiostreamplayer.html",
            "classes/class_audiostreamplayer2d.html",
            "classes/class_audiostreamplayer3d.html",
            "classes/class_input.html",
            "classes/class_inputevent.html",
            "classes/class_vector2.html",
            "classes/class_vector3.html",
            "classes/class_transform2d.html",
            "classes/class_transform3d.html",
            "classes/class_packedscene.html",
            "classes/class_resource.html",
            "classes/class_texture2d.html",
            "classes/class_shader.html",
            "classes/class_material.html",
            "classes/class_mesh.html",
            "classes/class_arraymesh.html",
            "classes/class_gdscript.html",
            "classes/class_object.html",
            "classes/class_refcounted.html",
            "classes/class_signal.html",
            "classes/class_callable.html",
            "classes/class_array.html",
            "classes/class_dictionary.html",
            "classes/class_string.html",
            "classes/class_color.html",
            "classes/class_rect2.html",
            "classes/class_aabb.html",
            "classes/class_basis.html",
            "classes/class_quaternion.html",
            "classes/class_projection.html",
            "classes/class_navigationagent2d.html",
            "classes/class_navigationagent3d.html",
            "classes/class_raycast2d.html",
            "classes/class_raycast3d.html",
            "classes/class_shapecast2d.html",
            "classes/class_shapecast3d.html",
            "classes/class_httpRequest.html",
            "classes/class_httpclient.html",
            "classes/class_jsonparseerror.html",
            "classes/class_fileaccess.html",
            "classes/class_diraccess.html",
            "classes/class_os.html",
            "classes/class_engine.html",
            "classes/class_projectsettings.html",
            "classes/class_displayserver.html",
            "classes/class_renderingserver.html",
            "classes/class_physicsserver2d.html",
            "classes/class_physicsserver3d.html",
            "classes/class_scenetree.html",
            "classes/class_viewport.html",
            "classes/class_window.html",
            "classes/class_theme.html",
            "classes/class_stylebox.html",
            "classes/class_font.html",
            
            // GDScript reference
            "tutorials/scripting/gdscript/gdscript_basics.html",
            "tutorials/scripting/gdscript/gdscript_styleguide.html",
            "tutorials/scripting/gdscript/gdscript_exports.html",
            "tutorials/scripting/gdscript/static_typing.html",
            
            // C# reference
            "tutorials/scripting/c_sharp/c_sharp_basics.html",
            "tutorials/scripting/c_sharp/c_sharp_features.html",
            "tutorials/scripting/c_sharp/c_sharp_differences.html",
            "tutorials/scripting/c_sharp/c_sharp_signals.html",
        };

        public override void _Ready()
        {
            _httpClient = new System.Net.Http.HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "G0-Godot-Plugin/1.0");
        }

        /// <summary>
        /// Checks if a documentation index exists.
        /// </summary>
        public bool IndexExists()
        {
            return FileAccess.FileExists(IndexPath);
        }

        /// <summary>
        /// Loads the existing documentation index from disk.
        /// </summary>
        public bool LoadIndex()
        {
            if (!IndexExists())
            {
                _index = new DocumentationIndex();
                return false;
            }

            try
            {
                using var file = FileAccess.Open(IndexPath, FileAccess.ModeFlags.Read);
                if (file == null)
                {
                    GD.PrintErr("G0: Failed to open documentation index file");
                    _index = new DocumentationIndex();
                    return false;
                }

                var jsonString = file.GetAsText();
                _index = JsonSerializer.Deserialize<DocumentationIndex>(jsonString);
                
                if (_index == null)
                {
                    _index = new DocumentationIndex();
                    return false;
                }

                GD.Print($"G0: Loaded documentation index with {_index.Entries.Count} entries");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Error loading documentation index: {ex.Message}");
                _index = new DocumentationIndex();
                return false;
            }
        }

        /// <summary>
        /// Saves the documentation index to disk.
        /// </summary>
        public bool SaveIndex()
        {
            if (_index == null)
            {
                GD.PrintErr("G0: No index to save");
                return false;
            }

            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };
                var jsonString = JsonSerializer.Serialize(_index, options);

                using var file = FileAccess.Open(IndexPath, FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    GD.PrintErr("G0: Failed to open documentation index file for writing");
                    return false;
                }

                file.StoreString(jsonString);
                GD.Print($"G0: Saved documentation index with {_index.Entries.Count} entries");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Error saving documentation index: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts downloading and indexing the Godot documentation.
        /// </summary>
        public async Task StartIndexingAsync()
        {
            if (_isIndexing)
            {
                GD.PrintErr("G0: Already indexing documentation");
                return;
            }

            _isIndexing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _index = new DocumentationIndex();

            try
            {
                GD.Print("G0: Starting documentation indexing...");
                
                var allPages = new List<string>(_pagesToIndex);
                
                // First, fetch table of contents pages to discover more links
                var tocPages = await DiscoverPagesFromTocAsync(_cancellationTokenSource.Token);
                allPages.AddRange(tocPages);
                
                // Remove duplicates
                allPages = allPages.Distinct().ToList();
                
                GD.Print($"G0: Found {allPages.Count} pages to index");

                int completed = 0;
                var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);
                var tasks = new List<Task>();

                foreach (var page in allPages)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    await semaphore.WaitAsync(_cancellationTokenSource.Token);

                    var pageTask = Task.Run(async () =>
                    {
                        try
                        {
                            await IndexPageAsync(page, _cancellationTokenSource.Token);
                        }
                        finally
                        {
                            semaphore.Release();
                            var current = Interlocked.Increment(ref completed);
                            CallDeferred(nameof(EmitProgress), current, allPages.Count, page);
                        }
                    }, _cancellationTokenSource.Token);

                    tasks.Add(pageTask);
                }

                await Task.WhenAll(tasks);

                _index.LastUpdated = DateTime.UtcNow;
                _index.TotalPages = allPages.Count;
                
                SaveIndex();

                CallDeferred(nameof(EmitComplete), _index.Entries.Count);
            }
            catch (OperationCanceledException)
            {
                GD.Print("G0: Documentation indexing was cancelled");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"G0: Error during indexing: {ex.Message}");
                CallDeferred(nameof(EmitError), ex.Message);
            }
            finally
            {
                _isIndexing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Cancels the current indexing operation.
        /// </summary>
        public void CancelIndexing()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task<List<string>> DiscoverPagesFromTocAsync(CancellationToken token)
        {
            var discoveredPages = new List<string>();
            
            try
            {
                var tocUrl = BaseUrl + "index.html";
                var html = await _httpClient.GetStringAsync(tocUrl, token);
                
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Find all links in the table of contents
                var links = doc.DocumentNode.SelectNodes("//a[@href]");
                if (links != null)
                {
                    foreach (var link in links)
                    {
                        var href = link.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href) && 
                            !href.StartsWith("http") && 
                            !href.StartsWith("#") &&
                            href.EndsWith(".html"))
                        {
                            // Clean up relative paths
                            var cleanPath = href.TrimStart('/');
                            if (!cleanPath.StartsWith("_"))
                            {
                                discoveredPages.Add(cleanPath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GD.Print($"G0: Could not discover additional pages: {ex.Message}");
            }

            return discoveredPages;
        }

        private async Task IndexPageAsync(string pagePath, CancellationToken token)
        {
            try
            {
                var url = BaseUrl + pagePath;
                var html = await _httpClient.GetStringAsync(url, token);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Get the page title
                var titleNode = doc.DocumentNode.SelectSingleNode("//title");
                var title = titleNode?.InnerText?.Trim() ?? pagePath;
                title = System.Net.WebUtility.HtmlDecode(title);
                
                // Remove " — Godot Engine documentation" suffix
                title = Regex.Replace(title, @"\s*[—–-]\s*Godot Engine.*$", "", RegexOptions.IgnoreCase);

                // Get the main content area
                var contentNode = doc.DocumentNode.SelectSingleNode("//div[@class='body']") ??
                                  doc.DocumentNode.SelectSingleNode("//div[@class='document']") ??
                                  doc.DocumentNode.SelectSingleNode("//article") ??
                                  doc.DocumentNode.SelectSingleNode("//main");

                if (contentNode == null)
                {
                    return;
                }

                // Find all sections
                var sections = contentNode.SelectNodes(".//section") ?? 
                               new HtmlNodeCollection(null);

                // If no sections, treat the whole content as one entry
                if (sections.Count == 0)
                {
                    var entry = CreateEntryFromNode(url, title, "", contentNode, pagePath);
                    if (entry != null)
                    {
                        lock (_index.Entries)
                        {
                            _index.Entries.Add(entry);
                        }
                    }
                }
                else
                {
                    // Process each section
                    foreach (var section in sections)
                    {
                        var sectionTitle = GetSectionTitle(section);
                        var entry = CreateEntryFromNode(url, title, sectionTitle, section, pagePath);
                        if (entry != null)
                        {
                            lock (_index.Entries)
                            {
                                _index.Entries.Add(entry);
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Page not found or network error - skip silently
            }
            catch (Exception ex)
            {
                GD.Print($"G0: Error indexing {pagePath}: {ex.Message}");
            }
        }

        private string GetSectionTitle(HtmlNode section)
        {
            var headingNode = section.SelectSingleNode(".//h1") ??
                              section.SelectSingleNode(".//h2") ??
                              section.SelectSingleNode(".//h3") ??
                              section.SelectSingleNode(".//h4");
            
            if (headingNode != null)
            {
                var text = headingNode.InnerText.Trim();
                return System.Net.WebUtility.HtmlDecode(text);
            }

            return "";
        }

        private DocumentationEntry CreateEntryFromNode(string url, string title, string section, HtmlNode node, string pagePath)
        {
            // Extract text content
            var textContent = ExtractTextContent(node);
            if (string.IsNullOrWhiteSpace(textContent) || textContent.Length < 50)
            {
                return null;
            }

            // Truncate if too long
            if (textContent.Length > MaxContentLength)
            {
                textContent = textContent.Substring(0, MaxContentLength) + "...";
            }

            // Extract code examples
            var codeExamples = ExtractCodeExamples(node);

            // Extract keywords
            var keywords = ExtractKeywords(title, section, textContent);

            // Determine category from path
            var category = ExtractCategory(pagePath);

            return new DocumentationEntry
            {
                Url = url,
                Title = title,
                Section = section,
                Content = textContent,
                CodeExamples = codeExamples,
                Keywords = keywords,
                Category = category,
                IndexedAt = DateTime.UtcNow
            };
        }

        private string ExtractTextContent(HtmlNode node)
        {
            // Clone node to avoid modifying original
            var clonedNode = HtmlNode.CreateNode(node.OuterHtml);
            
            // Remove script and style nodes
            var scriptsAndStyles = clonedNode.SelectNodes(".//script|.//style|.//nav|.//footer|.//header");
            if (scriptsAndStyles != null)
            {
                foreach (var toRemove in scriptsAndStyles.ToList())
                {
                    toRemove.Remove();
                }
            }

            // Get text and clean it up
            var text = clonedNode.InnerText;
            text = System.Net.WebUtility.HtmlDecode(text);
            
            // Clean up whitespace
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            return text;
        }

        private List<CodeExample> ExtractCodeExamples(HtmlNode node)
        {
            var examples = new List<CodeExample>();

            // Find code blocks
            var codeBlocks = node.SelectNodes(".//pre/code") ?? 
                             node.SelectNodes(".//div[contains(@class, 'highlight')]//pre");

            if (codeBlocks != null)
            {
                foreach (var codeBlock in codeBlocks.Take(3)) // Limit to 3 examples per section
                {
                    var code = codeBlock.InnerText.Trim();
                    code = System.Net.WebUtility.HtmlDecode(code);

                    if (code.Length > 50 && code.Length < 2000)
                    {
                        // Try to determine language
                        var language = "gdscript";
                        var classAttr = codeBlock.GetAttributeValue("class", "");
                        var parentClassAttr = codeBlock.ParentNode?.GetAttributeValue("class", "") ?? "";
                        
                        if (classAttr.Contains("csharp") || parentClassAttr.Contains("csharp") ||
                            classAttr.Contains("c-sharp") || parentClassAttr.Contains("c-sharp"))
                        {
                            language = "csharp";
                        }
                        else if (classAttr.Contains("python") || parentClassAttr.Contains("python"))
                        {
                            language = "python";
                        }

                        examples.Add(new CodeExample
                        {
                            Language = language,
                            Code = code
                        });
                    }
                }
            }

            return examples;
        }

        private List<string> ExtractKeywords(string title, string section, string content)
        {
            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add words from title
            foreach (var word in ExtractWords(title))
            {
                keywords.Add(word);
            }

            // Add words from section
            foreach (var word in ExtractWords(section))
            {
                keywords.Add(word);
            }

            // Extract important terms from content
            var godotTerms = new[] 
            {
                "node", "scene", "script", "signal", "export", "onready", "func", "var",
                "class_name", "extends", "preload", "load", "instance", "queue_free",
                "get_node", "add_child", "remove_child", "_ready", "_process", "_physics_process",
                "_input", "_unhandled_input", "connect", "emit_signal", "call_deferred",
                "Vector2", "Vector3", "Transform2D", "Transform3D", "Basis", "Quaternion",
                "CharacterBody2D", "CharacterBody3D", "RigidBody2D", "RigidBody3D",
                "Area2D", "Area3D", "CollisionShape2D", "CollisionShape3D",
                "Sprite2D", "Sprite3D", "AnimatedSprite2D", "AnimatedSprite3D",
                "Camera2D", "Camera3D", "TileMap", "TileMapLayer",
                "AnimationPlayer", "AnimationTree", "Tween", "Timer",
                "Control", "Label", "Button", "TextEdit", "LineEdit",
                "PackedScene", "Resource", "Texture2D", "AudioStream",
                "Input", "InputEvent", "InputEventKey", "InputEventMouse",
                "GDScript", "C#", "export", "@export", "@onready"
            };

            var contentLower = content.ToLowerInvariant();
            foreach (var term in godotTerms)
            {
                if (contentLower.Contains(term.ToLowerInvariant()))
                {
                    keywords.Add(term);
                }
            }

            return keywords.Take(20).ToList();
        }

        private IEnumerable<string> ExtractWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            var words = Regex.Split(text, @"[\s\-_:,.!?()[\]{}]+");
            foreach (var word in words)
            {
                if (word.Length >= 3)
                {
                    yield return word;
                }
            }
        }

        private string ExtractCategory(string pagePath)
        {
            // Extract the first directory from the path
            var parts = pagePath.Split('/');
            if (parts.Length > 1)
            {
                return parts[0];
            }
            return "general";
        }

        private void EmitProgress(int current, int total, string currentPage)
        {
            EmitSignal(SignalName.IndexingProgress, current, total, currentPage);
        }

        private void EmitComplete(int totalEntries)
        {
            EmitSignal(SignalName.IndexingComplete, totalEntries);
        }

        private void EmitError(string errorMessage)
        {
            EmitSignal(SignalName.IndexingError, errorMessage);
        }

        public override void _ExitTree()
        {
            _httpClient?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
#endif

