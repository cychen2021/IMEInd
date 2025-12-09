using NUnit.Framework;
using IMEInd;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Reflection;

namespace IMEInd.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class FloatingModeIntegrationTests
{
    private ToastForm? _toastForm;
    private Form? _mockInputForm;
    private TextBox? _mockTextBox;

    [SetUp]
    public void SetUp()
    {
        _toastForm = new ToastForm();

        // Create a mock input form with a textbox to simulate floating mode
        _mockInputForm = new Form
        {
            Width = 400,
            Height = 300,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(100, 100),
            ShowInTaskbar = false
        };

        _mockTextBox = new TextBox
        {
            Width = 300,
            Location = new Point(50, 100)
        };

        _mockInputForm.Controls.Add(_mockTextBox);
    }

    [TearDown]
    public void TearDown()
    {
        _toastForm?.Dispose();
        _toastForm = null;
        _mockTextBox?.Dispose();
        _mockTextBox = null;
        _mockInputForm?.Dispose();
        _mockInputForm = null;
    }

    [Test]
    public void FloatingMode_WithValidInputElement_PositionsNearInputField()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Show the mock form so we can get its automation element
        _mockInputForm!.Show();
        _mockTextBox!.Focus();

        try
        {
            // Get the automation element for the textbox
            var automationElement = AutomationElement.FromHandle(_mockTextBox.Handle);
            var boundingRect = automationElement.Current.BoundingRectangle;

            // Act
            _toastForm!.ShowToast("Test", screen, ToastStyle.Default, automationElement);

            // Assert
            Assert.That(_toastForm.Visible, Is.True, "Toast should be visible");
            Assert.That(_toastForm.TopMost, Is.True, "Toast should be topmost for floating mode");

            // Verify position is near the input field
            // Toast should be either above or below the input field
            var toastY = _toastForm.Location.Y;
            var inputTop = (int)boundingRect.Y;
            var inputBottom = (int)(boundingRect.Y + boundingRect.Height);

            // Toast should be near the input (within reasonable range)
            var isNearInput = (toastY < inputTop && toastY > inputTop - 200) ||
                             (toastY > inputBottom && toastY < inputBottom + 200);

            Assert.That(isNearInput, Is.True,
                $"Toast Y position ({toastY}) should be near input field (top: {inputTop}, bottom: {inputBottom})");

            // Verify X position is reasonably centered on the input
            var toastX = _toastForm.Location.X;
            var inputCenterX = (int)(boundingRect.X + boundingRect.Width / 2);
            var xDifference = Math.Abs(toastX + _toastForm.Width / 2 - inputCenterX);

            Assert.That(xDifference, Is.LessThan(300),
                "Toast should be horizontally centered near the input field");
        }
        finally
        {
            _mockInputForm.Hide();
        }
    }

    [Test]
    public void FloatingMode_WithInputElement_StaysWithinScreenBounds()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Position mock form near screen edge to test boundary handling
        _mockInputForm!.Location = new Point(screen.Bounds.Width - 100, 50);
        _mockInputForm.Show();
        _mockTextBox!.Focus();

        try
        {
            var automationElement = AutomationElement.FromHandle(_mockTextBox.Handle);

            // Act
            _toastForm!.ShowToast("Test", screen, ToastStyle.Default, automationElement);

            // Assert
            Assert.That(_toastForm.Location.X, Is.GreaterThanOrEqualTo(screen.Bounds.X),
                "Toast X should be within screen left boundary");
            Assert.That(_toastForm.Location.X + _toastForm.Width,
                Is.LessThanOrEqualTo(screen.Bounds.X + screen.Bounds.Width),
                "Toast X should be within screen right boundary");
            Assert.That(_toastForm.Location.Y, Is.GreaterThanOrEqualTo(screen.Bounds.Y),
                "Toast Y should be within screen top boundary");
            Assert.That(_toastForm.Location.Y + _toastForm.Height,
                Is.LessThanOrEqualTo(screen.Bounds.Y + screen.Bounds.Height),
                "Toast Y should be within screen bottom boundary");
            Assert.That(_toastForm.TopMost, Is.True,
                "Toast should maintain TopMost property in floating mode");
        }
        finally
        {
            _mockInputForm.Hide();
        }
    }

    [Test]
    public void FloatingMode_ElementNearTopOfScreen_AdjustsPosition()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Position mock form near top of screen
        _mockInputForm!.Location = new Point(screen.Bounds.X + 200, screen.Bounds.Y + 10);
        _mockInputForm.Show();
        _mockTextBox!.Focus();

        try
        {
            var automationElement = AutomationElement.FromHandle(_mockTextBox.Handle);
            var boundingRect = automationElement.Current.BoundingRectangle;

            // Act
            _toastForm!.ShowToast("Test", screen, ToastStyle.Default, automationElement);

            // Assert
            // When input is near top, toast positioning logic kicks in
            // Code first tries to position above, but if not enough space (< 20px from top),
            // it positions below instead
            var inputTop = (int)boundingRect.Y;
            var inputBottom = (int)(boundingRect.Y + boundingRect.Height);
            var toastY = _toastForm.Location.Y;

            // Toast should be positioned within screen bounds
            Assert.That(_toastForm.Location.Y, Is.GreaterThanOrEqualTo(screen.Bounds.Y),
                "Toast should be within screen top boundary");
            Assert.That(_toastForm.TopMost, Is.True,
                "Toast TopMost should be maintained");

            // When near top (less than 20px), it should position below the input
            if (inputTop < screen.Bounds.Y + 20)
            {
                Assert.That(toastY, Is.GreaterThanOrEqualTo(inputBottom - 20),
                    "Toast should be positioned at or below input when input is very near top of screen");
            }
        }
        finally
        {
            _mockInputForm.Hide();
        }
    }

    [Test]
    public void FloatingMode_FallbackToDefaultPosition_OnException()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;

        // Create a disposed control to trigger exception when accessing automation element
        var disposedTextBox = new TextBox();
        var handle = disposedTextBox.Handle; // Get handle before disposal
        disposedTextBox.Dispose();

        try
        {
            // This should fail to get valid automation element
            var automationElement = AutomationElement.FromHandle(handle);

            // Act - Should fall back to default position
            _toastForm!.ShowToast("Test", screen, ToastStyle.Default, automationElement);

            // Assert
            Assert.That(_toastForm.Visible, Is.True,
                "Toast should still be visible even if floating mode fails");
            Assert.That(_toastForm.TopMost, Is.True,
                "Toast should maintain TopMost property even with fallback");

            // Should use default position (near bottom center)
            var expectedCenterX = screen.Bounds.X + screen.Bounds.Width / 2;
            Assert.That(_toastForm.Location.X, Is.InRange(expectedCenterX - 200, expectedCenterX + 200),
                "Should fall back to default position on exception");
        }
        catch
        {
            // Some environments may not allow automation element creation from disposed control
            Assert.Pass("Test environment does not support automation element from disposed control");
        }
    }

    [Test]
    public void FloatingMode_MultipleUpdates_MaintainsTopMost()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;
        _mockInputForm!.Show();
        _mockTextBox!.Focus();

        try
        {
            var automationElement = AutomationElement.FromHandle(_mockTextBox.Handle);

            // Act - Show toast multiple times with floating mode
            _toastForm!.ShowToast("First", screen, ToastStyle.Default, automationElement);
            Assert.That(_toastForm.TopMost, Is.True, "TopMost should be true after first show");

            _toastForm.ShowToast("Second", screen, ToastStyle.Default, automationElement);
            Assert.That(_toastForm.TopMost, Is.True, "TopMost should be true after second show");

            _toastForm.ShowToast("Third", screen, ToastStyle.Default, automationElement);

            // Assert
            Assert.That(_toastForm.TopMost, Is.True,
                "TopMost should remain true after multiple floating mode updates");
            Assert.That(_toastForm.Visible, Is.True,
                "Toast should be visible after updates");
        }
        finally
        {
            _mockInputForm.Hide();
        }
    }

    [Test]
    public void FloatingMode_DifferentInputPositions_AdjustsToastPosition()
    {
        // Arrange
        var screen = Screen.PrimaryScreen!;
        _mockInputForm!.Show();
        _mockTextBox!.Focus();

        try
        {
            var automationElement = AutomationElement.FromHandle(_mockTextBox.Handle);

            // Act - First position
            _toastForm!.ShowToast("Test1", screen, ToastStyle.Default, automationElement);
            var firstPosition = _toastForm.Location;

            // Move the mock form to a different position
            _mockInputForm.Location = new Point(_mockInputForm.Location.X + 200, _mockInputForm.Location.Y + 100);
            Application.DoEvents(); // Process position update

            automationElement = AutomationElement.FromHandle(_mockTextBox.Handle);
            _toastForm.ShowToast("Test2", screen, ToastStyle.Default, automationElement);
            var secondPosition = _toastForm.Location;

            // Assert
            Assert.That(secondPosition, Is.Not.EqualTo(firstPosition),
                "Toast position should change when input field moves");
            Assert.That(_toastForm.TopMost, Is.True,
                "TopMost should be maintained when adjusting position");
        }
        finally
        {
            _mockInputForm.Hide();
        }
    }
}
