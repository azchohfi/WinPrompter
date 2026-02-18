using System.IO.Pipes;
using System.Text;

namespace WinPrompter;

/// <summary>
/// Custom entry point that enforces single-instance behavior.
/// If another instance is running, sends the file path via named pipe and exits.
/// </summary>
public static class Program
{
    private const string MutexName = "WinPrompter_SingleInstance_Mutex";
    private const string PipeName = "WinPrompter_FilePipe";

    [STAThread]
    public static void Main(string[] args)
    {
        // Find file argument (first arg that looks like a file path)
        string? filePath = args.FirstOrDefault(a => !a.StartsWith('-') && File.Exists(a));

        using var mutex = new Mutex(true, MutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            // Another instance is already running — send the file path and exit
            if (filePath != null)
                SendFileToExistingInstance(filePath);
            return;
        }

        // We are the first instance — store the file path for MainWindow to pick up
        App.StartupFilePath = filePath;

        // Start WinUI application
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(p =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }

    private static void SendFileToExistingInstance(string filePath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000); // 2 second timeout
            var bytes = Encoding.UTF8.GetBytes(filePath);
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
        }
        catch { /* Existing instance may not be listening yet — silently fail */ }
    }
}
