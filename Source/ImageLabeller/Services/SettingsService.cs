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
                    return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
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
