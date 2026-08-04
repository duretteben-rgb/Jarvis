using Jarvis.Runtime.Startup;

namespace Jarvis.Runtime;

/// <summary>
/// Entry point of the JARVIS OS headless runtime.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await StartupRunner.RunAsync(args);
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"JARVIS runtime failed: {ex.Message}");
            return 1;
        }
    }
}
