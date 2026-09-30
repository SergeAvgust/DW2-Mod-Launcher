using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace DW2ModLauncher.Core.Services.Publishing.Interop
{
    /// <summary>
    /// Reads the on-screen text of a CHILD process's own console window - one it allocated for
    /// itself via AllocConsole (as DW2 does for "--ugc-publish"; see docs/workshop-publish.md) -
    /// by attaching to that console with AttachConsole and reading its screen buffer directly.
    ///
    /// This is NOT a substitute for ordinary stdout/stderr redirection: redirection only works
    /// when the child writes through the standard handles it was launched with, and a process
    /// that calls AllocConsole gets itself a brand new console with its own handles instead,
    /// bypassing anything the parent set up. There is no supported .NET API for "read some other
    /// process's console," so this reaches for the raw Win32 console functions instead - every
    /// bit of that unmanaged wizardry is confined to this one file, on purpose, so nothing outside
    /// it needs to know or care how the trick works.
    ///
    /// A process can only ever be attached to one console at a time, and attaching detaches from
    /// whatever console the caller had (a WinForms app normally has none, so this is safe here).
    /// AttachConsole fails until the target process actually calls AllocConsole itself, which does
    /// not happen the instant it starts - callers should poll (see ModWorkshopIdWatcher) rather
    /// than attach exactly once. Reading, on the other hand, is NOT time-sensitive the same way:
    /// the screen buffer keeps whatever was printed to it (scrollback) regardless of when you
    /// attach, right up until the console itself is destroyed - which for "--ugc-publish"
    /// specifically only happens once a person dismisses its "Press any key to exit." prompt. So
    /// there is no need to race the engine to attach before it prints the workshop id; the only
    /// real deadline is reading it before that console goes away.
    /// </summary>
    internal static class ChildConsoleCapture
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareReadWrite = 1 | 2;
        private const uint OpenExisting = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct Coord
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SmallRect
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ConsoleScreenBufferInfo
        {
            public Coord dwSize;
            public Coord dwCursorPosition;
            public ushort wAttributes;
            public SmallRect srWindow;
            public Coord dwMaximumWindowSize;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        // NOT GetStdHandle(STD_OUTPUT_HANDLE): confirmed by hand that it returns a handle-shaped
        // value here that GetConsoleScreenBufferInfo then rejects with ERROR_INVALID_HANDLE - a
        // process that never had a console of its own at startup (a WinForms app, same as this
        // one) seems to keep a stale/unusable entry in that slot that AttachConsole doesn't
        // refresh. Opening "CONOUT$" directly is the documented, actually-reliable way to get a
        // live handle to whichever console the process is currently attached to.
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out ConsoleScreenBufferInfo lpConsoleScreenBufferInfo);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ReadConsoleOutputCharacterW(IntPtr hConsoleOutput, [Out] char[] lpCharacter, uint nLength, Coord dwReadCoord, out uint lpNumberOfCharsRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WmChar = 0x0102;

        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        /// <summary>
        /// Attaches to <paramref name="processId"/>'s own console (retrying up to
        /// <paramref name="attachTimeout"/>, since it may not have allocated one yet) and returns
        /// every row of its screen buffer up to the current cursor line, joined with newlines and
        /// right-trimmed. Returns null if no console ever appeared within the timeout, or if
        /// reading it failed for any reason - callers should treat that as "try again later," not
        /// as a hard error.
        /// </summary>
        public static string TryReadScreenText(int processId, TimeSpan attachTimeout)
        {
            DateTime deadline = DateTime.UtcNow + attachTimeout;
            bool attached = false;
            try
            {
                while (!attached && DateTime.UtcNow < deadline)
                {
                    attached = AttachConsole((uint)processId);
                    if (!attached) Thread.Sleep(100);
                }
                if (!attached) return null;

                IntPtr conout = CreateFileW("CONOUT$", GenericRead | GenericWrite, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (conout == IntPtr.Zero || conout == InvalidHandleValue) return null;
                try
                {
                    ConsoleScreenBufferInfo info;
                    if (!GetConsoleScreenBufferInfo(conout, out info)) return null;

                    int width = info.dwSize.X;
                    int rowCount = info.dwCursorPosition.Y + 1;
                    if (width <= 0 || rowCount <= 0) return null;

                    System.Text.StringBuilder text = new System.Text.StringBuilder();
                    char[] row = new char[width];
                    for (int y = 0; y < rowCount; y++)
                    {
                        Coord readFrom = new Coord { X = 0, Y = (short)y };
                        uint charsRead;
                        if (!ReadConsoleOutputCharacterW(conout, row, (uint)width, readFrom, out charsRead)) continue;
                        text.AppendLine(new string(row, 0, (int)charsRead).TrimEnd());
                    }
                    return text.ToString();
                }
                finally
                {
                    CloseHandle(conout);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (attached) { try { FreeConsole(); } catch { } }
            }
        }

        /// <summary>
        /// Best-effort: attaches to <paramref name="processId"/>'s console (if it still has one)
        /// and posts it a WM_CHAR, the same way pressing a key on its window would, to satisfy its
        /// "Press any key to exit." prompt. Used once the workshop id has already been read, so the
        /// process can run its own normal exit path (which may include opening a browser to the
        /// published item - untested, but worth not foreclosing on) instead of being killed out
        /// from under it. Callers should still fall back to killing the process if it doesn't exit
        /// shortly after this - dismissal can silently fail (no console left to attach to, the
        /// message getting ignored, etc.) and this method deliberately never throws for that.
        /// </summary>
        public static void TryDismiss(int processId)
        {
            bool attached = false;
            try
            {
                attached = AttachConsole((uint)processId);
                if (!attached) return;
                IntPtr hwnd = GetConsoleWindow();
                if (hwnd != IntPtr.Zero) PostMessage(hwnd, WmChar, new IntPtr(' '), IntPtr.Zero);
            }
            catch { }
            finally
            {
                if (attached) { try { FreeConsole(); } catch { } }
            }
        }
    }
}
