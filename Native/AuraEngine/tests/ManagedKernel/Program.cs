using System;
using System.Collections.Generic;

namespace AuraEngine.KernelTests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Opt-in randomized soak: `managed_kernel_tests.dll soak [seconds] [seed] [episode]`.
            if (args.Length > 0 && args[0] == "soak")
            {
                var rest = new string[args.Length - 1];
                Array.Copy(args, 1, rest, 0, rest.Length);
                return SoakRunner.Run(rest);
            }

            var runner = new KernelTestSuite();
            var failures = runner.RunAll();
            Console.WriteLine();
            Console.WriteLine(failures.Count == 0
                ? $"KERNEL_TESTS_OK ({runner.Passed} passed)"
                : $"KERNEL_TESTS_FAIL ({runner.Passed} passed, {failures.Count} failed)");
            foreach (var failure in failures)
                Console.WriteLine("  FAIL " + failure);
            return failures.Count == 0 ? 0 : 1;
        }
    }
}
