// ClickFX — 入口
// 环境：.NET Framework 4.8 / STA

using System;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

        NativeMethods.timeBeginPeriod(1);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        EffectRegistry.Register(new LineBurstEffect());
        EffectRegistry.Register(new RippleEffect());
        EffectRegistry.Register(new SparkEffect());
        EffectRegistry.Register(new StarEffect());
        EffectRegistry.Register(new PetalEffect());

        var manager = new OverlayManager();
        manager.Start();

        Application.Run();

        ConfigManager.Save(manager.Config);
        manager.Dispose();

        NativeMethods.timeEndPeriod(1);
    }
}
