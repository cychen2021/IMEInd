using System;
using System.IO;
using System.Collections.Generic;
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
    /// A list of executable names whose windows should be ignored (no IME toast shown).
    /// Case-insensitive. You can specify with or without the .exe suffix.
    /// Example: ["msedgewebview2.exe", "MyApp", "Code.exe"]
    /// </summary>
    public List<string> ExcludeExecutables { get; set; } = new();

    /// <summary>
    /// When true, the indicator will be positioned around the input field instead of at a fixed screen position.
    /// Default: false
    /// </summary>
    public bool FloatingMode { get; set; } = false;

    /// <summary>
    /// Load configuration from the platform-dependent location.
    /// On Windows: %APPDATA%\IMEInd\config.toml
    /// </summary>
    public static Config Load()
    {
        var config = new Config();
        var configPath = GetConfigPath();

        // Always log the configuration path we will use/load
        if (App.LogLevel >= 2)
        {
            App.log($"Configuration file path: {configPath}");
        }

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

                // Optional array for excluded executables
                if (tomlTable.TryGetValue("ExcludeExecutables", out var excludeListRaw) && excludeListRaw is TomlArray arr)
                {
                    foreach (var item in arr)
                    {
                        if (item is string s && !string.IsNullOrWhiteSpace(s))
                        {
                            config.ExcludeExecutables.Add(s.Trim());
                        }
                    }
                }

                if (tomlTable.TryGetValue("FloatingMode", out var floatingMode))
                {
                    config.FloatingMode = Convert.ToBoolean(floatingMode);
                }

                if (App.LogLevel >= 2)
                {
                    App.log($"Configuration loaded from: {configPath}");
                    if (config.ExcludeExecutables.Count > 0 && App.LogLevel >= 3)
                    {
                        App.log($"Excluded executables: {string.Join(", ", config.ExcludeExecutables)}");
                    }
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

# Executable names whose windows should be ignored (no toast displayed)
# Case-insensitive; specify with or without .exe
# Example: [""msedgewebview2.exe"", ""MyEmbeddedHost"", ""SomeApp.exe""]
ExcludeExecutables = [""MuMuNxDevice.exe"", ""MuMuNxMain.exe"", ""zotero.exe""]

# When true, the indicator will be positioned around the input field instead of at a fixed screen position
# Default: false
FloatingMode = false
";
            File.WriteAllText(configPath, defaultConfig);

            if (App.LogLevel >= 2)
            {
                App.log($"Default configuration file created at: {configPath}");
            }
        }
    }
}
