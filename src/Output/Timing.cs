using System.Diagnostics;

namespace MED.Output;

public sealed class TimingScope : IDisposable
{
    private readonly Stopwatch _sw;
    private readonly Action<TimeSpan> _onDone;
    private bool _disposed;

    public TimingScope(Action<TimeSpan> onDone)
    {
        _onDone = onDone ?? throw new ArgumentNullException(nameof(onDone));
        _sw = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _sw.Stop();
        _onDone(_sw.Elapsed);
    }
}

public static class Timing
{
    public static TimeSpan Measure(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));

        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.Elapsed;
    }
}
