// Program.cs - IME Indicator for Windows
// Monitors UI Automation focus changes and displays IME status toast overlay
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Automation;

namespace IMEInd;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var indicator = new ToastForm();

        Automation.AddAutomationFocusChangedEventHandler(OnFocusChanged);
        SystemEventsWrapper.OnInputLangChange += () =>
        {
            indicator.ShowToast("IME: " + InputMethodName.GetCurrent());
        };

        Application.Run(new ApplicationContext()); // trayless background
        Automation.RemoveAllEventHandlers();

        void OnFocusChanged(object? sender, AutomationFocusChangedEventArgs e)
        {
            try
            {
                var el = AutomationElement.FocusedElement;
                if (el is null) return;

                var ct = el.Current.ControlType;
                bool editable = ct == ControlType.Edit || ct == ControlType.Document
                                || el.TryGetCurrentPattern(ValuePattern.Pattern, out _)
                                || el.TryGetCurrentPattern(TextPattern.Pattern, out _);

                if (!editable) return;

                indicator.ShowToast("IME: " + InputMethodName.GetCurrent(), GetScreenPoint(el));
            }
            catch { /* swallow */ }
        }
    }

    static Point GetScreenPoint(AutomationElement el)
    {
        // 优先取文本插入符附近矩形（若支持 TextPattern）
        if (el.TryGetCurrentPattern(TextPattern.Pattern, out var pat) && pat is TextPattern tp)
        {
            try
            {
                var range = tp.DocumentRange;
                var rects = range?.GetBoundingRectangles();
                if (rects != null && rects.Length > 0)
                {
                    var rect = rects[0];
                    return new Point((int)rect.Left, Math.Max(0, (int)rect.Top - 30));
                }
            }
            catch { }
        }
        // 退化：用元素矩形上方
        try
        {
            var b = el.Current.BoundingRectangle;
            return new Point((int)(b.Left + b.Width / 2), Math.Max(0, (int)b.Top - 30));
        }
        catch { }
        // 兜底：屏幕顶部中间
        return new Point(Screen.PrimaryScreen!.Bounds.Width / 2, 20);
    }
}

public sealed class ToastForm : Form
{
    readonly Label _label;
    readonly System.Windows.Forms.Timer _timer;
    public ToastForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(34, 34, 34);
        _label = new Label
        {
            ForeColor = Color.White,
            AutoSize = false,
            Width = 300,
            Height = 30,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        Controls.Add(_label);
        Size = _label.Size;
        _timer = new System.Windows.Forms.Timer { Interval = 800 };
        _timer.Tick += (_, __) => Hide();
    }
    public void ShowToast(string text, Point? near = null)
    {
        _label.Text = text;
        Opacity = 0.95;
        Location = near.HasValue
            ? new Point(Math.Max(0, near.Value.X - Width / 2), Math.Max(0, near.Value.Y))
            : new Point(Screen.PrimaryScreen!.Bounds.Width / 2 - Width / 2, 20);
        Show();
        _timer.Stop();
        _timer.Start();
    }
}

public static class InputMethodName
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);

    public static string GetCurrent()
    {
        var h = GetForegroundWindow();
        var tid = GetWindowThreadProcessId(h, out _);
        var hkl = GetKeyboardLayout(tid);
        ushort langId = (ushort)((ulong)hkl & 0xFFFF);

        return langId switch
        {
            0x0409 => "EN",
            0x0804 => "CHS",
            0x0404 => "CHT",
            0x0411 => "JA",
            0x0412 => "KO",
            _ => $"0x{langId:X4}"
        };
    }
}

public static class SystemEventsWrapper
{
    // 简易：轮询 HKL 变化（120ms），变了就回调一次（足够灵敏）
    static SystemEventsWrapper()
    {
        var t = new System.Windows.Forms.Timer { Interval = 120 };
        IntPtr last = IntPtr.Zero;
        t.Tick += (_, __) =>
        {
            var h = GetKeyboardLayout(GetCurrentThreadId());
            if (h != last)
            {
                last = h;
                OnInputLangChange?.Invoke();
            }
        };
        t.Start();
    }

    public static event Action? OnInputLangChange;

    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
}
