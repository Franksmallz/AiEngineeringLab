using System.Diagnostics;

namespace RiskInference.Benchmark;

public sealed record ResourceUsage(
    // Share of the whole machine (all processors) used by the server process.
    double AverageCpuPercentOfMachine,
    // Share of one processor. 100 means one full processor.
    double AverageCpuPercentOfOneCore,
    double PeakWorkingSetMb,
    double PeakPrivateMb);

// Samples the CPU and memory of another process while a level runs.
public sealed class ProcessSampler : IDisposable
{
    private readonly Process _process;
    private readonly Thread _thread;
    private volatile bool _stop;
    private TimeSpan _cpuStart;
    private long _wallStart;
    private double _peakWorkingSet;
    private double _peakPrivate;

    public ProcessSampler(int processId)
    {
        _process = Process.GetProcessById(processId);
        _thread = new Thread(Loop) { IsBackground = true };
    }

    public void Start()
    {
        _process.Refresh();
        _cpuStart = _process.TotalProcessorTime;
        _wallStart = Stopwatch.GetTimestamp();
        _thread.Start();
    }

    public ResourceUsage Stop()
    {
        _stop = true;
        _thread.Join();
        _process.Refresh();
        var cpu = (_process.TotalProcessorTime - _cpuStart).TotalSeconds;
        var wall = (Stopwatch.GetTimestamp() - _wallStart) / (double)Stopwatch.Frequency;
        var oneCore = wall <= 0 ? 0 : cpu / wall * 100.0;
        return new ResourceUsage(
            Math.Round(oneCore / Environment.ProcessorCount, 1),
            Math.Round(oneCore, 1),
            Math.Round(_peakWorkingSet, 1),
            Math.Round(_peakPrivate, 1));
    }

    private void Loop()
    {
        while (!_stop)
        {
            _process.Refresh();
            _peakWorkingSet = Math.Max(_peakWorkingSet, _process.WorkingSet64 / 1048576.0);
            _peakPrivate = Math.Max(_peakPrivate, _process.PrivateMemorySize64 / 1048576.0);
            Thread.Sleep(250);
        }
    }

    public void Dispose() => _process.Dispose();
}
