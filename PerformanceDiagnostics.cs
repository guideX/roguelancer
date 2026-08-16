using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Roguelancer;

/// <summary>
/// Bounded, opt-in frame accounting for Phase 3.6. This intentionally measures
/// the game-loop boundaries that are visible to the game rather than attempting
/// to infer costs from scene complexity.
/// </summary>
public sealed class PerformanceDiagnostics
{
    public readonly struct SectionScope : IDisposable
    {
        private readonly PerformanceDiagnostics? _owner;
        private readonly string _name;
        private readonly long _start;

        internal SectionScope(PerformanceDiagnostics owner, string name)
        {
            _owner = owner;
            _name = name;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_owner is null) return;
            _owner.RecordSection(_name, Stopwatch.GetTimestamp() - _start);
        }
    }

    private enum Mode
    {
        Spaceflight,
        Station
    }

    private sealed class ModeStats
    {
        public int Frames;
        public int UpdateCalls;
        public int RunningSlowlyUpdates;
        public long WallTicks;
        public long UpdateTicks;
        public long DrawTicks;
        public long PresentTicks;
        public long UpdateAllocatedBytes;
        public long DrawAllocatedBytes;
        public long MaxUpdateAllocatedBytes;
        public long MaxDrawAllocatedBytes;
        public double LastElapsedGameMs;
        public double LastTotalGameSeconds;
        public readonly Dictionary<string, SectionStats> Sections = new(StringComparer.Ordinal);
        public readonly Dictionary<string, CounterStats> Counters = new(StringComparer.Ordinal);
    }

    private sealed class SectionStats
    {
        public int Count;
        public long TotalTicks;
        public long MaxTicks;
    }

    private sealed class CounterStats
    {
        public int Samples;
        public long Total;
        public long Max;
    }

    private readonly bool _enabled;
    private readonly double _durationSeconds;
    private readonly long _startedAt;
    private readonly ModeStats _space = new();
    private readonly ModeStats _station = new();
    private Mode _activeMode;
    private Mode _drawMode;
    private long _updateStartedAt;
    private long _updateAllocationStart;
    private long _drawStartedAt;
    private long _drawAllocationStart;
    private long _presentStartedAt;
    private long _lastPresentCompletedAt;
    private int _updatesSinceDraw;
    private int _drawUpdateCount;
    private bool _drawBodyActive;
    private bool _presentActive;
    private int _initialGen0;
    private int _initialGen1;
    private int _initialGen2;
    private long _initialManagedBytes;
    private double _lastProgressSeconds = -1.0;
    private bool _summaryPrinted;
    private double _drawTotalGameSeconds;

    public PerformanceDiagnostics(bool enabled, double durationSeconds = 0.0)
    {
        _enabled = enabled;
        _durationSeconds = Math.Max(0.0, durationSeconds);
        _startedAt = Stopwatch.GetTimestamp();
        _initialGen0 = GC.CollectionCount(0);
        _initialGen1 = GC.CollectionCount(1);
        _initialGen2 = GC.CollectionCount(2);
        _initialManagedBytes = GC.GetTotalMemory(false);
    }

    public bool Enabled => _enabled;

    public SectionScope Measure(string name)
    {
        return new SectionScope(this, name);
    }

    public void BeginUpdate(GameTime gameTime, bool stationMode)
    {
        if (!_enabled) return;

        _activeMode = stationMode ? Mode.Station : Mode.Spaceflight;
        ModeStats stats = GetStats(_activeMode);
        stats.LastElapsedGameMs = gameTime.ElapsedGameTime.TotalMilliseconds;
        stats.LastTotalGameSeconds = gameTime.TotalGameTime.TotalSeconds;
        _updateStartedAt = Stopwatch.GetTimestamp();
        _updateAllocationStart = GC.GetAllocatedBytesForCurrentThread();
        _updatesSinceDraw++;
        if (gameTime.IsRunningSlowly) GetStats(_activeMode).RunningSlowlyUpdates++;
    }

    public void SetMode(bool stationMode)
    {
        if (_enabled) _activeMode = stationMode ? Mode.Station : Mode.Spaceflight;
    }

    public void EndUpdate(bool stationMode)
    {
        if (!_enabled || _updateStartedAt == 0) return;

        _activeMode = stationMode ? Mode.Station : Mode.Spaceflight;
        ModeStats stats = GetStats(_activeMode);
        stats.UpdateCalls++;
        long updateTicks = Stopwatch.GetTimestamp() - _updateStartedAt;
        long updateAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - _updateAllocationStart;
        stats.UpdateTicks += updateTicks;
        stats.UpdateAllocatedBytes += updateAllocatedBytes;
        stats.MaxUpdateAllocatedBytes = Math.Max(stats.MaxUpdateAllocatedBytes, updateAllocatedBytes);
        _updateStartedAt = 0;
    }

    public void BeginDraw(GameTime gameTime, bool stationMode)
    {
        if (!_enabled) return;

        _drawMode = stationMode ? Mode.Station : Mode.Spaceflight;
        _activeMode = _drawMode;
        _drawUpdateCount = _updatesSinceDraw;
        _updatesSinceDraw = 0;
        _drawStartedAt = Stopwatch.GetTimestamp();
        _drawAllocationStart = GC.GetAllocatedBytesForCurrentThread();
        _drawBodyActive = true;
        _drawTotalGameSeconds = gameTime.TotalGameTime.TotalSeconds;
    }

    public void EndDrawBody()
    {
        if (!_enabled || !_drawBodyActive) return;

        ModeStats stats = GetStats(_drawMode);
        stats.DrawTicks += Stopwatch.GetTimestamp() - _drawStartedAt;
        long drawAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - _drawAllocationStart;
        stats.DrawAllocatedBytes += drawAllocatedBytes;
        stats.MaxDrawAllocatedBytes = Math.Max(stats.MaxDrawAllocatedBytes, drawAllocatedBytes);
        _drawBodyActive = false;
    }

    public void BeginEndDraw()
    {
        if (!_enabled) return;

        _presentStartedAt = Stopwatch.GetTimestamp();
        _presentActive = true;
    }

    public void EndEndDraw()
    {
        if (!_enabled || !_presentActive) return;

        long now = Stopwatch.GetTimestamp();
        ModeStats stats = GetStats(_drawMode);
        stats.PresentTicks += now - _presentStartedAt;
        if (_lastPresentCompletedAt != 0)
        {
            stats.Frames++;
            stats.WallTicks += now - _lastPresentCompletedAt;
            RecordCounter(_drawMode, "update.calls.per.draw", _drawUpdateCount);
        }

        _lastPresentCompletedAt = now;
        _presentActive = false;
        MaybePrintProgress(_drawTotalGameSeconds);
    }

    public bool ShouldStop(double totalGameSeconds)
    {
        return _enabled && _durationSeconds > 0.0 && totalGameSeconds >= _durationSeconds;
    }

    public void LogGraphicsConfiguration(Game game, GraphicsDeviceManager graphics, string label)
    {
        if (!_enabled) return;

        PresentationParameters presentation = game.GraphicsDevice.PresentationParameters;
        Console.WriteLine(
            $"[PERF CONFIG {label}] fixed={game.IsFixedTimeStep} targetMs={game.TargetElapsedTime.TotalMilliseconds:0.###} " +
            $"vsync={graphics.SynchronizeWithVerticalRetrace} interval={presentation.PresentationInterval} " +
            $"backbuffer={presentation.BackBufferWidth}x{presentation.BackBufferHeight} " +
            $"fullscreen={graphics.IsFullScreen} profile={graphics.GraphicsProfile} msaa={presentation.MultiSampleCount} " +
            $"viewport={game.GraphicsDevice.Viewport.Width}x{game.GraphicsDevice.Viewport.Height} " +
            $"renderTargets={game.GraphicsDevice.GetRenderTargets().Length}");
    }

    public void AddCounter(string name, int amount = 1)
    {
        if (!_enabled) return;
        RecordCounter(_drawMode, name, amount);
    }

    public void AddCounter(string name, long amount)
    {
        if (!_enabled) return;
        RecordCounter(_drawMode, name, amount);
    }

    public void PrintSummary()
    {
        if (!_enabled || _summaryPrinted) return;
        _summaryPrinted = true;

        double elapsedSeconds = ToMilliseconds(Stopwatch.GetTimestamp() - _startedAt) / 1000.0;
        long managedDelta = GC.GetTotalMemory(false) - _initialManagedBytes;
        Console.WriteLine($"[PERF SUMMARY] elapsedSeconds={elapsedSeconds:0.###} managedBytesDelta={managedDelta} gcGen0={GC.CollectionCount(0) - _initialGen0} gcGen1={GC.CollectionCount(1) - _initialGen1} gcGen2={GC.CollectionCount(2) - _initialGen2}");
        PrintModeSummary("SPACEFLIGHT", _space);
        PrintModeSummary("STATION", _station);
    }

    private void MaybePrintProgress(double totalGameSeconds)
    {
        if (_durationSeconds <= 0.0 || totalGameSeconds - _lastProgressSeconds < 2.0) return;
        _lastProgressSeconds = totalGameSeconds;
        Console.WriteLine($"[PERF PROGRESS] gameSeconds={totalGameSeconds:0.###} spaceFrames={_space.Frames} stationFrames={_station.Frames} updatesSinceDraw={_updatesSinceDraw}");
    }

    private void RecordSection(string name, long elapsedTicks)
    {
        if (!_enabled) return;

        ModeStats stats = GetStats(_activeMode);
        if (!stats.Sections.TryGetValue(name, out SectionStats section))
        {
            section = new SectionStats();
            stats.Sections.Add(name, section);
        }

        section.Count++;
        section.TotalTicks += elapsedTicks;
        section.MaxTicks = Math.Max(section.MaxTicks, elapsedTicks);
    }

    private void RecordCounter(Mode mode, string name, long value)
    {
        ModeStats stats = GetStats(mode);
        if (!stats.Counters.TryGetValue(name, out CounterStats counter))
        {
            counter = new CounterStats();
            stats.Counters.Add(name, counter);
        }

        counter.Samples++;
        counter.Total += value;
        counter.Max = Math.Max(counter.Max, value);
    }

    private void PrintModeSummary(string label, ModeStats stats)
    {
        double frames = Math.Max(1, stats.Frames);
        double updates = Math.Max(1, stats.UpdateCalls);
        double wallMs = ToMilliseconds(stats.WallTicks) / frames;
        double updateMs = ToMilliseconds(stats.UpdateTicks) / frames;
        double drawMs = ToMilliseconds(stats.DrawTicks) / frames;
        double presentMs = ToMilliseconds(stats.PresentTicks) / frames;
        double unaccountedMs = wallMs - updateMs - drawMs - presentMs;
        double fps = wallMs > 0.0 ? 1000.0 / wallMs : 0.0;
        double updateAllocPerUpdate = stats.UpdateAllocatedBytes / updates;
        double drawAllocPerFrame = stats.DrawAllocatedBytes / frames;
        double callsPerDraw = GetCounterAverage(stats, "update.calls.per.draw");

        Console.WriteLine(
            $"[PERF {label}] frames={stats.Frames} fps={fps:0.##} wallMsPerFrame={wallMs:0.##} " +
            $"updateCalls={stats.UpdateCalls} updateCallsPerDraw={callsPerDraw:0.##} " +
            $"updateMsPerFrame={updateMs:0.##} drawBodyMsPerFrame={drawMs:0.##} presentMsPerFrame={presentMs:0.##} " +
            $"unaccountedMs={unaccountedMs:0.##} allocBytesPerUpdate={updateAllocPerUpdate:0} allocBytesPerDraw={drawAllocPerFrame:0} " +
            $"maxAllocUpdate={stats.MaxUpdateAllocatedBytes} maxAllocDraw={stats.MaxDrawAllocatedBytes} " +
            $"gameElapsedMs={stats.LastElapsedGameMs:0.###} lastGameSeconds={stats.LastTotalGameSeconds:0.###} " +
            $"runningSlowlyUpdates={stats.RunningSlowlyUpdates}");

        foreach (KeyValuePair<string, SectionStats> pair in stats.Sections)
        {
            SectionStats section = pair.Value;
            Console.WriteLine(
                $"[PERF {label} SECTION] {pair.Key} count={section.Count} avgMs={ToMilliseconds(section.TotalTicks) / Math.Max(1, section.Count):0.###} maxMs={ToMilliseconds(section.MaxTicks):0.###}");
        }

        foreach (KeyValuePair<string, CounterStats> pair in stats.Counters)
        {
            CounterStats counter = pair.Value;
            Console.WriteLine($"[PERF {label} COUNTER] {pair.Key} avg={counter.Total / (double)Math.Max(1, counter.Samples):0.###} max={counter.Max}");
        }
    }

    private static double GetCounterAverage(ModeStats stats, string name)
    {
        return stats.Counters.TryGetValue(name, out CounterStats counter)
            ? counter.Total / (double)Math.Max(1, counter.Samples)
            : 0.0;
    }

    private ModeStats GetStats(Mode mode) => mode == Mode.Station ? _station : _space;

    private static double ToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }
}
