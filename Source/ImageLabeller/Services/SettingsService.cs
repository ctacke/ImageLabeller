using ImageLabeller.Models;
using System;
using System.IO;
using System.Text.Json;

namespace ImageLabeller.Services
{
    public class SettingsService
    {
        private static readonly string SettingsFileName = "user.settings";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _settingsPath;

        public SettingsService()
        {
            var appFolder = AppDomain.CurrentDomain.BaseDirectory;
            _settingsPath = Path.Combine(appFolder, SettingsFileName);
        }

        public UserSettings Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();

                    // Migration: Initialize ClassNames if null or empty (for existing settings files)
                    if (settings.ClassNames == null || settings.ClassNames.Count == 0)
                    {
                        settings.ClassNames = new System.Collections.Generic.List<string>
                        {
                            "15", "20", "25", "30", "35", "40", "45", "50",
                            "55", "60", "65", "70", "75", "80"
                        };
                        // Save updated settings with defaults
                        Save(settings);
                    }

                    return settings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new UserSettings();
        }

        public void Save(UserSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
