using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MIR.Test.GameTests;

/// <summary>
/// An <see cref="ILogger"/> for a test game (<see cref="TestGameUtil.Create"/>) that keeps every message. Walgelijk's static
/// <c>Logger</c> writes to <c>Game.Main.Logger</c>, so while the test game is <c>Game.Main</c> this sees everything the game logs.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    public readonly ConcurrentQueue<(LogLevel Level, string Message)> Entries = new();

    public IEnumerable<string> AtLeast(LogLevel level) => Entries.Where(e => e.Level >= level).Select(e => $"[{e.Level}] {e.Message}");

    public IEnumerable<string> Errors => AtLeast(LogLevel.Error);

    public IEnumerable<string> Warnings => Entries.Where(e => e.Level == LogLevel.Warning).Select(e => e.Message);

    public void Clear() => Entries.Clear();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Enqueue((logLevel, formatter(state, exception) + (exception == null ? string.Empty : " " + exception)));
}
