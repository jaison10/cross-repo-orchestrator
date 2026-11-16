using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace OrchestratorGate;

/// <summary>
/// Starts the server so it outlives the shell that launched it (Claude Code's Bash tool may close its job when a command ends).
/// </summary>
static class Proc
{
    public static void StartDetached(string exe, string args)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsDetached(exe, args);
            return;
        }
        Process.Start(new ProcessStartInfo("/bin/sh")
        {
            ArgumentList = { "-c", "setsid nohup \"$0\" $1 >/dev/null 2>&1 &", exe, args },
            UseShellExecute = false,
        })?.Dispose();
    }

    const uint DetachedProcess = 0x00000008;
    const uint CreateNewProcessGroup = 0x00000200;
    const uint CreateBreakawayFromJob = 0x01000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInformation
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CreateProcessW(string? app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags,
        IntPtr env, string? cwd, ref StartupInfo si, out ProcessInformation pi);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);

    // Breakaway keeps the server alive when the calling shell's job closes; jobs that forbid it fail the start, so retry without.
    static void WindowsDetached(string exe, string args)
    {
        var err = 0;
        foreach (var extra in new[] { CreateBreakawayFromJob, 0u })
        {
            var si = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
            var cmd = new StringBuilder($"\"{exe}\" {args}");
            if (CreateProcessW(null, cmd, IntPtr.Zero, IntPtr.Zero, false, DetachedProcess | CreateNewProcessGroup | extra,
                    IntPtr.Zero, null, ref si, out var pi))
            {
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                return;
            }
            err = Marshal.GetLastWin32Error();
        }
        throw new GateException(500, $"could not start the server (win32 error {err})");
    }
}
