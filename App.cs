using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Reflection;
namespace IMEInd;

sealed class ToastForm : Form
{
    readonly Label _label;
    readonly Label _icon;
    readonly System.Windows.Forms.Timer _timer;


    public ToastForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        var height = 80;
        _icon = new Label
        {
            Text = "\uF2B7",
            AutoSize = true,
            Font = new Font("Segoe Fluent Icons", 20),
            ForeColor = Color.White,
            BackColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 0),
            MaximumSize = new Size(int.MaxValue, height),
            MinimumSize = new Size(0, height),
        };
        Controls.Add(_icon);
        _label = new Label
        {
            ForeColor = Color.White,
            BackColor = Color.Black,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            Location = new Point(_icon.Width, 0),
            MaximumSize = new Size(int.MaxValue, height),
            MinimumSize = new Size(0, height),
        };
        Controls.Add(_label);

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Black;
        Opacity = 0.5;
        _timer = new System.Windows.Forms.Timer { Interval = 5000 };
        _timer.Tick += (_, __) => Hide();
    }
    public void ShowToast(string text, Point? near = null)
    {
        // Ensure UI updates happen on the UI thread
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowToast(text, near)));
            return;
        }

        _label.Text = text;
        // Ensure layout reflects new text/icon sizes before positioning
        PerformLayout();
        // Reposition label in case icon width changed
        _label.Location = new Point(_icon.Width, 0);

        Opacity = 0.95;
        Location = near.HasValue
            ? new Point(Math.Max(0, near.Value.X - Width / 2), Math.Max(0, near.Value.Y))
            : new Point(Screen.PrimaryScreen!.Bounds.Width / 2 - Width / 2, Screen.PrimaryScreen!.Bounds.Height - Height - 100);

        Show();
        // Restart timer on UI thread so Tick will fire correctly
        _timer.Stop();
        _timer.Start();
    }
}


class App
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
#if DEBUG
    [DllImport("kernel32.dll")] static extern bool AllocConsole();
#endif

    public record IME(int LangID)
    {
        public string Name => LangID switch
        {
            0x0409 => "English",
            0x0804 => "简体中文",
            0x0404 => "繁體中文",
            0x0411 => "日本語",
            0x0412 => "한국어",
            _ => $"0x{LangID:X4}"
        };
    }

    private Listener listener = new Listener();

    public static Screen GetScreenFromWindowHandle(nint windowHandler)
    {
        try
        {
            return Screen.FromHandle(windowHandler);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to get screen for window handle {windowHandler}: {ex}");
            // Fallback to primary screen if anything goes wrong
            return Screen.PrimaryScreen!;
        }
    }

    public void Show(ToastForm indicator, IME ime, nint windowHandler)
    {
        Screen screen = GetScreenFromWindowHandle(windowHandler);
        var centerX = screen.Bounds.X + screen.Bounds.Width / 2;
        var y = screen.Bounds.Y + screen.Bounds.Height - 100 - indicator.Height;
        var nearPoint = new Point(centerX, Math.Max(screen.Bounds.Y, y));
        indicator.ShowToast($"{ime.Name}", nearPoint);
    }

    [STAThread]
    public void Run()
    {
#if DEBUG
        try
        {
            AllocConsole();
            Console.WriteLine("[DEBUG] Console allocated. IMEInd starting...");
        }
        catch { /* If console allocation fails, continue silently. */ }
#endif

        ApplicationConfiguration.Initialize();
        var indicator = new ToastForm();

        // Load icon from embedded resource
        Icon? trayIconImage = null;
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "IMEInd.resources.icon.ico";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                trayIconImage = new Icon(stream);
            }
        }
        catch
        {
            // Fallback to system icon if loading fails
        }

        // Create tray icon
        var trayIcon = new NotifyIcon
        {
            Icon = trayIconImage ?? SystemIcons.Information,
            Text = "IME Indicator",
            Visible = true
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (s, e) =>
        {
            trayIcon.Visible = false;
            Application.Exit();
        };
        contextMenu.Items.Add(exitMenuItem);
        trayIcon.ContextMenuStrip = contextMenu;

        listener.OnInputLangChange += (ime, windowHandler) =>
        {
            Show(indicator, ime, windowHandler);
        };

        listener.OnFocusChanged += (ime, windowHandler) =>
        {
            Show(indicator, ime, windowHandler);
        };
        listener.FirstRun += (ime, windowHandler) =>
        {
            Show(indicator, ime, windowHandler);
        };

        listener.Start();

        Application.Run(new ApplicationContext());

        // Cleanup
        Automation.RemoveAllEventHandlers();
        trayIcon.Dispose();

    }
    public class Listener
    {
        private DateTime lastTime = DateTime.MinValue;
        private IME lastIME = new IME(0);
        private nint lastWindow = IntPtr.Zero;
        private Screen lastScreen = Screen.PrimaryScreen!;
        IME GetCurrentIME()
        {
            var h = GetForegroundWindow();
            var tid = GetWindowThreadProcessId(h, out _);
            var hkl = GetKeyboardLayout(tid);
            ushort langID = (ushort)((ulong)hkl & 0xFFFF);
            return new IME(langID);
        }

        private nint GetForegroundInputWindow()
        {
            var hWnd = GetForegroundWindow();
            if (lastWindow == IntPtr.Zero)
            {
                return hWnd;
            }
            var el = AutomationElement.FromHandle(hWnd);
            if (el != null)
            {
                var ct = el.Current.ControlType;
                bool editable = ct == ControlType.Edit || ct == ControlType.Document
                                || el.TryGetCurrentPattern(ValuePattern.Pattern, out _)
                                || el.TryGetCurrentPattern(TextPattern.Pattern, out _);
                if (editable)
                {
                    return hWnd;
                }
            }
            return lastWindow;
        }


        private System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 120 };
        private void forceUpdateIME()
        {
            lastIME = GetCurrentIME();
        }

        private void forceUpdateWindow()
        {
            var h = GetForegroundInputWindow();
            lastWindow = h;
            forceUpdateScreen();
        }

        private void forceUpdateTime()
        {
            lastTime = DateTime.Now;
        }

        protected void logDebug(string msg)
        {
            Console.Error.WriteLine($"[DEBUG: Listener] {msg}");
        }

        public void Start()
        {
            forceUpdateIME();
            forceUpdateWindow();
            forceUpdateTime();
            logDebug($"Initial IME: {lastIME.Name}, Window: {lastWindow}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
            FirstRun?.Invoke(lastIME, lastWindow);
            timer.Start();
        }

        private void forceUpdateScreen()
        {
            try
            {
                lastScreen = GetScreenFromWindowHandle(lastWindow);
            }
            catch
            {
                lastScreen = Screen.PrimaryScreen!;
            }
        }

        public Listener()
        {
            timer.Tick += (_, __) =>
            {
                var h = GetCurrentIME();
                logDebug($"Checked IME: {h.Name}, Last IME: {lastIME.Name}");
                if (h != lastIME)
                {
                    lastIME = h;
                    forceUpdateTime();
                    forceUpdateWindow();
                    logDebug($"IME changed to: {lastIME.Name}, Window: {lastWindow}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    OnInputLangChange?.Invoke(lastIME, lastWindow);
                }
            };
            timer.Tick += (_, __) =>
            {
                var h = GetForegroundInputWindow();
                var now = DateTime.Now;
                logDebug($"Checked Window: {h}, Last Window: {lastWindow}");
                logDebug($"Time since last change: {(now - lastTime).TotalSeconds} seconds");
                logDebug($"Current Screen: {GetScreenFromWindowHandle(h).DeviceName}, Last Screen: {lastScreen.DeviceName}");
                if (h != lastWindow && ((now - lastTime).TotalSeconds >= 300 || GetScreenFromWindowHandle(h) != lastScreen))
                {
                    lastWindow = h;
                    forceUpdateTime();
                    forceUpdateIME();
                    logDebug($"Window changed to: {lastWindow}, IME: {lastIME.Name}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    OnFocusChanged?.Invoke(lastIME, lastWindow);
                }
            };
        }

        public delegate void Action(IME ime, nint hwnd);

        public event Action? OnInputLangChange;
        public event Action? OnFocusChanged;
        public event Action? FirstRun;

        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    }
}
