using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Reflection;
using System.Diagnostics;
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
    public void ShowToast(string text, Screen screen)
    {
        // Ensure UI updates happen on the UI thread
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ShowToast(text, screen)));
            return;
        }
        var centerX = screen.Bounds.X + screen.Bounds.Width / 2;
        var y = screen.Bounds.Y + screen.Bounds.Height - 100 - Height;
        var nearPoint = new Point(centerX, Math.Max(screen.Bounds.Y, y));

        _label.Text = text;
        // Ensure layout reflects new text/icon sizes before positioning
        PerformLayout();
        // Reposition label in case icon width changed
        _label.Location = new Point(_icon.Width, 0);

        Opacity = 0.95;

        Location = nearPoint;

        Show();
        // Restart timer on UI thread so Tick will fire correctly
        _timer.Stop();
        _timer.Start();
    }
}


class App
{
    public static void log(string msg)
    {
        Console.Error.WriteLine($"[LOGGING] {msg}");
    }
    public static int LogLevel = 2; // 0: None, 1: Error, 2: Info, 3: Debug

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
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

    private record struct DebugRecord(
        nint? hWnd,
        string? controlType,
        string? uiElement,
        bool? isValuePattern,
        bool? isTextPattern,
        bool? hasLegacyPatternType,
        string? className,
        bool? hasEditableChild
    )
    {
        public DebugRecord() : this(null, null, null, null, null, null, null, null) { }
    }
#if DEBUG
    private static DebugRecord? lastDebugRecord = null;
#endif

    public static bool IsEditable(AutomationElement element)
    {
        var (r, debugRecord) = _IsEditable(element);
#if DEBUG
        if (lastDebugRecord != debugRecord)
        {
            lastDebugRecord = debugRecord;
            log($"DebugRecord: {debugRecord}");
        }
#endif
        return r;
    }
    private static (bool, DebugRecord) _IsEditable(AutomationElement element)
    {
        DebugRecord debugRecord = new DebugRecord();
        if (element == null)
            return (false, debugRecord);
        ControlType ct;
        try
        {
            ct = element.Current.ControlType;
        }
        catch
        {
            return (false, debugRecord);
        }
#if DEBUG
        debugRecord.controlType = ct.LocalizedControlType;
#endif
        if (ct == ControlType.Edit || ct == ControlType.Document)
            return (true, debugRecord);

        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj))
        {
#if DEBUG
            debugRecord.isValuePattern = true;
#endif
            var valuePattern = (ValuePattern)valuePatternObj;
            if (!valuePattern.Current.IsReadOnly)
                return (true, debugRecord);
        }
#if DEBUG
        debugRecord.isValuePattern = false;
#endif

        if (element.TryGetCurrentPattern(TextPattern.Pattern, out _))
        {
#if DEBUG
            debugRecord.isTextPattern = true;
#endif
            return (true, debugRecord);
        }

#if DEBUG
        debugRecord.isTextPattern = false;
#endif
        var className = element.Current.ClassName;
#if DEBUG
        debugRecord.className = className;
#endif
        if (!string.IsNullOrEmpty(className) &&
            (className.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
             className.Contains("TextArea", StringComparison.OrdinalIgnoreCase)))
            return (true, debugRecord);

        var child = element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
#if DEBUG
        debugRecord.hasEditableChild = child != null;
#endif
        if (child != null)
            return (true, debugRecord);

        return (false, debugRecord);
    }

    public void Show(ToastForm indicator, IME ime, Screen screen)
    {
        if (LogLevel >= 2)
        {
            log($"Showing IME toast: {ime.Name} on screen {screen.DeviceName}");
        }
        indicator.ShowToast($"{ime.Name}", screen);
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

        listener.OnInputLangChange += (ime, screen) =>
        {
            Show(indicator, ime, screen);
        };

        listener.OnFocusChanged += (ime, screen) =>
        {
            Show(indicator, ime, screen);
        };
        listener.FirstRun += (ime, screen) =>
        {
            Show(indicator, ime, screen);
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
        // For ancestor retrieval
        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        IME GetCurrentIME()
        {
            var h = GetForegroundWindow();
            var tid = GetWindowThreadProcessId(h, out _);
            var hkl = GetKeyboardLayout(tid);
            ushort LangID = (ushort)((ulong)hkl & 0xFFFF);
            return new IME(LangID);
        }

        private static bool IsWebView2HostClass(string? className) =>
            !string.IsNullOrEmpty(className) && (
                className.Contains("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("WebView", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("CefBrowserWindow", StringComparison.OrdinalIgnoreCase));

        private static IntPtr GetTopLevel(IntPtr h)
        {
            if (h == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                var top = GetAncestor(h, GA_ROOT);
                return top != IntPtr.Zero ? top : h;
            }
            catch
            {
                return h;
            }
        }

        // Normalizes a raw UIA handle to a real top-level window; falls back to process MainWindow or foreground window.
        private nint GetRealWindow(nint rawHandle, AutomationElement? el)
        {
            IntPtr candidate = rawHandle;
            string? className = null;
            int? processId = null;
            if (el != null)
            {
                try
                {
                    className = el.Current.ClassName;
                    processId = el.Current.ProcessId;
                }
                catch { }
            }

            // Normalize to top-level ancestor if possible.
            candidate = GetTopLevel(candidate);

            // If the class name looks like an embedded browser surface, prefer foreground top-level window.
            if (candidate == IntPtr.Zero || IsWebView2HostClass(className))
            {
                var fg = GetForegroundWindow();
                if (fg != IntPtr.Zero) candidate = fg;
            }

            // Process-based fallback: sometimes MainWindowHandle differs from the raw content handle.
            if (candidate == IntPtr.Zero && processId.HasValue)
            {
                try
                {
                    var proc = Process.GetProcessById(processId.Value);
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        candidate = proc.MainWindowHandle;
                    }
                }
                catch { }
            }

            // Final fallback to desktop window.
            if (candidate == IntPtr.Zero)
            {
                candidate = GetDesktopWindow();
            }
            return candidate;
        }

        private nint GetForegroundInputWindow()
        {
            var el = AutomationElement.FocusedElement;
            IntPtr rawHandle = IntPtr.Zero;
            if (el != null)
            {
                try
                {
                    rawHandle = new IntPtr(el.Current.NativeWindowHandle);
                }
                catch { rawHandle = IntPtr.Zero; }
            }

            var candidate = GetRealWindow(rawHandle, el);

            if (lastWindow == IntPtr.Zero)
            {
                if (LogLevel >= 2)
                {
                    log($"Initial resolved window handle: {candidate}");
                }
                return candidate;
            }

            bool editable = el != null && IsEditable(el);
            if (LogLevel >= 3)
            {
                log($"Raw handle: {rawHandle}, Resolved handle: {candidate}, Editable: {editable}");
            }
            if (editable && candidate != IntPtr.Zero)
            {
                if (LogLevel >= 3)
                {
                    log($"Focused element editable; using resolved handle: {candidate}");
                }
                return candidate;
            }

            // If candidate differs from lastWindow, prefer the fresh candidate even if not editable to avoid staleness.
            if (candidate != IntPtr.Zero && candidate != lastWindow)
            {
                if (LogLevel >= 2)
                {
                    log($"Non-editable focus; updating window to new handle: {candidate}");
                }
                return candidate;
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

        public void Start()
        {
            forceUpdateIME();
            forceUpdateWindow();
            forceUpdateTime();
            if (LogLevel >= 3)
            {
                log($"Initial IME: {lastIME.LangID}, Window: {lastWindow}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
            }
            FirstRun?.Invoke(lastIME, lastScreen);
            timer.Start();
        }

        private void forceUpdateScreen()
        {
            try
            {
                lastScreen = GetScreenFromWindowHandle(lastWindow);
            }
            catch (Exception ex)
            {
                if (LogLevel >= 1)
                {
                    log($"ERROR: Failed to get screen from window handle {lastWindow}: {ex}");
                }
                lastScreen = Screen.PrimaryScreen!;
            }
        }

        public Listener()
        {
            timer.Tick += (_, __) =>
            {
                var h = GetCurrentIME();
                if (LogLevel >= 3)
                {
                    log($"Checked IME: {h.LangID}, Last IME: {lastIME.LangID}");
                }
                if (h != lastIME)
                {
                    lastIME = h;
                    forceUpdateTime();
                    forceUpdateWindow();
                    if (LogLevel >= 2)
                    {
                        log($"IME changed to: {lastIME.LangID}, Window: {lastWindow}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    }
                    OnInputLangChange?.Invoke(lastIME, lastScreen);
                }
            };
            timer.Tick += (_, __) =>
            {
                var h = GetForegroundInputWindow();
                var now = DateTime.Now;
                if (LogLevel >= 3)
                {
                    log($"Checked Window: {h}, Last Window: {lastWindow}");
                    log($"Time since last change: {(now - lastTime).TotalSeconds} seconds");
                    log($"Current Screen: {GetScreenFromWindowHandle(h).DeviceName}, Last Screen: {lastScreen.DeviceName}");
                }
                if (LogLevel >= 2)
                {
                    if (h != lastWindow)
                    {
                        log($"Last screen: {lastScreen.DeviceName}, New screen: {GetScreenFromWindowHandle(h).DeviceName}");
                    }
                }
                if (h != lastWindow && ((now - lastTime).TotalSeconds >= 300 || GetScreenFromWindowHandle(h) != lastScreen))
                {
                    lastWindow = h;
                    forceUpdateScreen();
                    forceUpdateTime();
                    forceUpdateIME();
                    if (LogLevel >= 2)
                    {
                        log($"Window changed to: {lastWindow}, IME: {lastIME.LangID}, Screen: {lastScreen.DeviceName}, Time: {lastTime}");
                    }
                    OnFocusChanged?.Invoke(lastIME, lastScreen);
                }
            };
        }

        public delegate void Action(IME ime, Screen screen);

        public event Action? OnInputLangChange;
        public event Action? OnFocusChanged;
        public event Action? FirstRun;

        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    }
}
