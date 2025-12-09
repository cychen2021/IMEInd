using NUnit.Framework;
using IMEInd;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Reflection;

namespace IMEInd.Tests;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class AppFloatingModeTests
{
    private Form? _mockInputForm;
    private TextBox? _mockTextBox;

    [SetUp]
    public void SetUp()
    {
        // Create a mock input form with a textbox
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
        _mockTextBox?.Dispose();
        _mockTextBox = null;
        _mockInputForm?.Dispose();
        _mockInputForm = null;
    }

    [Test]
    public void App_Show_WithFloatingModeTrue_PassesInputElement()
    {
        // Arrange
        var app = new App();
        var toastForm = new ToastForm();
        var ime = new App.IME(0x0409); // English
        var screen = Screen.PrimaryScreen!;

        // Create a mock config with floating mode enabled
        var config = new Config { FloatingMode = true };

        // Use reflection to set the private _config field
        var configField = typeof(App).GetField("_config", BindingFlags.NonPublic | BindingFlags.Static);
        configField!.SetValue(null, config);

        _mockInputForm!.Show();
        _mockTextBox!.Focus();

        try
        {
            // Act
            app.Show(toastForm, ime, screen);

            // Assert
            Assert.That(toastForm.Visible, Is.True, "Toast should be visible");
            Assert.That(toastForm.TopMost, Is.True, "Toast should maintain TopMost when floating mode is enabled");

            // The position should be affected by floating mode when focused element is editable
            // We can't directly verify the inputElement was passed, but we can verify TopMost is maintained
        }
        finally
        {
            _mockInputForm.Hide();
            toastForm.Dispose();
            configField!.SetValue(null, null); // Reset
        }
    }

    [Test]
    public void App_Show_WithFloatingModeFalse_UsesDefaultPosition()
    {
        // Arrange
        var app = new App();
        var toastForm = new ToastForm();
        var ime = new App.IME(0x0409); // English
        var screen = Screen.PrimaryScreen!;

        // Create a mock config with floating mode disabled
        var config = new Config { FloatingMode = false };

        // Use reflection to set the private _config field
        var configField = typeof(App).GetField("_config", BindingFlags.NonPublic | BindingFlags.Static);
        configField!.SetValue(null, config);

        _mockInputForm!.Show();
        _mockTextBox!.Focus();

        try
        {
            // Act
            app.Show(toastForm, ime, screen);

            // Assert
            Assert.That(toastForm.Visible, Is.True, "Toast should be visible");
            Assert.That(toastForm.TopMost, Is.True, "Toast should maintain TopMost even with floating mode disabled");

            // With floating mode disabled, position should be default (near bottom center)
            var expectedCenterX = screen.Bounds.X + screen.Bounds.Width / 2;
            Assert.That(toastForm.Location.X, Is.InRange(expectedCenterX - 200, expectedCenterX + 200),
                "Should use default position when floating mode is disabled");
        }
        finally
        {
            _mockInputForm.Hide();
            toastForm.Dispose();
            configField!.SetValue(null, null); // Reset
        }
    }

    [Test]
    public void App_Show_WithUnavailableIME_UsesStrikeThrough()
    {
        // Arrange
        var app = new App();
        var toastForm = new ToastForm();
        var ime = new App.IME(0x0407); // German - unsupported
        var screen = Screen.PrimaryScreen!;

        // Use reflection to set config
        var configField = typeof(App).GetField("_config", BindingFlags.NonPublic | BindingFlags.Static);
        configField!.SetValue(null, new Config { FloatingMode = false });

        try
        {
            // Act
            app.Show(toastForm, ime, screen);

            // Assert
            Assert.That(toastForm.Visible, Is.True, "Toast should be visible");
            Assert.That(toastForm.TopMost, Is.True, "Toast should maintain TopMost");

            // Find the label and verify strikethrough
            var label = FindLabelControl(toastForm);
            Assert.That(label, Is.Not.Null);
            Assert.That(label!.Text, Is.EqualTo("Unavailable"), "Should show 'Unavailable' for unsupported IME");
            Assert.That(label.Font.Strikeout, Is.True, "Should use strikethrough for unavailable IME");
        }
        finally
        {
            toastForm.Dispose();
            configField!.SetValue(null, null); // Reset
        }
    }

    [Test]
    public void App_Show_WithSupportedIME_UsesDefaultStyle()
    {
        // Arrange
        var app = new App();
        var toastForm = new ToastForm();
        var ime = new App.IME(0x0804); // Simplified Chinese - supported
        var screen = Screen.PrimaryScreen!;

        // Use reflection to set config
        var configField = typeof(App).GetField("_config", BindingFlags.NonPublic | BindingFlags.Static);
        configField!.SetValue(null, new Config { FloatingMode = false });

        try
        {
            // Act
            app.Show(toastForm, ime, screen);

            // Assert
            Assert.That(toastForm.Visible, Is.True, "Toast should be visible");
            Assert.That(toastForm.TopMost, Is.True, "Toast should maintain TopMost");

            // Find the label and verify default style
            var label = FindLabelControl(toastForm);
            Assert.That(label, Is.Not.Null);
            Assert.That(label!.Text, Is.EqualTo("简体中文"), "Should show IME name for supported IME");
            Assert.That(label.Font.Strikeout, Is.False, "Should not use strikethrough for supported IME");
        }
        finally
        {
            toastForm.Dispose();
            configField!.SetValue(null, null); // Reset
        }
    }

    [Test]
    public void App_IsEditable_ReturnsTrueForEditControl()
    {
        // Arrange
        _mockInputForm!.Show();
        _mockTextBox!.Focus();

        try
        {
            var automationElement = AutomationElement.FromHandle(_mockTextBox.Handle);

            // Act
            var isEditable = App.IsEditable(automationElement);

            // Assert
            Assert.That(isEditable, Is.True, "TextBox should be recognized as editable");
        }
        finally
        {
            _mockInputForm.Hide();
        }
    }

    [Test]
    public void App_IsEditable_ReturnsFalseForNonEditControl()
    {
        // Arrange
        var label = new Label { Text = "Test" };
        _mockInputForm!.Controls.Add(label);
        _mockInputForm.Show();
        label.Focus();

        try
        {
            var automationElement = AutomationElement.FromHandle(label.Handle);

            // Act
            var isEditable = App.IsEditable(automationElement);

            // Assert
            Assert.That(isEditable, Is.False, "Label should not be recognized as editable");
        }
        finally
        {
            _mockInputForm.Hide();
            _mockInputForm.Controls.Remove(label);
            label.Dispose();
        }
    }

    [Test]
    public void App_IsEditable_ReturnsFalseForNullElement()
    {
        // Act
        var isEditable = App.IsEditable(null!);

        // Assert
        Assert.That(isEditable, Is.False, "Null element should return false");
    }

    private Label? FindLabelControl(ToastForm form)
    {
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
}
