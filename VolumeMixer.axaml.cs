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
    using System.Drawing;              // For Icon, Bitmap
    using System.Drawing.Imaging;     // For ImageFormat
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


            }
            
            private void PresetButton_Click(object? sender, RoutedEventArgs e)
{
    UpdateAudioSessionIcons();
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

        // Try to get the display name
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

        var menuItem = new MenuItem { Header = name };
        menuItem.Click += (s, e) =>
        {
            session.SimpleAudioVolume.Mute = !session.SimpleAudioVolume.Mute;

            var icon = GetIconForSession(session);
            if (icon != null)
            {
                // Use the sender (the button that was actually clicked)
                var clickedButton = sender as Button;
                if (clickedButton != null)
                {
                    clickedButton.Content = new Avalonia.Controls.Image
                    {
                        Source = icon,
                        Width = 12,
                        Height = 12,
                        Stretch = Avalonia.Media.Stretch.Uniform
                    };
                    
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
    UpdateAudioSessionIcons();
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
        
                    using (var stream = new MemoryStream())
                    {
                        icon.ToBitmap().Save(stream, ImageFormat.Png);
                        stream.Seek(0, SeekOrigin.Begin);
            
                        var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            
                        return bitmap;
                    }
                }
                catch (Exception ex)
                {

                }
                
                return null;
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

                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessions = device.AudioSessionManager.Sessions;

                for (int i = 0; i < Math.Min(sessions.Count, buttons.Length); i++)
                {
                    var session = sessions[i];
                    var icon = GetIconForSession(session);

                    if (buttons[i] != null)
                    {
                        SetIconToButton(buttons[i], icon);

                        // Remove previous handlers to avoid multiple subscriptions
                        buttons[i].Click -= Buttons_ClickHandler;  
                        buttons[i].Click += Buttons_ClickHandler;

                        // Local method to capture session for click events
                        void Buttons_ClickHandler(object? sender, RoutedEventArgs e)
                        {
                            session.SimpleAudioVolume.Mute = !session.SimpleAudioVolume.Mute;
                        }
                    }
                }
            }
            
        }
    }




