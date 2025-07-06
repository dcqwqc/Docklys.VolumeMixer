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
    Debug.WriteLine("[DEBUG] === UpdateAudioSessionIcons() Started ===");
    
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
                    button.Click -= GetButtonClickHandler(manualSession);
                    
                    // Add new handlers
                    slider.ValueChanged += OnSliderValueChanged;
                    button.Click += GetButtonClickHandler(manualSession);
                    
                    // Store the session-slider relationship
                    _sliderSessions[sliderName] = (manualSession, slider);
                    
                    Debug.WriteLine($"[DEBUG] Preserved manual assignment for {buttonName} -> {GetSessionDisplayName(manualSession)}");
                }
                continue; // Skip auto-assignment for this button
            }
            
            // Auto-assign from available sessions (skip manually assigned ones)
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
                button.Click -= GetButtonClickHandler(session);
                
                // Add new handlers
                slider.ValueChanged += OnSliderValueChanged;
                button.Click += GetButtonClickHandler(session);
                
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
    
    Debug.WriteLine("[DEBUG] === UpdateAudioSessionIcons() Completed ===");
}
            private EventHandler<RoutedEventArgs> GetButtonClickHandler(AudioSessionControl session)
            {
                return (sender, e) =>
                {
                    session.SimpleAudioVolume.Mute = !session.SimpleAudioVolume.Mute;
                    Debug.WriteLine($"[DEBUG] Toggled mute for {GetSessionDisplayName(session)}");
                };
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
                    Debug.WriteLine("[DEBUG] === Periodic Volume Sync Started ===");
        
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
        
                    Debug.WriteLine("[DEBUG] === Periodic Volume Sync Completed ===");
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
            
        }
    }




