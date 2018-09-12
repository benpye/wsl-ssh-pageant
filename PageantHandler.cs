using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WslSSHPageant
{
    public class PageantException : Exception
    {
        internal PageantException(string message) : base(message)
        {
        }
    }

    static class PageantHandler
    {
        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        static extern IntPtr FindWindow(string lpWindowClass, string lpWindowName);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateFileMapping(
            IntPtr hFile,
            IntPtr lpFileMappingAttributes,
            FileMapProtection flProtect,
            uint dwMaximumSizeHigh,
            uint dwMaximumSizeLow,
            string lpName);

        [Flags]
        public enum FileMapProtection : uint
        {
            PageReadonly = 0x02,
            PageReadWrite = 0x04,
            PageWriteCopy = 0x08,
            PageExecuteRead = 0x20,
            PageExecuteReadWrite = 0x40,
            SectionCommit = 0x8000000,
            SectionImage = 0x1000000,
            SectionNoCache = 0x10000000,
            SectionReserve = 0x4000000,
        }

        static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr MapViewOfFile(
            IntPtr hFileMappingObject,
            FileMapAccess dwDesiredAccess,
            UInt32 dwFileOffsetHigh,
            UInt32 dwFileOffsetLow,
            UIntPtr dwNumberOfBytesToMap);

        [Flags]
        public enum FileMapAccess : uint
        {
            FileMapCopy = 0x0001,
            FileMapWrite = 0x0002,
            FileMapRead = 0x0004,
            FileMapAllAccess = 0x001f,
            FileMapExecute = 0x0020,
        }

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct COPYDATASTRUCT
        {
            public IntPtr dwData;    // Any value the sender chooses.  Perhaps its main window handle?
            public int cbData;       // The count of bytes in the message.
            public IntPtr lpData;    // The address of the message.
        }

        const int WM_COPYDATA = 0x004A;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hHandle);

        internal static ArraySegment<byte> AGENT_EMPTY_RESPONSE = new ArraySegment<byte>(new byte[] { 0x00, 0x00, 0x00, 0x05, 0x0c, 0x00, 0x00, 0x00, 0x00 });
        internal const uint AGENT_MAX_MSGLEN = 8192;
        static readonly IntPtr AGENT_COPYDATA_ID = new IntPtr(0x804e50ba);

        // Send ssh-agent query to Pageant, ssh-agent and Pageant use same messages
        internal static byte[] Query(ArraySegment<byte> buf)
        {
            var hwnd = FindWindow("Pageant", "Pageant");
            if (hwnd == IntPtr.Zero)
            {
                throw new PageantException("HWND not found");
            }

            var mapName = String.Format("PageantRequest{0:x8}", GetCurrentThreadId());
            var fileMap = CreateFileMapping(INVALID_HANDLE_VALUE, IntPtr.Zero, FileMapProtection.PageReadWrite, 0, AGENT_MAX_MSGLEN, mapName);
            var sharedMemory = MapViewOfFile(fileMap, FileMapAccess.FileMapWrite, 0, 0, UIntPtr.Zero);
            Marshal.Copy(buf.Array, buf.Offset, sharedMemory, buf.Count);

            var mapNameBytes = Encoding.UTF8.GetBytes(mapName + '\0');
            var gch = GCHandle.Alloc(mapNameBytes);

            var cds = new COPYDATASTRUCT();
            cds.dwData = AGENT_COPYDATA_ID;
            cds.cbData = mapNameBytes.Length;
            cds.lpData = Marshal.UnsafeAddrOfPinnedArrayElement(mapNameBytes, 0);

            var data = Marshal.AllocHGlobal(Marshal.SizeOf(cds));
            Marshal.StructureToPtr(cds, data, false);
            var rcode = SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, data);
            if (rcode == IntPtr.Zero)
            {
                throw new PageantException("WM_COPYDATA failed");
            }

            var len = (Marshal.ReadByte(sharedMemory, 0) << 24) |
                      (Marshal.ReadByte(sharedMemory, 1) << 16) |
                      (Marshal.ReadByte(sharedMemory, 2) << 8) |
                      (Marshal.ReadByte(sharedMemory, 3));

            len += 4;
            if (len > AGENT_MAX_MSGLEN)
            {
                throw new PageantException("Message too long");
            }

            var ret = new byte[len];
            Marshal.Copy(sharedMemory, ret, 0, len);

            UnmapViewOfFile(sharedMemory);
            CloseHandle(fileMap);

            return ret;
        }
    }
}
