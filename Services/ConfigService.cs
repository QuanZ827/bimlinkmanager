using System;
using System.IO;
using System.Text.Json;
using BimLinkManager.Diagnostics;
using BimLinkManager.Models;

namespace BimLinkManager.Services
{
    public class UserConfig
    {
        public double WindowWidth { get; set; } = 1280;
        public double WindowHeight { get; set; } = 800;
        public PositioningSystem LastPositioning { get; set; } = PositioningSystem.AutoOriginToInternalOrigin;
        public LinkReferenceType LastReferenceType { get; set; } = LinkReferenceType.Overlay;
        public string LastWorksetName { get; set; }
    }

    public static class ConfigService
    {
        private static readonly object _gate = new object();
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static UserConfig Load()
        {
            try
            {
                var path = AppConstants.ConfigFilePath;
                if (!File.Exists(path)) return new UserConfig();
                var text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) return new UserConfig();
                return JsonSerializer.Deserialize<UserConfig>(text, _options) ?? new UserConfig();
            }
            catch (Exception ex)
            {
                Log.Warn("ConfigService.Load failed: " + ex.Message);
                return new UserConfig();
            }
        }

        public static void Save(UserConfig config)
        {
            if (config == null) return;
            try
            {
                AppConstants.EnsureAppDataFolder();
                var json = JsonSerializer.Serialize(config, _options);
                lock (_gate)
                {
                    File.WriteAllText(AppConstants.ConfigFilePath, json);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("ConfigService.Save failed: " + ex.Message);
            }
        }
    }
}
