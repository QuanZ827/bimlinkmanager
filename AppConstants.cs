using System;
using System.IO;

namespace BimLinkManager
{
    internal static class AppConstants
    {
        // Display identity
        public const string ProductName = "BIM Link Manager";
        public const string ProductCode = "BimLinkManager"; // identifier-safe (folders, logs)
        public const string Publisher = "Zhequan Zhang";
        public const string BrandSubtitle = "BY ZHEQUAN ZHANG";
        public const string RibbonTabName = "BIM Link Manager";
        public const string RibbonPanelName = "Cloud Links";
        public const string Version = "1.0.0";

        public const string BatchLinkButtonName = "BatchLinkButton";
        public const string BatchLinkButtonText = "Batch\nLink";
        public const string BatchLinkTooltip = "Batch link cloud models from ACC into the current project.";

        public const string ApsBaseUrl = "https://developer.api.autodesk.com";
        public const string DefaultCloudRegion = "US";

        public const int HttpTimeoutSeconds = 30;
        public const int MaxFolderChildren = 1000;

        public static string AppDataFolder
        {
            get
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(root, ProductCode);
            }
        }

        public static string ConfigFilePath
        {
            get { return Path.Combine(AppDataFolder, "config.json"); }
        }

        public static string LogFilePath
        {
            get { return Path.Combine(AppDataFolder, "bimlinkmanager.log"); }
        }

        public static void EnsureAppDataFolder()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }
            }
            catch
            {
            }
        }
    }
}