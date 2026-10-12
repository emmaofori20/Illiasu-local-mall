using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace ILLiasu_Local_Mall.Infrastructure.Logging;

public static class ApplicationLogging
{
    public const long FileSizeLimit = 2 * 1024 * 1024;
    public const int RetainedFiles = 8;
    private static long nextFailureReport;

    public static string ResolveDirectory(string contentRoot, string? webRoot, string? configured)
    {
        var root = Path.GetFullPath(contentRoot);
        var privateRoot = Path.Combine(root, "App_Data");
        var directory = Path.GetFullPath(configured ?? "App_Data/logs", root);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var publicRoot = Path.GetFullPath(webRoot ?? Path.Combine(root, "wwwroot"));
        if (!directory.StartsWith(privateRoot + Path.DirectorySeparatorChar, comparison)
            || directory.Equals(publicRoot, comparison) || directory.StartsWith(publicRoot + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("Unsafe diagnostic directory.");
        // Existing links/junctions must not redirect private storage into public files.
        for (var path = directory; path != null && !path.Equals(root, comparison); path = Path.GetDirectoryName(path))
            if ((Directory.Exists(path) || File.Exists(path)) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException("Linked diagnostic directory refused.");
        return directory;
    }

    public static Logger Create(string contentRoot, string? webRoot = null, string? configured = null)
    {
        // Serilog internal diagnostics can contain paths/raw event text: never print their input.
        SelfLog.Enable(_ => ReportSinkFailure());
        var output = new LoggerConfiguration().WriteTo.Console(new JsonFormatter());
        var failure = "";
        try
        {
            string directory;
            try { directory = ResolveDirectory(contentRoot, webRoot, configured); }
            catch { failure = "UnsafeDirectory"; throw; }
            Directory.CreateDirectory(directory);
            // A real write probe gives a visible startup warning if the directory is unwritable.
            var probe = Path.Combine(directory, ".write-check-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
            output.WriteTo.File(new JsonFormatter(), Path.Combine(directory, "app-.jsonl"),
                rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true,
                fileSizeLimitBytes: FileSizeLimit, retainedFileCountLimit: RetainedFiles,
                retainedFileTimeLimit: TimeSpan.FromDays(7), shared: true, buffered: false);
        }
        catch { if (failure.Length == 0) failure = "FileUnavailable"; }
        var logger = new LoggerConfiguration().MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning).MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext().Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("InstanceId", Guid.NewGuid().ToString("N"))
            .WriteTo.Sink(new SafeDiagnosticSink(output.CreateLogger())).CreateLogger();
        if (failure.Length != 0)
            logger.Warning("Diagnostic {DiagnosticEvent} {LoggingFailure}", "LoggingUnavailable", failure);
        return logger;
    }

    internal static void ReportSinkFailure()
    {
        // Fixed safe fallback; one report/minute across threads, no recursive logging.
        try
        {
            var now = Environment.TickCount64;
            var next = Interlocked.Read(ref nextFailureReport);
            if (now < next || Interlocked.CompareExchange(ref nextFailureReport, now + 60_000, next) != next) return;
            Console.Error.WriteLine("{\"DiagnosticEvent\":\"LoggingWriteFailure\",\"Level\":\"Warning\"}");
        }
        catch { /* Diagnostics must not terminate requests. */ }
    }
}
