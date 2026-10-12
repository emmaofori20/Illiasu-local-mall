using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace ILLiasu_Local_Mall.Infrastructure.Logging;

/// <summary>Projects EVERY event before any file/console sink receives it. Never renders input messages.</summary>
public sealed class SafeDiagnosticSink(Serilog.ILogger destination) : ILogEventSink, IDisposable
{
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("Diagnostic {DiagnosticEvent} from {SourceContext}");
    private static readonly HashSet<string> Events = ["ApplicationStarting", "ApplicationStarted", "ApplicationStopping",
        "ApplicationStopped", "HostFailure", "HttpRequestCompleted", "UnhandledRequestException", "IdentityOutcome", "LoggingUnavailable"];
    private static readonly HashSet<string> Outcomes = ["Succeeded", "Rejected", "InvalidInput", "LockedOut", "NotAllowed", "RequiresTwoFactor", "Aborted", "Failed"];
    private static readonly HashSet<string> Operations = ["Login", "Registration", "CustomerRoleAssignment", "Logout"];
    private static readonly HashSet<string> IdentityCodes = ["DuplicateEmail", "DuplicateUserName", "InvalidEmail", "InvalidUserName",
        "PasswordTooShort", "PasswordRequiresNonAlphanumeric", "PasswordRequiresDigit", "PasswordRequiresLower", "PasswordRequiresUpper",
        "PasswordRequiresUniqueChars", "UserAlreadyInRole", "UserNotInRole", "ConcurrencyFailure", "Other"];

    public static LogEvent Project(LogEvent input)
    {
        var properties = new List<LogEventProperty>();
        void Add(string name, object value) => properties.Add(new(name, new ScalarValue(value)));
        string? Text(string name) => input.Properties.TryGetValue(name, out var value) && value is ScalarValue { Value: string text } ? text : null;
        void Choice(string name, HashSet<string> allowed)
        { var text = Text(name); if (text != null && allowed.Contains(text)) Add(name, text); }
        var eventName = Text("DiagnosticEvent");
        Add("DiagnosticEvent", eventName != null && Events.Contains(eventName) ? eventName : "FrameworkDiagnostic");
        var source = Text("SourceContext");
        Add("SourceContext", source != null && source.Length <= 200
            && Regex.IsMatch(source, @"^(ILLiasu_Local_Mall|Microsoft|System)(\.[A-Za-z0-9_`]+)*$", RegexOptions.CultureInvariant)
            ? source : "Application");
        Choice("Outcome", Outcomes);
        Choice("IdentityOperation", Operations);
        Choice("Environment", ["Development", "Production", "Staging"]);
        Choice("Method", ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "OTHER"]);
        Choice("LoggingFailure", ["UnsafeDirectory", "FileUnavailable"]);
        foreach (var name in new[] { "RequestId", "InstanceId" })
        {
            var value = Text(name);
            if (value != null && Regex.IsMatch(value, "^[a-f0-9]{32}$", RegexOptions.CultureInvariant)) Add(name, value);
        }
        var endpoint = Text("Endpoint");
        if (endpoint != null && (endpoint is "Unmatched" or "Static" ||
            Regex.IsMatch(endpoint, @"^[A-Za-z][A-Za-z0-9]{0,60}\.[A-Za-z][A-Za-z0-9]{0,60}$", RegexOptions.CultureInvariant)))
            Add("Endpoint", endpoint);
        foreach (var name in new[] { "ProcessId", "StatusCode", "ElapsedMilliseconds", "ManagedMemoryMB" })
            if (input.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar
                && scalar.Value is int or long or double or decimal)
                properties.Add(new(name, scalar));
        if (input.Properties.TryGetValue("EventId", out var eventId) && eventId is StructureValue structure)
        {
            var id = structure.Properties.FirstOrDefault(p => p.Name == "Id")?.Value;
            if (id is ScalarValue { Value: int number }) Add("FrameworkEventId", number);
        }
        // Code descriptions may contain email/user identifiers: keep only known codes.
        if (input.Properties.TryGetValue("IdentityCodes", out var codes) && codes is SequenceValue sequence)
            properties.Add(new("IdentityCodes", new SequenceValue(sequence.Elements.Take(8).Select(code =>
                new ScalarValue(code is ScalarValue { Value: string text } && IdentityCodes.Contains(text) ? text : "Other")))));
        if (input.Exception != null)
        {
            var exceptions = new List<Exception>();
            void Visit(Exception error)
            {
                if (exceptions.Count >= 5) return;
                exceptions.Add(error);
                if (error is AggregateException aggregate) foreach (var inner in aggregate.InnerExceptions.Take(4)) Visit(inner);
                else if (error.InnerException != null) Visit(error.InnerException);
            }
            Visit(input.Exception);
            properties.Add(new("ExceptionTypes", new SequenceValue(exceptions.Select(e => new ScalarValue(e.GetType().Name)))));
            var frames = new StackTrace(input.Exception, false).GetFrames()?.Take(8)
                .Select(frame => frame.GetMethod()).Where(method => method != null)
                .Select(method => new ScalarValue($"{method!.DeclaringType?.FullName}.{method.Name}")) ?? [];
            properties.Add(new("ExceptionMethods", new SequenceValue(frames)));
            var errors = exceptions.OfType<SqlException>().SelectMany(e => e.Errors.Cast<SqlError>()).Take(8).ToArray();
            if (errors.Length != 0)
            {
                properties.Add(new("SqlErrors", new SequenceValue(errors.Select(e => new StructureValue([
                    new("Number", new ScalarValue(e.Number)), new("State", new ScalarValue((int)e.State)), new("Class", new ScalarValue((int)e.Class))])))));
                Add("SqlFailureCategory", errors.Any(e => e.Number is 18456 or 18452 or 4060 or 4064) ? "LoginOrDatabaseAccess"
                    : errors.Any(e => e.Number is 208 or 207) ? "SchemaMismatch"
                    : errors.Any(e => e.Number == -2) ? "Timeout" : "ConnectivityOrCommandFailure");
            }
        }
        // No Exception object, raw template, rendered message, arbitrary scopes or parameters survive.
        return new LogEvent(input.Timestamp.ToUniversalTime(), input.Level, null, Template, properties);
    }

    public void Emit(LogEvent logEvent)
    {
        try { destination.Write(Project(logEvent)); }
        catch { ApplicationLogging.ReportSinkFailure(); }
    }
    public void Dispose()
    { try { (destination as IDisposable)?.Dispose(); } catch { ApplicationLogging.ReportSinkFailure(); } }
}
