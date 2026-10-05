using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using AuraEngine.Physics.Native;

namespace AuraEngine.KernelTests
{
    /* Entry point of `managed_kernel_tests.dll soak [seconds] [seed] [episode | first-last]`.
       The run is a series of episodes; each episode is replayed twice and the two digests must match.
       With an explicit episode index (or an inclusive range) only those episodes run, which reproduces a reported failure;
       state carried across episodes inside the process matters, so a divergence may need the whole range. */
    internal static class SoakRunner
    {
        public static int Run(string[] args)
        {
            var seconds = args.Length > 0 ? double.Parse(args[0], CultureInfo.InvariantCulture) : 120.0;
            var seed = args.Length > 1 ? ulong.Parse(args[1], CultureInfo.InvariantCulture) : (ulong)(Environment.TickCount64 & 0xFFFFFFF) + 1UL;
            var onlyEpisode = -1;
            var lastEpisode = -1;
            if (args.Length > 2)
            {
                var parts = args[2].Split('-');
                onlyEpisode = int.Parse(parts[0], CultureInfo.InvariantCulture);
                lastEpisode = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : onlyEpisode;
            }

            ReadEnvironment();
            var logPath = Environment.GetEnvironmentVariable("AURA_SOAK_LOG");
            if (logPath == null)
                logPath = "soak_current.log";

            Console.WriteLine("SOAK start seconds=" + seconds + " seed=" + seed + " opsPerEpisode=" + SoakSettings.OpsPerEpisode
                + " evilRate=" + SoakSettings.EvilRate.ToString(CultureInfo.InvariantCulture) + " abi=" + NativeMethods.Aura_AbiVersion()
                + " log=" + logPath);
            SoakWatchdog.Start(seed);
            var clock = Stopwatch.StartNew();
            var episodes = 0;
            var ops = 0L;
            var index = onlyEpisode >= 0 ? onlyEpisode : 0;
            while (true)
            {
                var episodeSeed = seed ^ ((ulong)index * 0x9E3779B97F4A7C15UL);
                var rng = episodeSeed;
                episodeSeed = SoakRng.Mix(ref rng);
                var failure = RunEpisode(seed, episodeSeed, index, logPath, out var opCount);
                if (failure != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("SOAK_FAIL " + failure);
                    Console.WriteLine("reproduce: dotnet managed_kernel_tests.dll soak 0 " + seed + " " + index);
                    return 1;
                }

                episodes++;
                ops += opCount;
                index++;
                if (onlyEpisode >= 0 ? index > lastEpisode : clock.Elapsed.TotalSeconds >= seconds)
                    break;
            }

            Console.WriteLine("SOAK_OK seed=" + seed + " episodes=" + episodes + " operations(one pass)=" + ops + " elapsed=" + clock.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s");
            Console.WriteLine("result codes: " + string.Join(" ", Enumerable.Range(0, 32).Where(i => SoakEpisode.CodeCounts[i] > 0).Select(i => i + "=" + SoakEpisode.CodeCounts[i])));
            foreach (var finding in SoakSettings.Findings)
                Console.WriteLine("FINDING " + finding);
            Console.WriteLine("known box2d overlap overruns tolerated (per pass): " + SoakSettings.KnownOverruns);
            Console.WriteLine("calls on broken joints rejected as INVALID_HANDLE / HasJoint=false (both passes): " + SoakSettings.BrokenJointRejections);
            Console.WriteLine("operations: " + string.Join(" ", SoakEpisode.OpCounts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value)));
            return 0;
        }

        private static void ReadEnvironment()
        {
            var evil = Environment.GetEnvironmentVariable("AURA_SOAK_EVIL");
            if (evil != null)
                SoakSettings.EvilRate = float.Parse(evil, CultureInfo.InvariantCulture);
            var opsText = Environment.GetEnvironmentVariable("AURA_SOAK_OPS");
            if (opsText != null)
                SoakSettings.OpsPerEpisode = int.Parse(opsText, CultureInfo.InvariantCulture);
            var audit = Environment.GetEnvironmentVariable("AURA_SOAK_AUDIT_EVERY");
            if (audit != null)
                SoakSettings.AuditEvery = int.Parse(audit, CultureInfo.InvariantCulture);
            SoakSettings.Trace = Environment.GetEnvironmentVariable("AURA_SOAK_TRACE") == "1";
            SoakSettings.ContinueOnCorruption = Environment.GetEnvironmentVariable("AURA_SOAK_CONTINUE") == "1";
            SoakSettings.TolerateKnown = Environment.GetEnvironmentVariable("AURA_SOAK_KNOWN") == "1";
            var hang = Environment.GetEnvironmentVariable("AURA_SOAK_HANG_SECONDS");
            if (hang != null)
                SoakSettings.HangSeconds = int.Parse(hang, CultureInfo.InvariantCulture);
        }

        /* Runs one episode twice. Returns a failure report, or null when both passes agree. */
        private static string RunEpisode(ulong runSeed, ulong episodeSeed, int index, string logPath, out int opCount)
        {
            opCount = 0;
            SoakEpisode first = null;
            SoakEpisode second = null;
            try
            {
                first = new SoakEpisode(episodeSeed, index, logPath);
                try
                {
                    first.Run(SoakSettings.OpsPerEpisode);
                    first.Finish();
                }
                catch (Exception exception)
                {
                    return Report("pass A", runSeed, episodeSeed, index, exception, first);
                }

                ((IDisposable)first.Log).Dispose();
                opCount = first.OpIndex;
                second = new SoakEpisode(episodeSeed, index, logPath);
                try
                {
                    second.Run(SoakSettings.OpsPerEpisode);
                    second.Finish();
                }
                catch (Exception exception)
                {
                    return Report("pass B (replay)", runSeed, episodeSeed, index, exception, second);
                }

                var difference = Compare(runSeed, episodeSeed, index, first, second);
                if (difference != null && SoakSettings.ContinueOnCorruption)
                {
                    var firstLine = difference.Substring(0, Math.Min(difference.IndexOf('\n') < 0 ? difference.Length : difference.IndexOf('\n'), 200));
                    SoakSettings.Record("det" + index, firstLine + " episode " + index + " (needs the preceding episodes: soak 0 " + runSeed + " 0-" + index + ")");
                    return null;
                }

                return difference;
            }
            finally
            {
                (first as IDisposable)?.Dispose();
                (second as IDisposable)?.Dispose();
            }
        }

        private static string Report(string pass, ulong runSeed, ulong episodeSeed, int index, Exception exception, SoakEpisode episode)
        {
            var kind = exception is SoakFailureException ? "invariant violated" : "unexpected managed exception " + exception.GetType().Name;
            return kind + " in " + pass + ": " + exception.Message + "\nrun seed=" + runSeed + " episode=" + index + " episode seed=" + episodeSeed
                + " op=" + episode.OpIndex + "\nlast operations:\n" + episode.Log.Tail(40)
                + (exception is SoakFailureException ? string.Empty : "\n" + exception);
        }

        private static string TraceDiff(SoakEpisode a, SoakEpisode b)
        {
            var shared = Math.Min(a.Trace.Count, b.Trace.Count);
            for (var i = 0; i < shared; i++)
            {
                if (a.Trace[i] != b.Trace[i])
                    return "\nfirst differing audited body state:\n  A: " + a.Trace[i] + "\n  B: " + b.Trace[i] + "\n";
            }

            return a.Trace.Count == b.Trace.Count ? string.Empty : "\naudited state counts differ: " + a.Trace.Count + " vs " + b.Trace.Count + "\n";
        }

        private static string Compare(ulong runSeed, ulong episodeSeed, int index, SoakEpisode a, SoakEpisode b)
        {
            if (a.Digest == b.Digest && a.OpDigests.Count == b.OpDigests.Count)
                return null;
            var first = 0;
            var shared = Math.Min(a.OpDigests.Count, b.OpDigests.Count);
            while (first < shared && a.OpDigests[first] == b.OpDigests[first])
                first++;
            return "NON-DETERMINISTIC REPLAY: episode digests differ (" + a.Digest + " vs " + b.Digest + "), ops " + a.OpDigests.Count + " vs " + b.OpDigests.Count
                + ".\nrun seed=" + runSeed + " episode=" + index + " episode seed=" + episodeSeed + " first diverging operation index=" + (first + 1)
                + " (a divergence with no operation index differs only in the state audited after it)"
                + "\n--- pass A around the divergence:\n" + a.Log.Window(first, 6)
                + "--- pass B around the divergence:\n" + b.Log.Window(first, 6) + TraceDiff(a, b);
        }
    }
}
