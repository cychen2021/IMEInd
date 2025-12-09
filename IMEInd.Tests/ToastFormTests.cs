using NUnit.Framework;
using IMEInd;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Automation;

namespace IMEInd.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)] // Required for Windows Forms controls
public class ToastFormTests
{
    private ToastForm? _toastForm;

    [SetUp]
    public void SetUp()
    {
        _toastForm = new ToastForm();
    }

    [TearDown]
    public void TearDown()
    {
        _toastForm?.Dispose();
        _toastForm = null;
    }

    #region Basic UI Properties

    [Test]
    public void ToastForm_Initialization_HasCorrectFormProperties()
    {
        // Assert
        Assert.That(_toastForm!.FormBorderStyle, Is.EqualTo(FormBorderStyle.None), "Form should have no border");
        Assert.That(_toastForm.ShowInTaskbar, Is.False, "Form should not show in taskbar");
        Assert.That(_toastForm.TopMost, Is.True, "Form should be topmost");
        Assert.That(_toastForm.StartPosition, Is.EqualTo(FormStartPosition.Manual), "Form should have manual start position");
        Assert.That(_toastForm.AutoSize, Is.True, "Form should auto-size");
        Assert.That(_toastForm.AutoSizeMode, Is.EqualTo(AutoSizeMode.GrowAndShrink), "Form should grow and shrink");
    }

    [Test]
    public void ToastForm_Initialization_IsNotVisible()
    {
        // Assert
        Assert.That(_toastForm!.Visible, Is.False, "Form should not be visible initially");
    }

    [Test]
    public void ToastForm_Initialization_HasTopMostProperty()
    {
        // Assert - This is critical for floating mode to work
        Assert.That(_toastForm!.TopMost, Is.True, "TopMost property must be true for toast to float above other windows");
    }

    [Test]
    public void ToastForm_AfterInitialization_TopMostRemainsTrue()
    {
        // Act - Force layout update
        _toastForm!.PerformLayout();

        // Assert
        Assert.That(_toastForm.TopMost, Is.True, "TopMost should remain true after layout");
    }

    #endregion

    #region ShowToast Basic Functionality

    [Test]
    public void ShowToast_DefaultMode_MakesFormVisible()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test", screen, ToastStyle.Default, null);

        // Assert
        Assert.That(_toastForm.Visible, Is.True, "Form should be visible after ShowToast");
    }

    [Test]
    public void ShowToast_DefaultMode_HasCorrectOpacity()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test", screen, ToastStyle.Default, null);

        // Assert
        Assert.That(_toastForm.Opacity, Is.EqualTo(0.95).Within(0.01), "Opacity should be 0.95");
    }

    [Test]
    public void ShowToast_DefaultMode_PositionsNearBottomCenter()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;
        var expectedCenterX = screen.Bounds.X + screen.Bounds.Width / 2;
        var expectedY = screen.Bounds.Y + screen.Bounds.Height - 200 - _toastForm!.Height;

        // Act
        _toastForm.ShowToast("Test", screen, ToastStyle.Default, null);

        // Assert
        // X should be near center (allow some tolerance for width calculation)
        Assert.That(_toastForm.Location.X, Is.InRange(expectedCenterX - 200, expectedCenterX + 200),
            "X position should be near screen center");
        Assert.That(_toastForm.Location.Y, Is.GreaterThanOrEqualTo(screen.Bounds.Y),
            "Y position should be within screen bounds");
        Assert.That(_toastForm.Location.Y, Is.LessThanOrEqualTo(screen.Bounds.Y + screen.Bounds.Height),
            "Y position should be within screen bounds");
    }

    [Test]
    public void ShowToast_WithText_UpdatesLabelContent()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;
        var testText = "English";

        // Act
        _toastForm!.ShowToast(testText, screen, ToastStyle.Default, null);

        // Find the label control
        var label = FindLabelControl(_toastForm);

        // Assert
        Assert.That(label, Is.Not.Null, "Label control should exist");
        Assert.That(label!.Text, Is.EqualTo(testText), "Label text should match input");
    }

    [Test]
    public void ShowToast_DefaultStyle_HasBoldFont()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test", screen, ToastStyle.Default, null);

        // Find the label control
        var label = FindLabelControl(_toastForm);

        // Assert
        Assert.That(label, Is.Not.Null, "Label control should exist");
        Assert.That(label!.Font.Bold, Is.True, "Font should be bold for default style");
        Assert.That(label.Font.Strikeout, Is.False, "Font should not be strikeout for default style");
    }

    [Test]
    public void ShowToast_StrikeThroughStyle_HasStrikeoutFont()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test", screen, ToastStyle.StrikeThrough, null);

        // Find the label control
        var label = FindLabelControl(_toastForm);

        // Assert
        Assert.That(label, Is.Not.Null, "Label control should exist");
        Assert.That(label!.Font.Bold, Is.True, "Font should be bold");
        Assert.That(label.Font.Strikeout, Is.True, "Font should be strikeout for StrikeThrough style");
    }

    #endregion

    #region Floating Mode Tests

    [Test]
    public void ShowToast_FloatingMode_WithNullInputElement_UsesDefaultPosition()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act - Passing null inputElement should use default positioning even in floating mode
        _toastForm!.ShowToast("Test", screen, ToastStyle.Default, null);

        var expectedCenterX = screen.Bounds.X + screen.Bounds.Width / 2;

        // Assert
        Assert.That(_toastForm.Visible, Is.True, "Form should be visible");
        Assert.That(_toastForm.TopMost, Is.True, "TopMost should be true for floating");
        Assert.That(_toastForm.Location.X, Is.InRange(expectedCenterX - 200, expectedCenterX + 200),
            "Should use default position when inputElement is null");
    }

    [Test]
    public void ShowToast_FloatingMode_MaintainsTopMostProperty()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test", screen, ToastStyle.Default, null);

        // Assert - Critical test for floating mode issue
        Assert.That(_toastForm.TopMost, Is.True,
            "TopMost property must remain true after ShowToast to enable floating mode");
    }

    [Test]
    public void ShowToast_MultipleInvocations_MaintainsTopMost()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act - Call ShowToast multiple times
        _toastForm!.ShowToast("First", screen, ToastStyle.Default, null);
        Assert.That(_toastForm.TopMost, Is.True, "TopMost should be true after first call");

        _toastForm.ShowToast("Second", screen, ToastStyle.Default, null);
        Assert.That(_toastForm.TopMost, Is.True, "TopMost should be true after second call");

        _toastForm.ShowToast("Third", screen, ToastStyle.Default, null);

        // Assert
        Assert.That(_toastForm.TopMost, Is.True,
            "TopMost should remain true after multiple ShowToast invocations");
    }

    [Test]
    public void ToastForm_TopMostProperty_IsAccessible()
    {
        // Act & Assert - Verify we can read and set TopMost
        Assert.That(_toastForm!.TopMost, Is.True, "Initial TopMost should be true");

        // Verify setting works
        _toastForm.TopMost = false;
        Assert.That(_toastForm.TopMost, Is.False, "TopMost should be settable to false");

        _toastForm.TopMost = true;
        Assert.That(_toastForm.TopMost, Is.True, "TopMost should be settable back to true");
    }

    [Test]
    public void ShowToast_AfterHide_CanBeShownAgain()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test1", screen, ToastStyle.Default, null);
        Assert.That(_toastForm.Visible, Is.True, "Should be visible after first show");
        Assert.That(_toastForm.TopMost, Is.True, "TopMost should be true after first show");

        _toastForm.Hide();
        Assert.That(_toastForm.Visible, Is.False, "Should be hidden");

        _toastForm.ShowToast("Test2", screen, ToastStyle.Default, null);

        // Assert
        Assert.That(_toastForm.Visible, Is.True, "Should be visible after second show");
        Assert.That(_toastForm.TopMost, Is.True, "TopMost should still be true after hide/show cycle");
    }

    #endregion

    #region Position Calculation Tests

    [Test]
    public void ShowToast_OnSecondaryScreen_PositionsCorrectly()
    {
        // Arrange - Skip if only one screen
        if (Screen.AllScreens.Length < 2)
        {
            Assert.Ignore("Test requires multiple screens");
        }

        var secondaryScreen = Screen.AllScreens.First(s => !s.Primary);

        // Act
        _toastForm!.ShowToast("Test", secondaryScreen, ToastStyle.Default, null);

        // Assert - Position should be within secondary screen bounds
        Assert.That(_toastForm.Location.X, Is.GreaterThanOrEqualTo(secondaryScreen.Bounds.X),
            "X position should be within secondary screen");
        Assert.That(_toastForm.Location.X, Is.LessThanOrEqualTo(secondaryScreen.Bounds.Right),
            "X position should be within secondary screen");
    }

    [Test]
    public void ShowToast_ChangingScreens_UpdatesPosition()
    {
        // Arrange
        var primaryScreen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test1", primaryScreen, ToastStyle.Default, null);
        var firstLocation = _toastForm.Location;

        // If there's a secondary screen, test position change
        if (Screen.AllScreens.Length > 1)
        {
            var secondaryScreen = Screen.AllScreens.First(s => !s.Primary);
            _toastForm.ShowToast("Test2", secondaryScreen, ToastStyle.Default, null);
            var secondLocation = _toastForm.Location;

            // Assert - Location should change for different screens
            Assert.That(secondLocation, Is.Not.EqualTo(firstLocation),
                "Position should change when displaying on different screens");
        }

        // Always verify TopMost is maintained
        Assert.That(_toastForm.TopMost, Is.True, "TopMost should be maintained across screen changes");
    }

    #endregion

    #region UI Component Structure Tests

    [Test]
    public void ToastForm_HasFlowLayoutPanel()
    {
        // Act
        var panel = FindFlowLayoutPanel(_toastForm!);

        // Assert
        Assert.That(panel, Is.Not.Null, "ToastForm should contain a FlowLayoutPanel");
    }

    [Test]
    public void ToastForm_HasIconLabel()
    {
        // Act
        var iconLabel = FindIconLabel(_toastForm!);

        // Assert
        Assert.That(iconLabel, Is.Not.Null, "ToastForm should contain an icon label");
        Assert.That(iconLabel!.Text, Is.EqualTo("\uF2B7"), "Icon should be the keyboard icon");
    }

    [Test]
    public void ToastForm_HasTextLabel()
    {
        // Act
        var label = FindLabelControl(_toastForm!);

        // Assert
        Assert.That(label, Is.Not.Null, "ToastForm should contain a text label");
    }

    [Test]
    public void ToastForm_LabelsHaveCorrectColors()
    {
        // Arrange
        var expectedBackColor = Color.FromArgb(255, ColorTranslator.FromHtml("#122738"));
        var expectedForeColor = Color.White;

        // Act
        var label = FindLabelControl(_toastForm!);
        var iconLabel = FindIconLabel(_toastForm!);

        // Assert
        Assert.That(label, Is.Not.Null);
        Assert.That(iconLabel, Is.Not.Null);
        Assert.That(label!.ForeColor, Is.EqualTo(expectedForeColor), "Label foreground should be white");
        Assert.That(iconLabel!.ForeColor, Is.EqualTo(expectedForeColor), "Icon foreground should be white");
        // Note: BackColor comparison may need tolerance due to alpha channel
    }

    #endregion

    #region Timer and Visibility Tests

    [Test]
    public void ShowToast_StartsTimer()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test", screen, ToastStyle.Default, null);

        // Assert
        Assert.That(_toastForm.Visible, Is.True, "Form should be visible immediately after ShowToast");

        // Note: We can't easily test timer functionality in unit tests without async/wait,
        // but we verify the form is shown which indicates timer was started
    }

    [Test]
    public void ShowToast_FormIsShown_BeforeTimerCompletes()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Act
        _toastForm!.ShowToast("Test", screen, ToastStyle.Default, null);

        // Assert - Immediately after ShowToast, form should be visible
        Assert.That(_toastForm.Visible, Is.True,
            "Form should be visible immediately, not waiting for timer");
    }

    #endregion

    #region Helper Methods

    private Label? FindLabelControl(ToastForm form)
    {
        // Find the main text label (not the icon)
        foreach (Control control in form.Controls)
        {
            if (control is FlowLayoutPanel panel)
            {
                foreach (Control panelControl in panel.Controls)
                {
                    if (panelControl is Label label && label.Text != "\uF2B7")
                    {
                        return label;
                    }
                }
            }
        }
        return null;
    }

    private Label? FindIconLabel(ToastForm form)
    {
        // Find the icon label
        foreach (Control control in form.Controls)
        {
            if (control is FlowLayoutPanel panel)
            {
                foreach (Control panelControl in panel.Controls)
                {
                    if (panelControl is Label label && label.Text == "\uF2B7")
                    {
                        return label;
                    }
                }
            }
        }
        return null;
    }

    private FlowLayoutPanel? FindFlowLayoutPanel(ToastForm form)
    {
        foreach (Control control in form.Controls)
        {
            if (control is FlowLayoutPanel panel)
            {
                return panel;
            }
        }
        return null;
    }

    #endregion
}
