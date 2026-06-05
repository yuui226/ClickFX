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

        var manager = new OverlayManager();
        manager.Start();

        try
        {
            Application.Run();
        }
        finally
        {
            ConfigManager.Save(manager.Config);
            manager.Dispose();
            EffectRegistry.Cleanup();
            GC.KeepAlive(_mutex);
        }
    }
}
