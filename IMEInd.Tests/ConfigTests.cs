using NUnit.Framework;
using IMEInd;
using System;
using System.IO;

namespace IMEInd.Tests;

[TestFixture]
public class ConfigTests
{
    private string _tempDir = null!;
    private string _originalAppData = null!;

    [SetUp]
    public void SetUp()
    {
        // Create a temporary directory for test configuration files
        _tempDir = Path.Combine(Path.GetTempPath(), "IMEInd_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Store original environment variable
        _originalAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up temporary directory
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    [Test]
    public void Config_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new Config();

        // Assert
        Assert.That(config.TimerIntervalMs, Is.EqualTo(500));
        Assert.That(config.WindowChangeThresholdSeconds, Is.EqualTo(300));
        Assert.That(config.LongTimeElapsedMinutes, Is.EqualTo(60));
        Assert.That(config.ExcludeExecutables, Is.Not.Null);
        Assert.That(config.ExcludeExecutables, Is.Empty);
        Assert.That(config.FloatingMode, Is.False);
    }

    [Test]
    public void Config_ExcludeExecutables_CanBeModified()
    {
        // Arrange
        var config = new Config();

        // Act
        config.ExcludeExecutables.Add("test.exe");
        config.ExcludeExecutables.Add("another.exe");

        // Assert
        Assert.That(config.ExcludeExecutables, Has.Count.EqualTo(2));
        Assert.That(config.ExcludeExecutables, Contains.Item("test.exe"));
        Assert.That(config.ExcludeExecutables, Contains.Item("another.exe"));
    }

    [Test]
    public void Config_Properties_CanBeSet()
    {
        // Arrange
        var config = new Config();

        // Act
        config.TimerIntervalMs = 1000;
        config.WindowChangeThresholdSeconds = 600;
        config.LongTimeElapsedMinutes = 120;
        config.FloatingMode = true;

        // Assert
        Assert.That(config.TimerIntervalMs, Is.EqualTo(1000));
        Assert.That(config.WindowChangeThresholdSeconds, Is.EqualTo(600));
        Assert.That(config.LongTimeElapsedMinutes, Is.EqualTo(120));
        Assert.That(config.FloatingMode, Is.True);
    }

    [Test]
    public void GetConfigPath_ReturnsPathInAppData()
    {
        // Act
        var path = Config.GetConfigPath();

        // Assert
        Assert.That(path, Is.Not.Null.And.Not.Empty);
        Assert.That(path, Does.EndWith("config.toml"));
        Assert.That(path, Does.Contain("IMEInd"));
    }

    [Test]
    public void GetConfigPath_ContainsApplicationData()
    {
        // Act
        var path = Config.GetConfigPath();
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Assert
        Assert.That(path, Does.StartWith(appDataPath));
    }
}
