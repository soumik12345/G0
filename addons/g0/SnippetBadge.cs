#if TOOLS
using Godot;
using G0.Models;

namespace G0
{
    /// <summary>
    /// A compact badge UI component that displays a code snippet reference.
    /// Shows the file name and line range, with a hover tooltip for code preview
    /// and a remove button to delete the snippet.
    /// </summary>
    [Tool]
    public partial class SnippetBadge : PanelContainer
    {
        /// <summary>
        /// Signal emitted when the user requests to remove this snippet.
        /// </summary>
        [Signal]
        public delegate void RemoveRequestedEventHandler(SnippetBadge badge);

        /// <summary>
        /// The code snippet this badge represents.
        /// </summary>
        public CodeSnippet Snippet { get; private set; }

        private HBoxContainer _contentContainer;
        private Label _iconLabel;
        private Label _textLabel;
        private Button _removeButton;
        private PopupPanel _hoverPopup;
        private RichTextLabel _codePreviewLabel;
        private bool _isHovering = false;

        public SnippetBadge()
        {
            // Default constructor required for Godot
        }

        public SnippetBadge(CodeSnippet snippet)
        {
            Snippet = snippet;
        }

        public override void _Ready()
        {
            BuildUI();
            UpdateDisplay();
        }

        private void BuildUI()
        {
            // Apply badge styling
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.2f, 0.3f, 0.4f, 0.9f);
            styleBox.CornerRadiusTopLeft = 4;
            styleBox.CornerRadiusTopRight = 4;
            styleBox.CornerRadiusBottomLeft = 4;
            styleBox.CornerRadiusBottomRight = 4;
            styleBox.ContentMarginLeft = 6;
            styleBox.ContentMarginRight = 4;
            styleBox.ContentMarginTop = 2;
            styleBox.ContentMarginBottom = 2;
            styleBox.BorderWidthLeft = 2;
            styleBox.BorderColor = GetLanguageColor(Snippet?.Language ?? "");
            AddThemeStyleboxOverride("panel", styleBox);

            // Content container
            _contentContainer = new HBoxContainer();
            _contentContainer.AddThemeConstantOverride("separation", 4);
            AddChild(_contentContainer);

            // File icon
            _iconLabel = new Label();
            _iconLabel.Text = "✂️";
            _iconLabel.AddThemeFontSizeOverride("font_size", 11);
            _contentContainer.AddChild(_iconLabel);

            // File name and line range
            _textLabel = new Label();
            _textLabel.AddThemeFontSizeOverride("font_size", 11);
            _textLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.95f));
            _contentContainer.AddChild(_textLabel);

            // Remove button
            _removeButton = new Button();
            _removeButton.Text = "×";
            _removeButton.Flat = true;
            _removeButton.AddThemeFontSizeOverride("font_size", 12);
            _removeButton.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _removeButton.AddThemeColorOverride("font_hover_color", new Color(1f, 0.4f, 0.4f));
            _removeButton.CustomMinimumSize = new Vector2(16, 16);
            _removeButton.Pressed += OnRemovePressed;
            _removeButton.TooltipText = "Remove snippet";
            _contentContainer.AddChild(_removeButton);

            // Build hover popup for code preview
            BuildHoverPopup();

            // Connect mouse signals for hover
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
        }

        private void BuildHoverPopup()
        {
            _hoverPopup = new PopupPanel();
            _hoverPopup.Transparent = true;
            _hoverPopup.TransparentBg = true;
            
            var popupStyle = new StyleBoxFlat();
            popupStyle.BgColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            popupStyle.CornerRadiusTopLeft = 6;
            popupStyle.CornerRadiusTopRight = 6;
            popupStyle.CornerRadiusBottomLeft = 6;
            popupStyle.CornerRadiusBottomRight = 6;
            popupStyle.ContentMarginLeft = 10;
            popupStyle.ContentMarginRight = 10;
            popupStyle.ContentMarginTop = 8;
            popupStyle.ContentMarginBottom = 8;
            popupStyle.BorderWidthLeft = 1;
            popupStyle.BorderWidthRight = 1;
            popupStyle.BorderWidthTop = 1;
            popupStyle.BorderWidthBottom = 1;
            popupStyle.BorderColor = new Color(0.3f, 0.4f, 0.5f);
            _hoverPopup.AddThemeStyleboxOverride("panel", popupStyle);

            var popupVBox = new VBoxContainer();
            popupVBox.AddThemeConstantOverride("separation", 6);
            _hoverPopup.AddChild(popupVBox);

            // Header with file path
            var headerLabel = new Label();
            headerLabel.Text = Snippet != null ? $"📄 {Snippet.FilePath} (lines {Snippet.StartLine}-{Snippet.EndLine})" : "";
            headerLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.75f, 0.9f));
            headerLabel.AddThemeFontSizeOverride("font_size", 11);
            popupVBox.AddChild(headerLabel);

            // Code preview with scroll
            var scrollContainer = new ScrollContainer();
            scrollContainer.CustomMinimumSize = new Vector2(400, 200);
            scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
            scrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
            popupVBox.AddChild(scrollContainer);

            _codePreviewLabel = new RichTextLabel();
            _codePreviewLabel.BbcodeEnabled = true;
            _codePreviewLabel.FitContent = true;
            _codePreviewLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _codePreviewLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
            _codePreviewLabel.ScrollActive = false;
            _codePreviewLabel.CustomMinimumSize = new Vector2(380, 0);
            scrollContainer.AddChild(_codePreviewLabel);

            AddChild(_hoverPopup);
        }

        private void UpdateDisplay()
        {
            if (Snippet == null)
            {
                _textLabel.Text = "(no snippet)";
                return;
            }

            _textLabel.Text = Snippet.GetDisplaySummary();
            TooltipText = $"{Snippet.FilePath}\nLines {Snippet.StartLine}-{Snippet.EndLine}\n{Snippet.LineCount} lines";

            // Update code preview
            if (_codePreviewLabel != null)
            {
                var formattedCode = FormatCodePreview(Snippet.GetContentPreview(15));
                _codePreviewLabel.Clear();
                _codePreviewLabel.AppendText(formattedCode);
            }
        }

        private string FormatCodePreview(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return "[color=#8B949E][i]Empty snippet[/i][/color]";
            }

            // Use the MessageRenderer's highlighting if available, otherwise simple format
            var escapedCode = code
                .Replace("[", "[lb]")
                .Replace("]", "[rb]");

            return $"[bgcolor=#1E1E1E][code]{escapedCode}[/code][/bgcolor]";
        }

        private Color GetLanguageColor(string language)
        {
            return language.ToLowerInvariant() switch
            {
                "csharp" or "cs" => new Color(0.38f, 0.55f, 0.84f),     // C# blue
                "gdscript" or "gd" => new Color(0.29f, 0.59f, 0.78f),   // GDScript blue
                "python" or "py" => new Color(0.94f, 0.78f, 0.25f),     // Python yellow
                "javascript" or "js" => new Color(0.95f, 0.86f, 0.31f), // JS yellow
                "typescript" or "ts" => new Color(0.19f, 0.47f, 0.76f), // TS blue
                "rust" or "rs" => new Color(0.87f, 0.41f, 0.18f),       // Rust orange
                "go" => new Color(0.0f, 0.68f, 0.84f),                  // Go cyan
                "java" => new Color(0.94f, 0.47f, 0.25f),               // Java orange
                _ => new Color(0.5f, 0.6f, 0.7f)                        // Default gray-blue
            };
        }

        private void OnRemovePressed()
        {
            EmitSignal(SignalName.RemoveRequested, this);
        }

        private void OnMouseEntered()
        {
            _isHovering = true;
            
            // Show hover popup after a brief delay
            var timer = GetTree().CreateTimer(0.3);
            timer.Timeout += () =>
            {
                if (_isHovering && _hoverPopup != null && !_hoverPopup.Visible)
                {
                    ShowHoverPopup();
                }
            };
        }

        private void OnMouseExited()
        {
            _isHovering = false;
            
            // Hide popup after a brief delay to allow mouse to move to popup
            var timer = GetTree().CreateTimer(0.1);
            timer.Timeout += () =>
            {
                if (!_isHovering && _hoverPopup != null && _hoverPopup.Visible)
                {
                    // Check if mouse is over the popup by using position and size
                    var popupPos = _hoverPopup.Position;
                    var popupSize = _hoverPopup.Size;
                    var popupRect = new Rect2(popupPos.X, popupPos.Y, popupSize.X, popupSize.Y);
                    var mousePos = GetGlobalMousePosition();
                    if (!popupRect.HasPoint(mousePos))
                    {
                        _hoverPopup.Hide();
                    }
                }
            };
        }

        private void ShowHoverPopup()
        {
            if (_hoverPopup == null || Snippet == null)
            {
                return;
            }

            // Position popup above the badge
            var badgeGlobalPos = GetGlobalTransformWithCanvas().Origin;
            var parentWindow = GetWindow();
            
            if (parentWindow != null)
            {
                var popupSize = _hoverPopup.Size;
                var popupPosition = new Vector2I(
                    (int)(parentWindow.Position.X + badgeGlobalPos.X),
                    (int)(parentWindow.Position.Y + badgeGlobalPos.Y - popupSize.Y - 10)
                );
                
                // Ensure popup doesn't go off screen
                if (popupPosition.Y < 0)
                {
                    // Show below the badge instead
                    popupPosition.Y = (int)(parentWindow.Position.Y + badgeGlobalPos.Y + Size.Y + 10);
                }
                
                _hoverPopup.Position = popupPosition;
            }

            _hoverPopup.Popup();
        }

        /// <summary>
        /// Updates the snippet and refreshes the display.
        /// </summary>
        public void SetSnippet(CodeSnippet snippet)
        {
            Snippet = snippet;
            if (IsInsideTree())
            {
                UpdateDisplay();
            }
        }

        public override void _ExitTree()
        {
            if (_removeButton != null)
            {
                _removeButton.Pressed -= OnRemovePressed;
            }
            
            MouseEntered -= OnMouseEntered;
            MouseExited -= OnMouseExited;
        }
    }
}
#endif

