using System;
using System.Collections.Generic;

namespace AuraEngine.KernelTests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
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
