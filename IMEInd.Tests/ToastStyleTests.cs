using NUnit.Framework;
using IMEInd;

namespace IMEInd.Tests;

[TestFixture]
public class ToastStyleTests
{
    [Test]
    public void ToastStyle_Default_HasCorrectValue()
    {
        // Assert
        Assert.That((int)ToastStyle.Default, Is.EqualTo(0));
    }

    [Test]
    public void ToastStyle_StrikeThrough_HasCorrectValue()
    {
        // Assert
        Assert.That((int)ToastStyle.StrikeThrough, Is.EqualTo(1));
    }

    [Test]
    public void ToastStyle_EnumValues_AreDistinct()
    {
        // Assert
        Assert.That(ToastStyle.Default, Is.Not.EqualTo(ToastStyle.StrikeThrough));
    }

    [Test]
    public void ToastStyle_CanBeCastFromInt()
    {
        // Arrange & Act
        var defaultStyle = (ToastStyle)0;
        var strikeThroughStyle = (ToastStyle)1;

        // Assert
        Assert.That(defaultStyle, Is.EqualTo(ToastStyle.Default));
        Assert.That(strikeThroughStyle, Is.EqualTo(ToastStyle.StrikeThrough));
    }

    [Test]
    public void ToastStyle_CanBeCastToInt()
    {
        // Arrange & Act
        int defaultValue = (int)ToastStyle.Default;
        int strikeThroughValue = (int)ToastStyle.StrikeThrough;

        // Assert
        Assert.That(defaultValue, Is.EqualTo(0));
        Assert.That(strikeThroughValue, Is.EqualTo(1));
    }
}
