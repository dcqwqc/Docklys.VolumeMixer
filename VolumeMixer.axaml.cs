using Avalonia.Controls;
using Docklys.ModuleContracts;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Avalonia.Threading;
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

        // Unique Module ID (set by the main app)
        private string _uniqueModuleId;
        public string UniqueModuleId { get { return _uniqueModuleId; } }

        public void SetModuleId(string uniqueModuleId)
        {
            _uniqueModuleId = uniqueModuleId;
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

                        var icon = GetIconForSession(session);
                        if (icon != null)
                        {
                            // Set the icon on the button
                            SetIconToButton(clickedButton, icon);

                            // Mark this button as having a manual icon and store the session
                            _buttonHasManualIcon[buttonName] = true;
                            _buttonSessions[buttonName] = (session, icon);
                            
                            // Find the corresponding slider and update the session mapping
                            var sliderName = buttonName.Replace("SourceIcon", "VolumeSlider");
                            var slider = this.FindControl<Slider>(sliderName);

                            if (slider != null)
                            {
                                // Update slider value to match session volume
                                slider.Value = session.SimpleAudioVolume.Volume * 100;

                                // Update the slider-session mapping
                                _sliderSessions[sliderName] = (session, slider);
                                
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
                newText = "Preset"; // Default
            }

            menuItem.Header = newText;
        }

        private void OnPresetSelected(string preset)
        {
            //lagacy shit but idont wanna remove it
        }

        private IImage? GetIconForSession(AudioSessionControl session)
        {
            try
            {
                
                var processId = (int)session.GetProcessID;
                
                var process = Process.GetProcessById(processId);
                if (process.MainModule?.FileName == null)
                {
                    return null;
                }

                var icon = Icon.ExtractAssociatedIcon(process.MainModule.FileName);
                if (icon == null)
                {
                    return null;
                }
                
                // Convert to bitmap and apply grayscale + white tint
                using (var originalBitmap = icon.ToBitmap())
                {
                    var processedBitmap = ApplyGrayscaleWithWhiteTint(originalBitmap);

                    using (var stream = new MemoryStream())
                    {
                        processedBitmap.Save(stream, ImageFormat.Png);
                        stream.Seek(0, SeekOrigin.Begin);
                        var avaloniaBitmap = new Avalonia.Media.Imaging.Bitmap(stream);
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
    

    // First, try to load from JSON for each slider
    for (int i = 0; i < sliders.Length; i++)
    {
        var slider = sliders[i];
        var sliderName = slider?.Name ?? $"Slider{i}";
        
        if (slider != null)
        {
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
            // Check if this button has a manual assignment
            if (_buttonHasManualIcon.ContainsKey(buttonName) && _buttonHasManualIcon[buttonName])
            {
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
                }
                continue; // Skip auto-assignment for this button
            }

            // Check if this slider was already loaded from JSON
            if (_sliderSessions.ContainsKey(sliderName))
            {
                var (jsonSession, _) = _sliderSessions[sliderName];
                var icon = GetIconForSession(jsonSession);
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
                // Clear button if no session available
                SetIconToButton(button, null);
                if (_sliderSessions.ContainsKey(sliderName))
                {
                    _sliderSessions.Remove(sliderName);
                }
            }
        }
    }
    
}

        private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is Slider slider)
            {
                var sliderName = slider.Name ?? "Unknown";
                var oldValue = e.OldValue;
                var newValue = e.NewValue;
                
                if (_sliderSessions.ContainsKey(sliderName))
                {
                    var (session, _) = _sliderSessions[sliderName];
                    try
                    {
                        string sessionName = GetSessionDisplayName(session);
                        float oldSystemVolume = session.SimpleAudioVolume.Volume;

                        // Convert slider value (0-100) to volume (0.0-1.0)
                        float newSystemVolume = (float)(newValue / 100.0);
                        
                        session.SimpleAudioVolume.Volume = newSystemVolume;

                        // Verify the change was applied
                        float actualVolume = session.SimpleAudioVolume.Volume;
                                            }
                    catch (Exception ex)
                    {
                        
                        
                    }
                }
                else
                {
                }
            }
            else
            {
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
                            slider.Value = expectedSliderValue;
                            
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

            return filePath;
        }

        public void SaveSliderPaths(Dictionary<string, string> sliderPaths)
        {
            string filePath = GetJsonFilePath();
            string jsonContent = JsonConvert.SerializeObject(sliderPaths, Formatting.Indented);

            try
            {
                File.WriteAllText(filePath, jsonContent);

                // Verify the file was written
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] Exception type: {ex.GetType().Name}");
            }
        }


public Dictionary<string, string> LoadSliderPaths()
{
    string filePath = GetJsonFilePath();
    

    if (File.Exists(filePath))
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            string jsonContent = File.ReadAllText(filePath);

            var sliderPaths = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);

            if (sliderPaths != null)
            {

            }
            else
            { 
                sliderPaths = new Dictionary<string, string>();
            }

            return sliderPaths;
        }
        catch (Exception ex)
        {
            return new Dictionary<string, string>();
        }
    }
    else
    {
        return new Dictionary<string, string>();
    }
}

private void UpdateSliderSource(string sliderName, string sourceName)
{

    // Load existing slider paths
    var sliderPaths = LoadSliderPaths();

    // Update the mapping
    string oldValue = sliderPaths.ContainsKey(sliderName) ? sliderPaths[sliderName] : "NOT_SET";
    sliderPaths[sliderName] = sourceName;

    // Save the updated paths
    SaveSliderPaths(sliderPaths);
    
}

        private void UpdateSliderFromJson(Slider slider, string sliderName)
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            

            // List all available sessions
                        for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                string sessionName = GetSessionDisplayName(session);
            }
            var sliderPaths = LoadSliderPaths();

            if (sliderPaths.ContainsKey(sliderName))
            { var targetSessionName = sliderPaths[sliderName];

                // Manual iteration instead of FirstOrDefault
                AudioSessionControl sessionFromJson = null;
                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    var sessionName = GetSessionDisplayName(session);
                    if (sessionName == targetSessionName)
                    {
                        sessionFromJson = session;
                        break;
                    }
                }
                
                if (sessionFromJson != null)
                {
                    float sessionVolume = sessionFromJson.SimpleAudioVolume.Volume;
                    double newSliderValue = sessionVolume * 100;
                    
                    slider.Value = newSliderValue;
                    _sliderSessions[sliderName] = (sessionFromJson, slider);
                    
                    return;
                }
                else
                {
                }
            }
            else
            {
                            }
            
            slider.Value = 50; // Default value
        }
    }
}
