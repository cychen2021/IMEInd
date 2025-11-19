using System;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace IMEInd;

/// <summary>
/// Configuration settings for IME Indicator.
/// </summary>
public class Config
{
    /// <summary>
    /// Timer interval in milliseconds for checking IME state changes.
    /// Default: 500
    /// </summary>
    public int TimerIntervalMs { get; set; } = 500;

    /// <summary>
    /// Threshold in seconds before showing the toast again when window changes.
    /// Default: 300 (5 minutes)
    /// </summary>
    public int WindowChangeThresholdSeconds { get; set; } = 300;

    /// <summary>
    /// Threshold in minutes before showing the toast again due to long idle time.
    /// Default: 60 (1 hour)
    /// </summary>
    public int LongTimeElapsedMinutes { get; set; } = 60;

    /// <summary>
    /// Load configuration from the platform-dependent location.
    /// On Windows: %APPDATA%\IMEInd\config.toml
    /// </summary>
    public static Config Load()
    {
        var config = new Config();
        var configPath = GetConfigPath();

        if (File.Exists(configPath))
        {
            try
            {
                var tomlContent = File.ReadAllText(configPath);
                var tomlTable = Toml.ToModel(tomlContent);

                if (tomlTable.TryGetValue("TimerIntervalMs", out var timerInterval))
                {
                    config.TimerIntervalMs = Convert.ToInt32(timerInterval);
                }

                if (tomlTable.TryGetValue("WindowChangeThresholdSeconds", out var windowThreshold))
                {
                    config.WindowChangeThresholdSeconds = Convert.ToInt32(windowThreshold);
                }

                if (tomlTable.TryGetValue("LongTimeElapsedMinutes", out var longTimeThreshold))
                {
                    config.LongTimeElapsedMinutes = Convert.ToInt32(longTimeThreshold);
                }

                if (App.LogLevel >= 2)
                {
                    App.log($"Configuration loaded from: {configPath}");
                }
            }
            catch (Exception ex)
            {
                if (App.LogLevel >= 1)
                {
                    App.log($"ERROR: Failed to load configuration from {configPath}: {ex.Message}. Using defaults.");
                }
            }
        }
        else
        {
            if (App.LogLevel >= 2)
            {
                App.log($"Configuration file not found at {configPath}. Using defaults.");
            }
        }

        return config;
    }

    /// <summary>
    /// Get the platform-dependent configuration file path.
    /// </summary>
    public static string GetConfigPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "IMEInd");
        return Path.Combine(configDir, "config.toml");
    }

    /// <summary>
    /// Create a default configuration file with documentation.
    /// </summary>
    public static void CreateDefault()
    {
        var configPath = GetConfigPath();
        var configDir = Path.GetDirectoryName(configPath)!;

        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        if (!File.Exists(configPath))
        {
            var defaultConfig = @"# IME Indicator Configuration File
# This file is in TOML format (https://toml.io/)

# Timer interval in milliseconds for checking IME state changes
# Lower values provide more responsive detection but use more CPU
# Default: 500
TimerIntervalMs = 500

# Threshold in seconds before showing the toast again when window changes
# This prevents the toast from appearing too frequently when switching windows
# Default: 300 (5 minutes)
WindowChangeThresholdSeconds = 300

# Threshold in minutes before showing the toast again due to long idle time
# This ensures the toast appears periodically even if you stay in the same window
# Default: 60 (1 hour)
LongTimeElapsedMinutes = 60
";
            File.WriteAllText(configPath, defaultConfig);

            if (App.LogLevel >= 2)
            {
                App.log($"Default configuration file created at: {configPath}");
            }
        }
    }
}
