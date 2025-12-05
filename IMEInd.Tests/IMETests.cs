using NUnit.Framework;
using IMEInd;

namespace IMEInd.Tests;

[TestFixture]
public class IMETests
{
    [Test]
    public void IME_English_HasCorrectProperties()
    {
        // Arrange & Act
        var ime = new App.IME(0x0409);

        // Assert
        Assert.That(ime.LangID, Is.EqualTo(0x0409));
        Assert.That(ime.Name, Is.EqualTo("English"));
        Assert.That(ime.IsSupportedIME, Is.True);
    }

    [Test]
    public void IME_SimplifiedChinese_HasCorrectProperties()
    {
        // Arrange & Act
        var ime = new App.IME(0x0804);

        // Assert
        Assert.That(ime.LangID, Is.EqualTo(0x0804));
        Assert.That(ime.Name, Is.EqualTo("简体中文"));
        Assert.That(ime.IsSupportedIME, Is.True);
    }

    [Test]
    public void IME_TraditionalChinese_HasCorrectProperties()
    {
        // Arrange & Act
        var ime = new App.IME(0x0404);

        // Assert
        Assert.That(ime.LangID, Is.EqualTo(0x0404));
        Assert.That(ime.Name, Is.EqualTo("繁體中文"));
        Assert.That(ime.IsSupportedIME, Is.True);
    }

    [Test]
    public void IME_Japanese_HasCorrectProperties()
    {
        // Arrange & Act
        var ime = new App.IME(0x0411);

        // Assert
        Assert.That(ime.LangID, Is.EqualTo(0x0411));
        Assert.That(ime.Name, Is.EqualTo("日本語"));
        Assert.That(ime.IsSupportedIME, Is.True);
    }

    [Test]
    public void IME_Korean_HasCorrectProperties()
    {
        // Arrange & Act
        var ime = new App.IME(0x0412);

        // Assert
        Assert.That(ime.LangID, Is.EqualTo(0x0412));
        Assert.That(ime.Name, Is.EqualTo("한국어"));
        Assert.That(ime.IsSupportedIME, Is.True);
    }

    [Test]
    public void IME_UnsupportedLanguage_ReturnsHexName()
    {
        // Arrange & Act
        var ime = new App.IME(0x0407); // German

        // Assert
        Assert.That(ime.LangID, Is.EqualTo(0x0407));
        Assert.That(ime.Name, Is.EqualTo("0x0407"));
        Assert.That(ime.IsSupportedIME, Is.False);
    }

    [Test]
    public void IME_AnotherUnsupportedLanguage_ReturnsHexName()
    {
        // Arrange & Act
        var ime = new App.IME(0x040C); // French

        // Assert
        Assert.That(ime.LangID, Is.EqualTo(0x040C));
        Assert.That(ime.Name, Is.EqualTo("0x040C"));
        Assert.That(ime.IsSupportedIME, Is.False);
    }

    [Test]
    public void IME_ZeroLangID_IsUnsupported()
    {
        // Arrange & Act
        var ime = new App.IME(0);

        // Assert
        Assert.That(ime.LangID, Is.EqualTo(0));
        Assert.That(ime.Name, Is.EqualTo("0x0000"));
        Assert.That(ime.IsSupportedIME, Is.False);
    }

    [Test]
    public void IME_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var ime1 = new App.IME(0x0409);
        var ime2 = new App.IME(0x0409);
        var ime3 = new App.IME(0x0804);

        // Assert
        Assert.That(ime1, Is.EqualTo(ime2));
        Assert.That(ime1, Is.Not.EqualTo(ime3));
        Assert.That(ime1 == ime2, Is.True);
        Assert.That(ime1 != ime3, Is.True);
    }

    [Test]
    public void IME_RecordHashCode_IsConsistent()
    {
        // Arrange
        var ime1 = new App.IME(0x0409);
        var ime2 = new App.IME(0x0409);

        // Assert
        Assert.That(ime1.GetHashCode(), Is.EqualTo(ime2.GetHashCode()));
    }

    [TestCase(0x0409, "English", true)]
    [TestCase(0x0804, "简体中文", true)]
    [TestCase(0x0404, "繁體中文", true)]
    [TestCase(0x0411, "日本語", true)]
    [TestCase(0x0412, "한국어", true)]
    public void IME_SupportedLanguages_AreRecognized(int langId, string expectedName, bool expectedSupported)
    {
        // Arrange & Act
        var ime = new App.IME(langId);

        // Assert
        Assert.That(ime.Name, Is.EqualTo(expectedName));
        Assert.That(ime.IsSupportedIME, Is.EqualTo(expectedSupported));
    }

    [TestCase(0x0407)] // German
    [TestCase(0x040C)] // French
    [TestCase(0x0410)] // Italian
    [TestCase(0x0419)] // Russian
    [TestCase(0x0816)] // Portuguese (Portugal)
    public void IME_UnsupportedLanguages_AreNotRecognized(int langId)
    {
        // Arrange & Act
        var ime = new App.IME(langId);

        // Assert
        Assert.That(ime.IsSupportedIME, Is.False);
        Assert.That(ime.Name, Does.StartWith("0x"));
    }
}
