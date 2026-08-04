using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Jarvis.Core.Logging;

/// <summary>
/// Central logging configuration shared by every JARVIS host (runtime, UI, ...).
/// </summary>
public static class JarvisLogging
{
    /// <summary>
    /// Configures a console logger with a compact, single-line format and suppresses the
    /// default provider set added by the generic host.
    /// </summary>
    public static void Configure(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss.fff ";
            options.UseUtcTimestamp = false;
            options.ColorBehavior = LoggerColorBehavior.Enabled;
        });
        logging.AddDebug();
    }
}
