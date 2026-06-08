// ClickFX — 入口
// 环境：.NET Framework 4.8 / STA

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    static Mutex _mutex;

    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        // 声明 DPI 感知，确保鼠标坐标和窗口坐标一致
        SetProcessDPIAware();

        bool createdNew;
        _mutex = new Mutex(true, "ClickFX_SingleInstance", out createdNew);
        if (!createdNew)
        {
            MessageBox.Show("ClickFX 已在运行中。", "ClickFX",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        EffectRegistry.Register(new LineBurstEffect());
        EffectRegistry.Register(new RippleEffect());
        EffectRegistry.Register(new SparkEffect());
        EffectRegistry.Register(new StarEffect());
        EffectRegistry.Register(new PetalEffect());
        EffectRegistry.Register(new VortexEffect());
        EffectRegistry.Register(new FragmentEffect());
        EffectRegistry.Register(new MeteorEffect());
        EffectRegistry.Register(new LightningEffect());
        EffectRegistry.Register(new FingerEffect());

        // 同步开机自启动注册表
        var config = ConfigManager.Load();
        ConfigManager.SetAutoStart(config.AutoStart);

        var manager = new OverlayManager();
        try
        {
            manager.Start();
            Application.Run();
        }
        finally
        {
            try { ConfigManager.Save(manager.Config); } catch { }
            manager.Dispose();
            EffectRegistry.Cleanup();
            GC.KeepAlive(_mutex);
        }
    }
}
