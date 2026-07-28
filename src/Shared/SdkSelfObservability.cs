// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal;

/// <summary>
/// POC: routes SDK self-observability events (per
/// open-telemetry/semantic-conventions#3723) to a user-supplied
/// <see cref="ILogger"/>. Validates the .NET counterpart of the Rust POC,
/// which dispatches via the global <c>tracing</c> subscriber.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally a process-global, mutable sink rather than a
/// property on each component. It mirrors the Rust POC's reliance on
/// <c>tracing</c>'s global subscriber and keeps the per-component patch tiny.
/// A production design would replace this with explicit injection (e.g.
/// per-provider) once an emission target is settled on for the SDK.
/// </para>
/// <para>
/// The user MUST configure a separate <see cref="ILoggerFactory"/> backed by a
/// dedicated <see cref="Logs.LoggerProvider"/> for self-observability events:
/// routing them through the same <see cref="Logs.LoggerProvider"/> whose
/// components are emitting them would invite shutdown-time deadlock and lost
/// records.
/// </para>
/// </remarks>
internal static class SdkSelfObservability
{
    private static ILogger? logger;

    /// <summary>
    /// Sets the <see cref="ILogger"/> that SDK self-observability events are
    /// emitted to. Pass <see langword="null"/> to disable emission.
    /// </summary>
    /// <param name="value">The logger, or <see langword="null"/>.</param>
    public static void SetLogger(ILogger? value)
    {
        Volatile.Write(ref logger, value);
    }

    /// <summary>
    /// Emits an <c>otel.sdk.component.shutdown</c> event for a provider.
    /// Uses <c>error.type</c> (absent on success, present on failure) per the
    /// canonical semconv pattern.
    /// </summary>
    /// <param name="componentType">Provider type (e.g. <c>logger_provider</c>).</param>
    /// <param name="componentName">Instance name (e.g. <c>logger_provider/0</c>).</param>
    /// <param name="success">Whether all child components shut down successfully.</param>
    /// <param name="timeoutMilliseconds">The configured shutdown timeout.</param>
    /// <param name="elapsedMilliseconds">Elapsed time in milliseconds (for timeout classification).</param>
    /// <param name="durationSeconds">Elapsed time in seconds (for the duration attribute).</param>
    public static void EmitProviderShutdownEvent(
        string componentType,
        string componentName,
        bool success,
        int timeoutMilliseconds,
        double elapsedMilliseconds,
        double durationSeconds)
    {
        var sink = Volatile.Read(ref logger);
        if (sink is null)
        {
            return;
        }

        var level = success ? LogLevel.Information : LogLevel.Warning;
        var eventId = new EventId(0, "otel.sdk.component.shutdown");

        var state = new List<KeyValuePair<string, object?>>(4)
        {
            new("otel.component.type", componentType),
            new("otel.component.name", componentName),
            new("otel.component.shutdown.duration", durationSeconds),
        };

        if (!success)
        {
            // Classify: timeout takes precedence over generic failure
            var errorType = (timeoutMilliseconds != Timeout.Infinite
                && timeoutMilliseconds > 0
                && elapsedMilliseconds >= timeoutMilliseconds)
                ? "timeout"
                : "failed";
            state.Add(new("error.type", errorType));
        }

        sink.Log(
            level,
            eventId,
            state,
            exception: null,
            formatter: static (_, _) => string.Empty);
    }
}
