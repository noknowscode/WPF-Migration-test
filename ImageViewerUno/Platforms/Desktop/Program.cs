using Uno.UI.Hosting;

namespace ImageViewerUno;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Capture command-line args here — Uno.UI.Hosting does not forward them into
        // LaunchActivatedEventArgs.Arguments on Skia Desktop, and Environment.GetCommandLineArgs()
        // behaviour varies across launch vectors (dotnet run, direct exe, IDE launch).
        App.LaunchArgs = args;

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
