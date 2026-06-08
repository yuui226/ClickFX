// ClickFX — 核心运行时：鼠标钩子、Overlay 窗口、动画调度、托盘

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

// ==================== P/Invoke ====================

static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
        ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
        int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER pbmi,
        uint usage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

    [DllImport("kernel32.dll")]
    public static extern void RtlZeroMemory(IntPtr dest, uint count);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

[StructLayout(LayoutKind.Sequential)]
public struct POINT { public int X; public int Y; }

[StructLayout(LayoutKind.Sequential)]
public struct SIZE { public int cx; public int cy; }

[StructLayout(LayoutKind.Sequential)]
public struct MSLLHOOKSTRUCT
{
    public POINT pt;
    public uint mouseData;
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct BLENDFUNCTION
{
    public byte BlendOp;
    public byte BlendFlags;
    public byte SourceConstantAlpha;
    public byte AlphaFormat;
}

[StructLayout(LayoutKind.Sequential)]
public struct BITMAPINFOHEADER
{
    public uint biSize;
    public int biWidth;
    public int biHeight;
    public ushort biPlanes;
    public ushort biBitCount;
    public uint biCompression;
    public uint biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public uint biClrUsed;
    public uint biClrImportant;
}

// ==================== 常量 ====================

static class WinConsts
{
    public const int WH_MOUSE_LL = 14;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_HOTKEY = 0x0312;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int GWL_EXSTYLE = -20;
    public const int ULW_ALPHA = 0x00000002;
    public const byte AC_SRC_ALPHA = 0x01;
    public const byte AC_SRC_OVER = 0x00;
}

// ==================== 热键消息窗口 ====================

class HotkeyForm : Form
{
    public Action OnHotkeyPressed;

    const int HOTKEY_ID = 1;

    public bool RegisterHotkey(int modifiers, int key)
    {
        UnregisterHotkey();
        if (modifiers != 0 && key != 0)
            return NativeMethods.RegisterHotKey(Handle, HOTKEY_ID, modifiers, key);
        return true;
    }

    public void UnregisterHotkey()
    {
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
    }

    // 创建句柄但不显示窗口
    public void EnsureHandle()
    {
        var _ = Handle;
    }

    protected override void SetVisibleCore(bool value)
    {
        // 始终隐藏，不响应 Show()
        base.SetVisibleCore(false);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WinConsts.WM_HOTKEY && (int)m.WParam == HOTKEY_ID)
        {
            if (OnHotkeyPressed != null) OnHotkeyPressed();
        }
        base.WndProc(ref m);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW — 不在任务栏显示
            return cp;
        }
    }
}

// ==================== Overlay 管理器 ====================

class OverlayManager : IDisposable
{
    readonly List<OverlayForm> _overlays = new List<OverlayForm>();
    readonly List<AnimationState> _animations = new List<AnimationState>();
    readonly Timer _timer;
    IntPtr _hookId = IntPtr.Zero;
    LowLevelMouseProc _hookProc;
    NotifyIcon _trayIcon;
    ToolStripMenuItem _toggleItem;
    HotkeyForm _hotkeyForm;
    bool _enabled = true;
    bool _settingsOpen;

    public AppConfig Config { get; private set; }

    const int TICK_MS = 16;
    const int MAX_ANIMATIONS = 200;

    public OverlayManager()
    {
        Config = ConfigManager.Load();
        _timer = new Timer { Interval = TICK_MS };
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        _trayIcon = new NotifyIcon();
        _trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        _trayIcon.Text = "ClickFX";
        _trayIcon.Visible = true;

        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("设置");
        settingsItem.Click += (s, e) => OpenSettings();
        menu.Items.Add(settingsItem);

        _toggleItem = new ToolStripMenuItem("暂停");
        _toggleItem.Click += (s, e) => ToggleEnabled();
        menu.Items.Add(_toggleItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => Exit();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;

        foreach (var screen in Screen.AllScreens)
        {
            var form = new OverlayForm { Manager = this };
            form.SetBounds(screen.Bounds.X, screen.Bounds.Y,
                screen.Bounds.Width, screen.Bounds.Height);
            form.Show();
            _overlays.Add(form);
        }

        // 热键窗口（只创建句柄，不显示）
        _hotkeyForm = new HotkeyForm();
        _hotkeyForm.EnsureHandle();
        _hotkeyForm.OnHotkeyPressed = ToggleEnabled;
        ApplyHotkey();

        _hookProc = HookCallback;
        using (var process = Process.GetCurrentProcess())
        using (var module = process.MainModule)
        {
            _hookId = NativeMethods.SetWindowsHookEx(WinConsts.WH_MOUSE_LL, _hookProc,
                NativeMethods.GetModuleHandle(module.ModuleName), 0);
        }

        _timer.Start();
    }

    void ToggleEnabled()
    {
        _enabled = !_enabled;
        _toggleItem.Text = _enabled ? "暂停" : "启用";
    }

    void ApplyHotkey()
    {
        if (_hotkeyForm == null) return;
        if (Config.HotkeyModifiers != 0 && Config.HotkeyKey != 0)
        {
            if (!_hotkeyForm.RegisterHotkey(Config.HotkeyModifiers, Config.HotkeyKey))
            {
                MessageBox.Show("快捷键注册失败，可能已被其他程序占用。", "ClickFX",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        else
        {
            _hotkeyForm.UnregisterHotkey();
        }
    }

    void OpenSettings()
    {
        if (_settingsOpen) return;
        _settingsOpen = true;
        var original = Config;
        try
        {
            using (var form = new ConfigForm(Config))
            {
                form.OnPreview = preview => { Config = preview; };
                if (form.ShowDialog() == DialogResult.OK)
                {
                    Config = form.Result;
                    ConfigManager.Save(Config);
                    ApplyHotkey();
                }
                else
                {
                    Config = original; // 取消时还原
                }
            }
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    void OnTimerTick(object sender, EventArgs e)
    {
        lock (_animations)
        {
            for (int i = _animations.Count - 1; i >= 0; i--)
            {
                _animations[i].Age += TICK_MS;
                if (_animations[i].Age > _animations[i].Duration)
                {
                    _animations[i].Dispose();
                    _animations.RemoveAt(i);
                }
            }

            // 超出上限时挤掉最旧的动画，防止无限增长
            int overflow = _animations.Count - MAX_ANIMATIONS;
            if (overflow > 0)
            {
                for (int i = 0; i < overflow; i++)
                    _animations[i].Dispose();
                _animations.RemoveRange(0, overflow);
            }

            // 只 invalidate 有动画或上一帧有动画的屏幕，避免无动画屏幕做无用渲染
            bool hasAnimations = _animations.Count > 0;
            for (int i = 0; i < _overlays.Count; i++)
            {
                if (_overlays[i]._hadAnimations || (hasAnimations && HasAnimationsOnScreenLocked(_overlays[i].Bounds)))
                    _overlays[i].Invalidate();
            }
        }
    }

    // 调用方已持有 _animations 锁时使用此方法，避免重入锁开销
    bool HasAnimationsOnScreenLocked(Rectangle bounds)
    {
        for (int i = 0; i < _animations.Count; i++)
        {
            if (bounds.Contains(_animations[i].Position))
                return true;
        }
        return false;
    }

    IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _enabled)
        {
            var info = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                lParam, typeof(MSLLHOOKSTRUCT));
            int msg = (int)wParam;
            var pos = new Point(info.pt.X, info.pt.Y);

            bool triggerDown = Config.TriggerMode == "Down";
            int leftMsg = triggerDown ? WinConsts.WM_LBUTTONDOWN : WinConsts.WM_LBUTTONUP;
            int rightMsg = triggerDown ? WinConsts.WM_RBUTTONDOWN : WinConsts.WM_RBUTTONUP;

            if (msg == leftMsg)
                AddAnimation(pos, MouseButtons.Left);
            else if (msg == rightMsg)
                AddAnimation(pos, MouseButtons.Right);
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    static readonly Random _globalRng = new Random();
    int _lastClickTick = int.MinValue;

    void AddAnimation(Point pos, MouseButtons button)
    {
        int now = Environment.TickCount;
        if ((now - _lastClickTick & int.MaxValue) < 50) return;
        _lastClickTick = now;

        var effectName = (button == MouseButtons.Left)
            ? Config.LeftEffect
            : Config.RightEffect;
        var effect = EffectRegistry.Get(effectName) ?? EffectRegistry.GetFirst();
        if (effect == null) return;
        int duration = effect.Duration;

        int seed;
        lock (_globalRng) { seed = _globalRng.Next(); }

        lock (_animations)
        {
            _animations.Add(new AnimationState
            {
                Position = pos, Age = 0, Button = button, Duration = duration,
                RandomSeed = seed, CachedEffect = effect,
                Scale = Config.EffectScale
            });
        }
    }

    // 注意：返回的列表引用是共享的，调用方必须立即使用，不能跨帧持有。
    // 安全前提：WinForms 单线程，所有 OnPaint 在同一线程顺序执行。
    // 禁止在非 UI 线程访问此列表——若未来添加异步功能，需改为返回副本。
    readonly List<AnimationState> _screenAnims = new List<AnimationState>();

    public List<AnimationState> GetAnimationsForScreen(Rectangle bounds)
    {
        _screenAnims.Clear();
        lock (_animations)
        {
            for (int i = 0; i < _animations.Count; i++)
            {
                if (bounds.Contains(_animations[i].Position))
                    _screenAnims.Add(_animations[i]);
            }
        }
        return _screenAnims;
    }

    public void Exit() { Application.Exit(); }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        if (_hotkeyForm != null)
        {
            _hotkeyForm.UnregisterHotkey();
            try { _hotkeyForm.Dispose(); } catch { }
            _hotkeyForm = null;
        }
        _timer.Stop();
        _timer.Dispose();
        // 释放残留动画中的非托管资源（如 FingerEffect 的 Bitmap）
        lock (_animations)
        {
            for (int i = _animations.Count - 1; i >= 0; i--)
            {
                try { _animations[i].Dispose(); } catch { }
            }
            _animations.Clear();
        }
        for (int i = _overlays.Count - 1; i >= 0; i--)
        {
            try
            {
                _overlays[i].ReleaseDib();
                _overlays[i].ReleaseScreenDc();
                _overlays[i].Dispose();
            }
            catch { }
        }
        _overlays.Clear();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}

// ==================== 透明置顶窗口 ====================

class OverlayForm : Form
{
    public OverlayManager Manager { get; set; }

    // 缓存 DIB / DC / Graphics，避免每帧分配
    IntPtr _cachedHBitmap, _cachedHdcMem, _cachedOldObj, _cachedPBits;
    Graphics _cachedGraphics;
    int _cachedW, _cachedH;
    IntPtr _cachedHdcScreen; // 屏幕 DC，生命周期内不变，避免每帧重新获取
    Rectangle _prevAnimArea; // 上一帧的动效区域（非脏矩形，避免无限增长）
    internal bool _hadAnimations; // 上一帧是否有动画（用于按需 Invalidate）

    void EnsureDib(int w, int h)
    {
        if (_cachedHBitmap != IntPtr.Zero && _cachedW == w && _cachedH == h) return;
        ReleaseDib();

        var bih = new BITMAPINFOHEADER();
        bih.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
        bih.biWidth = w;
        bih.biHeight = -h;
        bih.biPlanes = 1;
        bih.biBitCount = 32;
        bih.biCompression = 0;

        _cachedHBitmap = NativeMethods.CreateDIBSection(IntPtr.Zero, ref bih, 0,
            out _cachedPBits, IntPtr.Zero, 0);
        if (_cachedHBitmap == IntPtr.Zero) return;
        if (_cachedHdcScreen == IntPtr.Zero)
            _cachedHdcScreen = NativeMethods.GetDC(IntPtr.Zero);
        _cachedHdcMem = NativeMethods.CreateCompatibleDC(_cachedHdcScreen);
        if (_cachedHdcMem == IntPtr.Zero) { NativeMethods.DeleteObject(_cachedHBitmap); _cachedHBitmap = IntPtr.Zero; return; }
        _cachedOldObj = NativeMethods.SelectObject(_cachedHdcMem, _cachedHBitmap);
        _cachedGraphics = Graphics.FromHdc(_cachedHdcMem);
        _cachedGraphics.SmoothingMode = SmoothingMode.AntiAlias;
        _cachedGraphics.CompositingQuality = CompositingQuality.HighSpeed;
        _cachedW = w;
        _cachedH = h;
    }

    public void ReleaseDib()
    {
        if (_cachedHBitmap == IntPtr.Zero) return;
        if (_cachedGraphics != null) { _cachedGraphics.Dispose(); _cachedGraphics = null; }
        NativeMethods.SelectObject(_cachedHdcMem, _cachedOldObj);
        NativeMethods.DeleteDC(_cachedHdcMem);
        NativeMethods.DeleteObject(_cachedHBitmap);
        _cachedHBitmap = IntPtr.Zero;
        _cachedW = 0;
        _cachedH = 0;
    }

    // 释放缓存的屏幕 DC（仅在 Overlay 销毁时调用）
    public void ReleaseScreenDc()
    {
        if (_cachedHdcScreen != IntPtr.Zero)
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, _cachedHdcScreen);
            _cachedHdcScreen = IntPtr.Zero;
        }
    }

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WinConsts.WS_EX_LAYERED | WinConsts.WS_EX_TRANSPARENT
                | WinConsts.WS_EX_TOPMOST | WinConsts.WS_EX_TOOLWINDOW;
            cp.Style |= WinConsts.WS_POPUP;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Manager == null) return;

        int w = Width;
        int h = Height;
        if (w <= 0 || h <= 0) return;

        var anims = Manager.GetAnimationsForScreen(Bounds);

        EnsureDib(w, h);
        if (_cachedHBitmap == IntPtr.Zero) return;

        // 脏矩形 = 上一帧动效区域 ∪ 当前帧动效区域（不含历史，避免无限增长）
        // 覆盖最大效果范围：手指 emoji 对角线(~117) + 远端距离(55) + 指尖偏移(~76) ≈ 190
        const int BASE_MARGIN = 200;
        Rectangle dirty = _prevAnimArea;
        Rectangle curAnimArea = Rectangle.Empty;

        for (int i = 0; i < anims.Count; i++)
        {
            int margin = (int)(BASE_MARGIN * anims[i].Scale);
            int ax = anims[i].Position.X - Left - margin;
            int ay = anims[i].Position.Y - Top - margin;
            var area = new Rectangle(ax, ay, margin * 2, margin * 2);
            dirty = Rectangle.Union(dirty, area);
            curAnimArea = Rectangle.Union(curAnimArea, area);
        }

        dirty.Intersect(new Rectangle(0, 0, w, h));
        if (!dirty.IsEmpty)
        {
            int stride = w * 4;
            // 当脏矩形宽度远小于屏幕宽度时，逐行清除避免清零不必要的像素
            if (dirty.Width < w * 3 / 4)
            {
                int rowBytes = dirty.Width * 4;
                int rowStart = dirty.X * 4;
                for (int row = 0; row < dirty.Height; row++)
                {
                    IntPtr ptr = _cachedPBits + (dirty.Y + row) * stride + rowStart;
                    NativeMethods.RtlZeroMemory(ptr, (uint)rowBytes);
                }
            }
            else
            {
                IntPtr start = _cachedPBits + dirty.Y * stride;
                uint bytes = (uint)(dirty.Height * stride);
                NativeMethods.RtlZeroMemory(start, bytes);
            }
        }

        if (anims.Count > 0)
        {
            for (int i = 0; i < anims.Count; i++)
            {
                var color = (anims[i].Button == MouseButtons.Left)
                    ? Manager.Config.LeftClick
                    : Manager.Config.RightClick;
                var effect = anims[i].CachedEffect;
                if (effect != null)
                    effect.Draw(_cachedGraphics, anims[i], color, Bounds);
            }
        }

        _prevAnimArea = curAnimArea;
        _hadAnimations = anims.Count > 0;

        var pptDst = new POINT { X = Left, Y = Top };
        var size = new SIZE { cx = w, cy = h };
        var pptSrc = new POINT { X = 0, Y = 0 };
        var blend = new BLENDFUNCTION();
        blend.BlendOp = WinConsts.AC_SRC_OVER;
        blend.BlendFlags = 0;
        blend.SourceConstantAlpha = 255;
        blend.AlphaFormat = WinConsts.AC_SRC_ALPHA;

        NativeMethods.UpdateLayeredWindow(Handle, _cachedHdcScreen, ref pptDst, ref size,
            _cachedHdcMem, ref pptSrc, 0, ref blend, WinConsts.ULW_ALPHA);
    }
}
