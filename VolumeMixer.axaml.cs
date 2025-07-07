using System.Collections.ObjectModel;
using Avalonia.Controls;
using Docklys.ModuleContracts;
using Avalonia.Media;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Linq; // Add this line
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using Avalonia.Threading;
using System.IO;
using Newtonsoft.Json;

namespace VolumeMixer
{
    public partial class VolumeMixer : UserControl, IModule
    {
        // Identification
        public string Id => "VolumeMixer";
        public string ModuleName => "Volume Mixer";
        public string ModuleVersion => "1.0.0";
        public string Category => "QuickTools";
        public string[] Tags => new[] { "Volume", "Media", "Audio", "Music", "Mixer" };

        // Layout info
        public int TileWidth => 1;
        public int TileHeight => 1;

        // Compatibility
        public string MinAppVersion => "1.0.0";
        public string MaxAppVersion => "1.0.0";
        public string[] SupportedPlatforms => new[] { "Windows" };

        // Not Imodule anymore, this is set by main app
        public string UniqueModuleId { get; private set; }

        public void SetModuleId(string uniquemoduleId)
        {
            UniqueModuleId = uniquemoduleId;
        }

        public void PrintModuleId()
        {
            Console.WriteLine($"Module ID: {UniqueModuleId}");
        }

        public VolumeMixer()
        {
            InitializeComponent();

            // Initialize audio sessions and sliders
            this.Loaded += (s, e) =>
            {
                UpdateAudioSessionIcons();
                Debug.WriteLine("[DEBUG] VolumeMixer loaded and audio sessions initialized");
            };

            // Start the volume sync timer
            _volumeUpdateTimer = new System.Threading.Timer(UpdateVolumesFromSessions, null, 1000, 500);

        }
        private Dictionary<string, (AudioSessionControl session, IImage icon)> _buttonSessions = new();
        private Dictionary<string, bool> _buttonHasManualIcon = new();
        private Dictionary<string, (AudioSessionControl session, Slider slider)> _sliderSessions = new();
        private System.Threading.Timer? _volumeUpdateTimer;
        private void PresetButton_Click(object? sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout
            {
                Placement = PlacementMode.Center,
                VerticalOffset = -11
            };

            // Get active audio sessions
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var sessionIndex = i; // Capture for closure

                // Try to get the display name
                string name = GetSessionDisplayName(session);

                var menuItem = new MenuItem { Header = name };
                menuItem.Click += (s, e) =>
                {
                    // Get the clicked button
                    var clickedButton = sender as Button;
                    if (clickedButton != null)
                    {
                        var buttonName = clickedButton.Name ?? "";
                        Debug.WriteLine($"[DEBUG] Manual assignment: {buttonName} -> {name}");

                        var icon = GetIconForSession(session);
                        if (icon != null)
                        {
                            // Set the icon on the button
                            SetIconToButton(clickedButton, icon);

                            // Mark this button as having a manual icon and store the session
                            _buttonHasManualIcon[buttonName] = true;
                            _buttonSessions[buttonName] = (session, icon);

                            Debug.WriteLine($"[DEBUG] Stored manual assignment for button: {buttonName}");

                            // Find the corresponding slider and update the session mapping
                            var sliderName = buttonName.Replace("SourceIcon", "VolumeSlider");
                            var slider = this.FindControl<Slider>(sliderName);

                            if (slider != null)
                            {
                                // Update slider value to match session volume
                                slider.Value = session.SimpleAudioVolume.Volume * 100;

                                // Update the slider-session mapping
                                _sliderSessions[sliderName] = (session, slider);

                                Debug.WriteLine($"[DEBUG] Updated slider {sliderName} mapping to session {name}");
                            }

                            // Update the slider source mapping
                            UpdateSliderSource(sliderName, name);
                        }
                    }
                };

                flyout.Items.Add(menuItem);
            }

            if (flyout.Items.Count == 0)
            {
                flyout.Items.Add(new MenuItem { Header = "No active audio sessions" });
            }

            var dockPanel = this.FindControl<DockPanel>("RootDockPanel");
            if (dockPanel != null)
            {
                flyout.ShowAt(dockPanel, showAtPointer: false);
            }
        }


        private MenuItem CreateMenuItem(string header)
        {
            var menuItem = new MenuItem { Header = header };
            menuItem.Click += (s, e) => OnPresetSelected(header);
            menuItem.PointerPressed += async (sender, e) =>
            {
                if (e.GetCurrentPoint(menuItem).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
                {
                    e.Handled = true;

                    // Wait for the menu system to finish its pointer up handling
                    await Task.Delay(100);

                    StartRename(menuItem);
                }
            };
            return menuItem;
        }

        private void StartRename(MenuItem menuItem)
        {
            var currentText = menuItem.Header?.ToString() ?? "";
            if (currentText == "Custom...") return; // Don't rename the Custom option

            var textBox = new TextBox
            {
                Text = currentText,
                Classes = { "inline-edit" },
                MaxLength = 8
            };

            menuItem.Header = textBox;
            textBox.Focus();
            textBox.SelectAll();

            textBox.LostFocus += (s, e) => FinishRename(menuItem, textBox);
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    FinishRename(menuItem, textBox);
                }
                else if (e.Key == Key.Escape)
                {
                    menuItem.Header = currentText; // Restore original text
                }
            };
        }

        private void FinishRename(MenuItem menuItem, TextBox textBox)
        {
            var newText = textBox.Text?.Trim();
            if (string.IsNullOrEmpty(newText))
            {
                newText = "Preset"; // Default name if empty
            }

            menuItem.Header = newText;
        }

        private void OnPresetSelected(string preset)
        {
            // Update the button text
            var presetButton = this.FindControl<DropDownButton>("PresetButton");
            if (presetButton != null)
            {
                presetButton.Content = preset;
            }

            // Handle the selected preset
            switch (preset)
            {
                case "Gaming":
                    // Apply gaming preset settings
                    break;
                case "Music":
                    // Apply music preset settings
                    break;
                // ... other cases
            }
        }

        private IImage? GetIconForSession(AudioSessionControl session)
        {
            try
            {
                Debug.WriteLine($"[DEBUG] Getting icon for session: {session.DisplayName}");

                var processId = (int)session.GetProcessID;
                Debug.WriteLine($"[DEBUG] Process ID: {processId}");

                var process = Process.GetProcessById(processId);
                Debug.WriteLine($"[DEBUG] Process name: {process.ProcessName}");
                Debug.WriteLine($"[DEBUG] Main module path: {process.MainModule?.FileName ?? "NULL"}");

                if (process.MainModule?.FileName == null)
                {
                    Debug.WriteLine("[DEBUG] No main module filename found");
                    return null;
                }

                var icon = Icon.ExtractAssociatedIcon(process.MainModule.FileName);
                if (icon == null)
                {
                    Debug.WriteLine("[DEBUG] No icon extracted from file");
                    return null;
                }

                Debug.WriteLine("[DEBUG] Icon extracted successfully, applying grayscale filter");

                // Convert to bitmap and apply grayscale + white tint
                using (var originalBitmap = icon.ToBitmap())
                {
                    var processedBitmap = ApplyGrayscaleWithWhiteTint(originalBitmap);

                    using (var stream = new MemoryStream())
                    {
                        processedBitmap.Save(stream, ImageFormat.Png);
                        stream.Seek(0, SeekOrigin.Begin);

                        Debug.WriteLine($"[DEBUG] Processed icon saved to stream, size: {stream.Length} bytes");

                        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                        Debug.WriteLine($"[DEBUG] Avalonia bitmap created: {avaloniaBitmap.PixelSize.Width}x{avaloniaBitmap.PixelSize.Height}");

                        processedBitmap.Dispose();
                        return avaloniaBitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] Exception in GetIconForSession: {ex.GetType().Name}");
                Debug.WriteLine($"[DEBUG] Exception message: {ex.Message}");
                Debug.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");

                Console.WriteLine($"Failed to get icon: {ex.Message}");
            }

            Debug.WriteLine("[DEBUG] Returning null icon");
            return null;
        }

        private System.Drawing.Bitmap ApplyGrayscaleWithWhiteTint(System.Drawing.Bitmap original)
        {
            var processed = new System.Drawing.Bitmap(original.Width, original.Height);

            // Contrast settings
            float contrastMultiplier = 3.0f;  // Higher = more contrast
            int contrastThreshold = 128;      // Midpoint (0-255)

            for (int x = 0; x < original.Width; x++)
            {
                for (int y = 0; y < original.Height; y++)
                {
                    var pixel = original.GetPixel(x, y);

                    // Preserve alpha channel
                    if (pixel.A == 0)
                    {
                        processed.SetPixel(x, y, pixel);
                        continue;
                    }

                    // Convert to grayscale using luminance formula
                    int gray = (int)(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);

                    // Apply high contrast transformation
                    // Formula: newGray = threshold + (gray - threshold) * multiplier
                    int contrastedGray = contrastThreshold + (int)((gray - contrastThreshold) * contrastMultiplier);

                    // Clamp to 0-255 range
                    contrastedGray = Math.Max(0, Math.Min(255, contrastedGray));

                    // Apply threshold-based black/white conversion
                    int finalValue;
                    if (contrastedGray > contrastThreshold)
                    {
                        // Bright areas -> push towards white
                        finalValue = 180 + (int)((contrastedGray - contrastThreshold) * 0.6f);
                    }
                    else
                    {
                        // Dark areas -> push towards black
                        finalValue = (int)(contrastedGray * 0.4f);
                    }

                    // Final clamp
                    finalValue = Math.Max(0, Math.Min(255, finalValue));

                    processed.SetPixel(x, y, System.Drawing.Color.FromArgb(pixel.A, finalValue, finalValue, finalValue));
                }
            }

            Debug.WriteLine($"[DEBUG] Applied high contrast grayscale (multiplier: {contrastMultiplier}, threshold: {contrastThreshold})");
            return processed;
        }
        private void SetIconToButton(Button button, IImage? icon)
        {
            if (icon == null)
            {
                button.Content = "≡"; // fallback icon/text
                return;
            }

            var image = new Avalonia.Controls.Image
            {
                Source = icon,
                Width = 16,
                Height = 16,
                Stretch = Avalonia.Media.Stretch.Uniform
            };

            button.Content = image;
        }


        private void UpdateAudioSessionIcons()
{
    Debug.WriteLine("[DEBUG] === STARTING UpdateAudioSessionIcons ===");

    var buttons = new[] {
        this.FindControl<Button>("SourceIcon1"),
        this.FindControl<Button>("SourceIcon2"),
        this.FindControl<Button>("SourceIcon3"),
        this.FindControl<Button>("SourceIcon4"),
    };

    var sliders = new[] {
        this.FindControl<Slider>("VolumeSlider1"),
        this.FindControl<Slider>("VolumeSlider2"),
        this.FindControl<Slider>("VolumeSlider3"),
        this.FindControl<Slider>("VolumeSlider4"),
    };

    var enumerator = new MMDeviceEnumerator();
    var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    var sessions = device.AudioSessionManager.Sessions;

    Debug.WriteLine($"[DEBUG] Found {sessions.Count} audio sessions");

    // First, try to load from JSON for each slider
    Debug.WriteLine("[DEBUG] === ATTEMPTING TO LOAD FROM JSON FIRST ===");
    for (int i = 0; i < sliders.Length; i++)
    {
        var slider = sliders[i];
        var sliderName = slider?.Name ?? $"Slider{i}";
        
        if (slider != null)
        {
            Debug.WriteLine($"[DEBUG] Attempting to load slider {sliderName} from JSON...");
            UpdateSliderFromJson(slider, sliderName);
        }
    }

    // Then handle button assignments and any sliders that weren't loaded from JSON
    for (int i = 0; i < buttons.Length; i++)
    {
        var button = buttons[i];
        var slider = sliders[i];
        var buttonName = button?.Name ?? $"Button{i}";
        var sliderName = slider?.Name ?? $"Slider{i}";

        if (button != null && slider != null)
        {
            Debug.WriteLine($"[DEBUG] Processing button/slider pair: {buttonName}/{sliderName}");
            
            // Check if this button has a manual assignment
            if (_buttonHasManualIcon.ContainsKey(buttonName) && _buttonHasManualIcon[buttonName])
            {
                Debug.WriteLine($"[DEBUG] Button {buttonName} has manual assignment - preserving it");

                // Use the manually assigned session
                if (_buttonSessions.ContainsKey(buttonName))
                {
                    var (manualSession, manualIcon) = _buttonSessions[buttonName];

                    // Keep the manual icon
                    SetIconToButton(button, manualIcon);

                    // Set up slider with manual session
                    slider.Value = manualSession.SimpleAudioVolume.Volume * 100;

                    // Remove previous handlers
                    slider.ValueChanged -= OnSliderValueChanged;

                    // Add new handlers
                    slider.ValueChanged += OnSliderValueChanged;

                    // Store the session-slider relationship
                    _sliderSessions[sliderName] = (manualSession, slider);

                    Debug.WriteLine($"[DEBUG] Preserved manual assignment for {buttonName} -> {GetSessionDisplayName(manualSession)}");
                }
                continue; // Skip auto-assignment for this button
            }

            // Check if this slider was already loaded from JSON
            if (_sliderSessions.ContainsKey(sliderName))
            {
                Debug.WriteLine($"[DEBUG] Slider {sliderName} was loaded from JSON, setting up corresponding button");
                
                var (jsonSession, _) = _sliderSessions[sliderName];
                var icon = GetIconForSession(jsonSession);
                
                Debug.WriteLine($"[DEBUG] Setting button {buttonName} icon for JSON-loaded session {GetSessionDisplayName(jsonSession)}");
                SetIconToButton(button, icon);
                
                // Remove previous handlers
                slider.ValueChanged -= OnSliderValueChanged;
                // Add new handlers
                slider.ValueChanged += OnSliderValueChanged;
                
                continue; // Skip auto-assignment for this button/slider pair
            }

            // Auto-assign from available sessions (skip manually assigned ones and JSON-loaded ones)
            if (i < sessions.Count)
            {
                var session = sessions[i];
                var icon = GetIconForSession(session);

                Debug.WriteLine($"[DEBUG] Auto-assigning session {GetSessionDisplayName(session)} to button {buttonName}");

                SetIconToButton(button, icon);

                // Set initial slider value from current session volume
                slider.Value = session.SimpleAudioVolume.Volume * 100;

                // Remove previous handlers
                slider.ValueChanged -= OnSliderValueChanged;

                // Add new handlers
                slider.ValueChanged += OnSliderValueChanged;

                // Store the session-slider relationship
                _sliderSessions[sliderName] = (session, slider);
            }
            else
            {
                Debug.WriteLine($"[DEBUG] No session available for button {buttonName}");
                // Clear button if no session available
                SetIconToButton(button, null);
                if (_sliderSessions.ContainsKey(sliderName))
                {
                    _sliderSessions.Remove(sliderName);
                }
            }
        }
    }
    
    Debug.WriteLine("[DEBUG] === COMPLETED UpdateAudioSessionIcons ===");
}

        private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is Slider slider)
            {
                var sliderName = slider.Name ?? "Unknown";
                var oldValue = e.OldValue;
                var newValue = e.NewValue;

                Debug.WriteLine($"[DEBUG] SLIDER CHANGED - {sliderName}: {oldValue:F1}% → {newValue:F1}%");

                if (_sliderSessions.ContainsKey(sliderName))
                {
                    var (session, _) = _sliderSessions[sliderName];
                    try
                    {
                        string sessionName = GetSessionDisplayName(session);
                        float oldSystemVolume = session.SimpleAudioVolume.Volume;

                        // Convert slider value (0-100) to volume (0.0-1.0)
                        float newSystemVolume = (float)(newValue / 100.0);

                        Debug.WriteLine($"[DEBUG] VOLUME UPDATE - {sessionName}:");
                        Debug.WriteLine($"[DEBUG]   System Volume: {oldSystemVolume:P1} → {newSystemVolume:P1}");
                        Debug.WriteLine($"[DEBUG]   Slider Value: {oldValue:F1}% → {newValue:F1}%");

                        session.SimpleAudioVolume.Volume = newSystemVolume;

                        // Verify the change was applied
                        float actualVolume = session.SimpleAudioVolume.Volume;
                        Debug.WriteLine($"[DEBUG]   Actual Result: {actualVolume:P1} ({actualVolume * 100:F1}%)");

                        if (Math.Abs(actualVolume - newSystemVolume) > 0.01f)
                        {
                            Debug.WriteLine($"[DEBUG]   ⚠️  WARNING: Volume not set correctly! Expected {newSystemVolume:P1}, got {actualVolume:P1}");
                        }
                        else
                        {
                            Debug.WriteLine($"[DEBUG]   ✅ Volume successfully updated for {sessionName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DEBUG] ❌ ERROR setting volume for slider {sliderName}: {ex.Message}");
                        Debug.WriteLine($"[DEBUG]   Exception Type: {ex.GetType().Name}");
                        Debug.WriteLine($"[DEBUG]   Stack Trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[DEBUG] ⚠️  WARNING: No session found for slider {sliderName}");
                    Debug.WriteLine($"[DEBUG]   Available sliders: {string.Join(", ", _sliderSessions.Keys)}");
                }
            }
            else
            {
                Debug.WriteLine($"[DEBUG] ❌ ERROR: Sender is not a Slider, got {sender?.GetType().Name ?? "null"}");
            }
        }
        private void UpdateVolumesFromSessions(object? state)
        {
            Dispatcher.UIThread.Post(() =>
            {

                foreach (var kvp in _sliderSessions)
                {
                    try
                    {
                        var sliderName = kvp.Key;
                        var (session, slider) = kvp.Value;

                        string sessionName = GetSessionDisplayName(session);
                        float systemVolume = session.SimpleAudioVolume.Volume;
                        double currentSliderValue = slider.Value;
                        double expectedSliderValue = systemVolume * 100;

                        // Only update if different to avoid feedback loops
                        if (Math.Abs(currentSliderValue - expectedSliderValue) > 1)
                        {
                            Debug.WriteLine($"[DEBUG] SYNC UPDATE - {sessionName} (Slider: {sliderName}):");
                            Debug.WriteLine($"[DEBUG]   System Volume: {systemVolume:P1} ({systemVolume * 100:F1}%)");
                            Debug.WriteLine($"[DEBUG]   Slider Value: {currentSliderValue:F1}% → {expectedSliderValue:F1}%");

                            slider.Value = expectedSliderValue;

                            Debug.WriteLine($"[DEBUG]   ✅ Synced slider to system volume");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DEBUG] ❌ ERROR during sync for {kvp.Key}: {ex.Message}");
                    }
                }
                
            });
        }
        private string GetSessionDisplayName(AudioSessionControl session)
        {
            string name = session.DisplayName;

            if (string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    var proc = Process.GetProcessById((int)session.GetProcessID);
                    name = proc.ProcessName;
                }
                catch
                {
                    name = "Unknown";
                }
            }

            return name;
        }

        private string GetJsonFilePath()
        {
            string moduleId = string.IsNullOrEmpty(UniqueModuleId) ? "NoIdSet" : UniqueModuleId;
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Docklys", "VolumeMixer");
            Directory.CreateDirectory(appDataPath);
            string filePath = Path.Combine(appDataPath, $"VolumeMixer_{moduleId}.json");
    
            Debug.WriteLine($"[DEBUG] JSON file path: {filePath}");
            Debug.WriteLine($"[DEBUG] Module ID: {moduleId}");
            Debug.WriteLine($"[DEBUG] App data path: {appDataPath}");
    
            return filePath;
        }

        public void SaveSliderPaths(Dictionary<string, string> sliderPaths)
        {
            string filePath = GetJsonFilePath();
            string jsonContent = JsonConvert.SerializeObject(sliderPaths, Formatting.Indented);
    
            Debug.WriteLine($"[DEBUG] === SAVING SLIDER PATHS ===");
            Debug.WriteLine($"[DEBUG] File path: {filePath}");
            Debug.WriteLine($"[DEBUG] Number of entries to save: {sliderPaths.Count}");
            Debug.WriteLine($"[DEBUG] JSON content to save:");
            Debug.WriteLine(jsonContent);
    
            try
            {
                File.WriteAllText(filePath, jsonContent);
                Debug.WriteLine($"[DEBUG] ✅ Successfully saved slider paths to JSON");
        
                // Verify the file was written
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    Debug.WriteLine($"[DEBUG] File size: {fileInfo.Length} bytes");
                    Debug.WriteLine($"[DEBUG] Last modified: {fileInfo.LastWriteTime}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] ❌ ERROR saving slider paths: {ex.Message}");
                Debug.WriteLine($"[DEBUG] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
            }
        }


public Dictionary<string, string> LoadSliderPaths()
{
    string filePath = GetJsonFilePath();
    
    Debug.WriteLine($"[DEBUG] === LOADING SLIDER PATHS ===");
    Debug.WriteLine($"[DEBUG] File path: {filePath}");
    Debug.WriteLine($"[DEBUG] File exists: {File.Exists(filePath)}");
    
    if (File.Exists(filePath))
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            Debug.WriteLine($"[DEBUG] File size: {fileInfo.Length} bytes");
            Debug.WriteLine($"[DEBUG] Last modified: {fileInfo.LastWriteTime}");
            
            string jsonContent = File.ReadAllText(filePath);
            Debug.WriteLine($"[DEBUG] Raw JSON content from file:");
            Debug.WriteLine(jsonContent);
            
            var sliderPaths = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
            
            if (sliderPaths != null)
            {
                Debug.WriteLine($"[DEBUG] Successfully loaded {sliderPaths.Count} slider paths:");
                foreach (var kvp in sliderPaths)
                {
                    Debug.WriteLine($"[DEBUG]   {kvp.Key} -> {kvp.Value}");
                }
            }
            else
            {
                Debug.WriteLine($"[DEBUG] ⚠️  Deserialization returned null, creating empty dictionary");
                sliderPaths = new Dictionary<string, string>();
            }
            
            return sliderPaths;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DEBUG] ❌ ERROR loading slider paths: {ex.Message}");
            Debug.WriteLine($"[DEBUG] Exception type: {ex.GetType().Name}");
            Debug.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
            Debug.WriteLine($"[DEBUG] Returning empty dictionary due to error");
            return new Dictionary<string, string>();
        }
    }
    else
    {
        Debug.WriteLine($"[DEBUG] File doesn't exist, returning empty dictionary");
        return new Dictionary<string, string>();
    }
}

private void UpdateSliderSource(string sliderName, string sourceName)
{
    Debug.WriteLine($"[DEBUG] === UPDATING SLIDER SOURCE ===");
    Debug.WriteLine($"[DEBUG] Slider name: {sliderName}");
    Debug.WriteLine($"[DEBUG] Source name: {sourceName}");
    
    // Load existing slider paths
    var sliderPaths = LoadSliderPaths();
    Debug.WriteLine($"[DEBUG] Loaded {sliderPaths.Count} existing slider paths");
    
    // Update the mapping
    string oldValue = sliderPaths.ContainsKey(sliderName) ? sliderPaths[sliderName] : "NOT_SET";
    sliderPaths[sliderName] = sourceName;
    
    Debug.WriteLine($"[DEBUG] Updated mapping: {sliderName} -> {oldValue} to {sourceName}");
    
    // Save the updated paths
    SaveSliderPaths(sliderPaths);
    
    Debug.WriteLine($"[DEBUG] ✅ Slider source update completed");
}

        private void UpdateSliderFromJson(Slider slider, string sliderName)
{
    Debug.WriteLine($"[DEBUG] === UPDATING SLIDER FROM JSON ===");
    Debug.WriteLine($"[DEBUG] Slider name: {sliderName}");
    Debug.WriteLine($"[DEBUG] Current slider value: {slider.Value}");
    
    var enumerator = new MMDeviceEnumerator();
    var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    var sessions = device.AudioSessionManager.Sessions;
    
    Debug.WriteLine($"[DEBUG] Found {sessions.Count} audio sessions");
    
    // List all available sessions
    Debug.WriteLine($"[DEBUG] Available audio sessions:");
    for (int i = 0; i < sessions.Count; i++)
    {
        var session = sessions[i];
        string sessionName = GetSessionDisplayName(session);
        Debug.WriteLine($"[DEBUG]   [{i}] {sessionName} (Volume: {session.SimpleAudioVolume.Volume:P1})");
    }
    
    Debug.WriteLine("[DEBUG] Checking if slider paths exist in JSON...");
    var sliderPaths = LoadSliderPaths();
    
    if (sliderPaths.ContainsKey(sliderName))
    {
        Debug.WriteLine($"[DEBUG] Found slider path in JSON: {sliderName} -> {sliderPaths[sliderName]}");
        var targetSessionName = sliderPaths[sliderName];
        
        // Manual iteration instead of FirstOrDefault
        AudioSessionControl sessionFromJson = null;
        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            var sessionName = GetSessionDisplayName(session);
            Debug.WriteLine($"[DEBUG] Comparing session '{sessionName}' with target '{targetSessionName}'");
            
            if (sessionName == targetSessionName)
            {
                sessionFromJson = session;
                Debug.WriteLine($"[DEBUG] ✅ Found matching session at index {i}");
                break;
            }
        }
        
        if (sessionFromJson != null)
        {
            float sessionVolume = sessionFromJson.SimpleAudioVolume.Volume;
            double newSliderValue = sessionVolume * 100;
            
            Debug.WriteLine($"[DEBUG] Loaded session from JSON: {targetSessionName}");
            Debug.WriteLine($"[DEBUG] Session volume: {sessionVolume:P1} ({sessionVolume * 100:F1}%)");
            Debug.WriteLine($"[DEBUG] Setting slider value: {slider.Value} -> {newSliderValue}");
            
            slider.Value = newSliderValue;
            _sliderSessions[sliderName] = (sessionFromJson, slider);
            
            Debug.WriteLine($"[DEBUG] ✅ Successfully updated slider from JSON");
            return;
        }
        else
        {
            Debug.WriteLine($"[DEBUG] ❌ Session from JSON not found: {targetSessionName}");
            Debug.WriteLine($"[DEBUG] Available sessions were: {string.Join(", ", Enumerable.Range(0, sessions.Count).Select(i => GetSessionDisplayName(sessions[i])))}");
        }
    }
    else
    {
        Debug.WriteLine($"[DEBUG] No slider path found in JSON for: {sliderName}");
        Debug.WriteLine($"[DEBUG] Available JSON keys: {string.Join(", ", sliderPaths.Keys)}");
    }

    Debug.WriteLine("[DEBUG] Falling back to default behavior.");
    Debug.WriteLine($"[DEBUG] Setting slider value to default: {slider.Value} -> 50");
    slider.Value = 50; // Default value
    
    Debug.WriteLine($"[DEBUG] === SLIDER UPDATE FROM JSON COMPLETED ===");
}
    }
}
