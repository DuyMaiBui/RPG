using System;
using System.Diagnostics;
using System.Threading;

namespace AuraEngine.KernelTests
{
    /* Detects a hang inside a native call: when no operation completes for HangSeconds it prints the active op
       log tail and terminates the process. */
    internal static class SoakWatchdog
    {
        private static long _lastBeat;
        private static volatile SoakEpisode _episode;
        private static ulong _seed;
        private static Thread _thread;

        public static void Start(ulong seed)
        {
            _seed = seed;
            Beat();
            _thread = new Thread(Watch) { IsBackground = true, Name = "soak-watchdog" };
            _thread.Start();
        }

        public static void Attach(SoakEpisode episode) => _episode = episode;

        public static void Beat() => Interlocked.Exchange(ref _lastBeat, Stopwatch.GetTimestamp());

        private static void Watch()
        {
            while (true)
            {
                Thread.Sleep(1000);
                var idle = (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastBeat)) / (double)Stopwatch.Frequency;
                if (idle < SoakSettings.HangSeconds)
                    continue;
                Console.WriteLine();
                Console.WriteLine("SOAK_HANG no operation completed for " + (int)idle + " s. seed=" + _seed);
                var episode = _episode;
                if (episode != null)
                {
                    Console.WriteLine("episode seed=" + episode.Seed + " (rerun: soak 0 " + _seed + " " + episode.Index + ")");
                    Console.WriteLine("last operations (the final line is the call that hung):");
                    Console.WriteLine(episode.Log.Tail(40));
                }

                Console.Out.Flush();
                new Thread(() => { Thread.Sleep(5000); Process.GetCurrentProcess().Kill(); }) { IsBackground = true }.Start();
                Environment.Exit(4);
            }
        }
    }
}
