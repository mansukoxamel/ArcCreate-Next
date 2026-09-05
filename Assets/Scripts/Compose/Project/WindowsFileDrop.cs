#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ArcCreate.Compose.Project
{
    internal sealed class WindowsFileDrop : IDisposable
    {
        private const int WindowProcedureIndex = -4;
        private const uint DropFilesMessage = 0x0233;
        private const uint QueryFileCount = 0xffffffff;

        private readonly IntPtr window;
        private readonly IntPtr previousWindowProcedure;
        private readonly WindowProcedure windowProcedure;
        private readonly Action<IReadOnlyList<string>> onFilesDropped;
        private bool disposed;

        public IntPtr Window => window;

        public WindowsFileDrop(Action<IReadOnlyList<string>> onFilesDropped)
        {
            this.onFilesDropped = onFilesDropped;
            window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (window == IntPtr.Zero)
            {
                window = GetActiveWindow();
            }

            if (window == IntPtr.Zero)
            {
                throw new InvalidOperationException("ArcCreate Next window was not available for file drop registration.");
            }

            windowProcedure = ProcessWindowMessage;
            IntPtr windowProcedurePointer = Marshal.GetFunctionPointerForDelegate(windowProcedure);
            previousWindowProcedure = SetWindowLongPtr(window, WindowProcedureIndex, windowProcedurePointer);
            if (previousWindowProcedure == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            DragAcceptFiles(window, true);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            DragAcceptFiles(window, false);
            SetWindowLongPtr(window, WindowProcedureIndex, previousWindowProcedure);
            disposed = true;
        }

        private IntPtr ProcessWindowMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            if (message == DropFilesMessage)
            {
                try
                {
                    uint fileCount = DragQueryFile(wParam, QueryFileCount, null, 0);
                    List<string> paths = new List<string>((int)fileCount);
                    for (uint i = 0; i < fileCount; i++)
                    {
                        uint length = DragQueryFile(wParam, i, null, 0);
                        StringBuilder path = new StringBuilder((int)length + 1);
                        DragQueryFile(wParam, i, path, (uint)path.Capacity);
                        paths.Add(path.ToString());
                    }

                    onFilesDropped(paths);
                }
                finally
                {
                    DragFinish(wParam);
                }

                return IntPtr.Zero;
            }

            return CallWindowProc(previousWindowProcedure, hwnd, message, wParam, lParam);
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW", CharSet = CharSet.Unicode)]
        private static extern IntPtr CallWindowProc(
            IntPtr previousWindowProcedure,
            IntPtr hwnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("shell32.dll")]
        private static extern void DragAcceptFiles(IntPtr hwnd, bool accept);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFile(
            IntPtr dropHandle,
            uint fileIndex,
            StringBuilder fileName,
            uint characterCount);

        [DllImport("shell32.dll")]
        private static extern void DragFinish(IntPtr dropHandle);
    }
}
#endif
