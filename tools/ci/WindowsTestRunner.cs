using System;

namespace Frociart
{
    internal static class CiTestRunner
    {
        [STAThread]
        public static int Main()
        {
            int failures = 0;
            foreach (Func<int> test in new Func<int>[] {
                Tests.EngineTests.Main, Tests.CapabilityTests.Main, Tests.CloneTests.Main })
            {
                try { if (test() != 0) failures++; }
                catch (Exception error) { Console.Error.WriteLine(error); failures++; }
            }
            Console.WriteLine("Windows native logic suites: " + (failures == 0 ? "PASS" : "FAIL"));
            return failures == 0 ? 0 : 1;
        }
    }
}
