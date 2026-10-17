using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TrustTunnelGui
{
    // Shared by the app (stopping its client) and the installer (stopping a client left running by an old version).
    static class ConsoleCtrl
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);

        [DllImport("kernel32.dll")]
        static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);

        static readonly object Sync = new object();

        // Sends Ctrl+C to a console process started with CreateNoWindow (it owns a hidden console).
        public static bool SendCtrlC(int pid)
        {
            lock (Sync)
            {
                FreeConsole();
                if (!AttachConsole((uint)pid)) return false;
                SetConsoleCtrlHandler(IntPtr.Zero, true); // do not terminate ourselves
                bool ok = GenerateConsoleCtrlEvent(0, 0);
                FreeConsole();
                // The event is delivered asynchronously; keep ignoring it for a moment.
                Thread.Sleep(300);
                SetConsoleCtrlHandler(IntPtr.Zero, false);
                return ok;
            }
        }
    }
}
