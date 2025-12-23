#if TOOLS
using Godot;
using System;
using System.Collections.Generic;

namespace G0
{
    /// <summary>
    /// A popup panel that provides file autocomplete functionality.
    /// Shows a filterable list of project files when the user types '@'.
    /// </summary>
    [Tool]
    public partial class FileAutocompletePopup : PopupPanel
    {
        /// <summary>
        /// Emitted when a file is selected from the autocomplete list.
        /// </summary>
        /// <param name="filePath">The selected file path.</param>
        [Signal]
        public delegate void FileSelectedEventHandler(string filePath);

        /// <summary>
        /// Emitted when the popup is cancelled (Escape pressed).
        /// </summary>
        [Signal]
        public delegate void CancelledEventHandler();

        // UI Elements
        private VBoxContainer _mainContainer;
        private LineEdit _searchInput;
        private ScrollContainer _scrollContainer;
        private VBoxContainer _fileListContainer;
        private Label _noResultsLabel;

        // State
        private List<string> _filteredFiles = new List<string>();
        private int _selectedIndex = -1;
        private List<Button> _fileButtons = new List<Button>();
        private string _currentQuery = "";

        // Configuration
        private const int MaxVisibleResults = 10;
        private const int PopupWidth = 400;
        private const int PopupHeight = 300;

        public override void _Ready()
        {
            // Configure popup properties
            Transparent = true;
            TransparentBg = true;

            BuildUI();
        }

        private void BuildUI()
        {
            // Main container
            _mainContainer = new VBoxContainer();
            _mainContainer.CustomMinimumSize = new Vector2(PopupWidth, PopupHeight);
            _mainContainer.AddThemeConstantOverride("separation", 4);
            AddChild(_mainContainer);

            // Background panel
            var bgPanel = new PanelContainer();
            bgPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bgPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.12f, 0.12f, 0.14f, 0.98f);
            styleBox.BorderWidthTop = 1;
            styleBox.BorderWidthBottom = 1;
            styleBox.BorderWidthLeft = 1;
            styleBox.BorderWidthRight = 1;
            styleBox.BorderColor = new Color(0.3f, 0.3f, 0.35f);
            styleBox.CornerRadiusTopLeft = 6;
            styleBox.CornerRadiusTopRight = 6;
            styleBox.CornerRadiusBottomLeft = 6;
            styleBox.CornerRadiusBottomRight = 6;
            styleBox.ContentMarginTop = 8;
            styleBox.ContentMarginBottom = 8;
            styleBox.ContentMarginLeft = 8;
            styleBox.ContentMarginRight = 8;
            bgPanel.AddThemeStyleboxOverride("panel", styleBox);
            _mainContainer.AddChild(bgPanel);

            var innerContainer = new VBoxContainer();
            innerContainer.AddThemeConstantOverride("separation", 6);
            bgPanel.AddChild(innerContainer);

            // Header label
            var headerLabel = new Label();
            headerLabel.Text = "📁 Select a file";
            headerLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
            headerLabel.AddThemeFontSizeOverride("font_size", 12);
            innerContainer.AddChild(headerLabel);

            // Search input
            _searchInput = new LineEdit();
            _searchInput.PlaceholderText = "Type to search files...";
            _searchInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _searchInput.TextChanged += OnSearchTextChanged;
            _searchInput.GuiInput += OnSearchGuiInput;
            innerContainer.AddChild(_searchInput);

            // Scroll container for file list
            _scrollContainer = new ScrollContainer();
            _scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            innerContainer.AddChild(_scrollContainer);

            // File list container
            _fileListContainer = new VBoxContainer();
            _fileListContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _fileListContainer.AddThemeConstantOverride("separation", 2);
            _scrollContainer.AddChild(_fileListContainer);

            // No results label
            _noResultsLabel = new Label();
            _noResultsLabel.Text = "No matching files found";
            _noResultsLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
            _noResultsLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _noResultsLabel.Visible = false;
            _fileListContainer.AddChild(_noResultsLabel);
        }

        /// <summary>
        /// Shows the popup and initializes with the given query.
        /// </summary>
        /// <param name="initialQuery">Initial search query (text after '@').</param>
        public void ShowWithQuery(string initialQuery = "")
        {
            _currentQuery = initialQuery;
            _selectedIndex = -1;

            // Clear and refresh file list
            RefreshFileList();

            // Set search input text
            _searchInput.Text = initialQuery;
            _searchInput.CaretColumn = initialQuery.Length;

            // Show popup
            Show();

            // Focus search input
            _searchInput.GrabFocus();
        }

        /// <summary>
        /// Refreshes the file list based on the current query.
        /// </summary>
        private void RefreshFileList()
        {
            // Clear existing buttons
            foreach (var button in _fileButtons)
            {
                button.QueueFree();
            }
            _fileButtons.Clear();

            // Get filtered files
            _filteredFiles = FileDiscovery.Instance.SearchFiles(_currentQuery, MaxVisibleResults);

            if (_filteredFiles.Count == 0)
            {
                _noResultsLabel.Visible = true;
                _selectedIndex = -1;
                return;
            }

            _noResultsLabel.Visible = false;

            // Create buttons for each file
            for (int i = 0; i < _filteredFiles.Count; i++)
            {
                var filePath = _filteredFiles[i];
                var button = CreateFileButton(filePath, i);
                _fileListContainer.AddChild(button);
                _fileButtons.Add(button);
            }

            // Select first item by default
            if (_filteredFiles.Count > 0)
            {
                SetSelectedIndex(0);
            }
        }

        /// <summary>
        /// Creates a button for a file entry.
        /// </summary>
        private Button CreateFileButton(string filePath, int index)
        {
            var button = new Button();
            button.Text = $"  📄 @{filePath}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Alignment = HorizontalAlignment.Left;
            button.ClipText = true;

            // Style the button
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.15f, 0.15f, 0.17f, 0.0f);
            normalStyle.ContentMarginLeft = 8;
            normalStyle.ContentMarginRight = 8;
            normalStyle.ContentMarginTop = 6;
            normalStyle.ContentMarginBottom = 6;
            normalStyle.CornerRadiusTopLeft = 4;
            normalStyle.CornerRadiusTopRight = 4;
            normalStyle.CornerRadiusBottomLeft = 4;
            normalStyle.CornerRadiusBottomRight = 4;
            button.AddThemeStyleboxOverride("normal", normalStyle);

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(0.25f, 0.25f, 0.28f, 0.8f);
            hoverStyle.ContentMarginLeft = 8;
            hoverStyle.ContentMarginRight = 8;
            hoverStyle.ContentMarginTop = 6;
            hoverStyle.ContentMarginBottom = 6;
            hoverStyle.CornerRadiusTopLeft = 4;
            hoverStyle.CornerRadiusTopRight = 4;
            hoverStyle.CornerRadiusBottomLeft = 4;
            hoverStyle.CornerRadiusBottomRight = 4;
            button.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = new StyleBoxFlat();
            pressedStyle.BgColor = new Color(0.3f, 0.5f, 0.7f, 0.8f);
            pressedStyle.ContentMarginLeft = 8;
            pressedStyle.ContentMarginRight = 8;
            pressedStyle.ContentMarginTop = 6;
            pressedStyle.ContentMarginBottom = 6;
            pressedStyle.CornerRadiusTopLeft = 4;
            pressedStyle.CornerRadiusTopRight = 4;
            pressedStyle.CornerRadiusBottomLeft = 4;
            pressedStyle.CornerRadiusBottomRight = 4;
            button.AddThemeStyleboxOverride("pressed", pressedStyle);

            button.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.92f));
            button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 1.0f));
            button.AddThemeFontSizeOverride("font_size", 13);

            // Capture index for closure
            int capturedIndex = index;
            button.Pressed += () => OnFileButtonPressed(capturedIndex);
            button.MouseEntered += () => SetSelectedIndex(capturedIndex);

            return button;
        }

        /// <summary>
        /// Sets the selected index and updates button styles.
        /// </summary>
        private void SetSelectedIndex(int index)
        {
            // Remove highlight from previous selection
            if (_selectedIndex >= 0 && _selectedIndex < _fileButtons.Count)
            {
                UpdateButtonHighlight(_fileButtons[_selectedIndex], false);
            }

            _selectedIndex = index;

            // Add highlight to new selection
            if (_selectedIndex >= 0 && _selectedIndex < _fileButtons.Count)
            {
                UpdateButtonHighlight(_fileButtons[_selectedIndex], true);
                EnsureButtonVisible(_fileButtons[_selectedIndex]);
            }
        }

        /// <summary>
        /// Updates the visual highlight state of a button.
        /// </summary>
        private void UpdateButtonHighlight(Button button, bool highlighted)
        {
            if (highlighted)
            {
                var highlightStyle = new StyleBoxFlat();
                highlightStyle.BgColor = new Color(0.3f, 0.5f, 0.7f, 0.6f);
                highlightStyle.ContentMarginLeft = 8;
                highlightStyle.ContentMarginRight = 8;
                highlightStyle.ContentMarginTop = 6;
                highlightStyle.ContentMarginBottom = 6;
                highlightStyle.CornerRadiusTopLeft = 4;
                highlightStyle.CornerRadiusTopRight = 4;
                highlightStyle.CornerRadiusBottomLeft = 4;
                highlightStyle.CornerRadiusBottomRight = 4;
                button.AddThemeStyleboxOverride("normal", highlightStyle);
            }
            else
            {
                var normalStyle = new StyleBoxFlat();
                normalStyle.BgColor = new Color(0.15f, 0.15f, 0.17f, 0.0f);
                normalStyle.ContentMarginLeft = 8;
                normalStyle.ContentMarginRight = 8;
                normalStyle.ContentMarginTop = 6;
                normalStyle.ContentMarginBottom = 6;
                normalStyle.CornerRadiusTopLeft = 4;
                normalStyle.CornerRadiusTopRight = 4;
                normalStyle.CornerRadiusBottomLeft = 4;
                normalStyle.CornerRadiusBottomRight = 4;
                button.AddThemeStyleboxOverride("normal", normalStyle);
            }
        }

        /// <summary>
        /// Ensures the selected button is visible in the scroll container.
        /// </summary>
        private void EnsureButtonVisible(Button button)
        {
            // Get button position relative to scroll container
            var buttonRect = button.GetGlobalRect();
            var scrollRect = _scrollContainer.GetGlobalRect();

            // Calculate if button is outside visible area
            if (buttonRect.Position.Y < scrollRect.Position.Y)
            {
                // Scroll up
                _scrollContainer.ScrollVertical = (int)button.Position.Y;
            }
            else if (buttonRect.Position.Y + buttonRect.Size.Y > scrollRect.Position.Y + scrollRect.Size.Y)
            {
                // Scroll down
                _scrollContainer.ScrollVertical = (int)(button.Position.Y + button.Size.Y - _scrollContainer.Size.Y);
            }
        }

        /// <summary>
        /// Handles search input text changes.
        /// </summary>
        private void OnSearchTextChanged(string newText)
        {
            _currentQuery = newText;
            RefreshFileList();
        }

        /// <summary>
        /// Handles keyboard input on the search field.
        /// </summary>
        private void OnSearchGuiInput(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                switch (keyEvent.Keycode)
                {
                    case Key.Down:
                        // Move selection down
                        if (_filteredFiles.Count > 0)
                        {
                            SetSelectedIndex((_selectedIndex + 1) % _filteredFiles.Count);
                        }
                        GetViewport().SetInputAsHandled();
                        break;

                    case Key.Up:
                        // Move selection up
                        if (_filteredFiles.Count > 0)
                        {
                            SetSelectedIndex(_selectedIndex <= 0 ? _filteredFiles.Count - 1 : _selectedIndex - 1);
                        }
                        GetViewport().SetInputAsHandled();
                        break;

                    case Key.Enter:
                    case Key.KpEnter:
                        // Select current item
                        if (_selectedIndex >= 0 && _selectedIndex < _filteredFiles.Count)
                        {
                            SelectFile(_filteredFiles[_selectedIndex]);
                        }
                        GetViewport().SetInputAsHandled();
                        break;

                    case Key.Escape:
                        // Cancel
                        Cancel();
                        GetViewport().SetInputAsHandled();
                        break;

                    case Key.Tab:
                        // Tab also selects
                        if (_selectedIndex >= 0 && _selectedIndex < _filteredFiles.Count)
                        {
                            SelectFile(_filteredFiles[_selectedIndex]);
                        }
                        GetViewport().SetInputAsHandled();
                        break;
                }
            }
        }

        /// <summary>
        /// Handles file button press.
        /// </summary>
        private void OnFileButtonPressed(int index)
        {
            if (index >= 0 && index < _filteredFiles.Count)
            {
                SelectFile(_filteredFiles[index]);
            }
        }

        /// <summary>
        /// Selects a file and emits the FileSelected signal.
        /// </summary>
        private void SelectFile(string filePath)
        {
            Hide();
            EmitSignal(SignalName.FileSelected, filePath);
        }

        /// <summary>
        /// Cancels the autocomplete and emits the Cancelled signal.
        /// </summary>
        private void Cancel()
        {
            Hide();
            EmitSignal(SignalName.Cancelled);
        }

        public override void _Input(InputEvent @event)
        {
            if (!Visible)
            {
                return;
            }

            // Handle clicks outside the popup
            if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
            {
                // For Window-based popups, we use the global mouse position from the event
                // and check against the popup's position and size (which are in screen coords)
                var globalMousePos = mouseEvent.GlobalPosition;
                var popupRect = new Rect2(Position, Size);
                
                if (!popupRect.HasPoint(globalMousePos))
                {
                    Cancel();
                }
            }
        }
    }
}
#endif

