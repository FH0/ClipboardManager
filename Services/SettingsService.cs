using System;
using System.IO;
using System.Text.Json;

namespace ClipboardManager.Services
{
    public class AppSettings
    {
        public int RecordLimit { get; set; } = 50000;
        public bool ShowTime { get; set; } = false;
    }

    public class SettingsService
    {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        private readonly string _settingsFilePath;
        
        public AppSettings Settings { get; private set; } = new AppSettings();

        private SettingsService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipboardManager");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            _settingsFilePath = Path.Combine(appDataPath, "settings.json");
            Load();
        }

        public void Load()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    Settings = new AppSettings();
                }
            }
            else
            {
                Settings = new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
            }
        }
    }
}
