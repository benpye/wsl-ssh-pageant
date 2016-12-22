using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    [DllImport("user32.dll", EntryPoint="FindWindow", SetLastError = true)]
    static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

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

    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool CloseHandle(IntPtr hHandle);

    const uint AGENT_MAX_MSGLEN = 8192;
    static readonly IntPtr AGENT_COPYDATA_ID = new IntPtr(0x804e50ba);

    // Send ssh-agent query to Pageant, ssh-agent and Pageant use same messages
    static byte[] Query(byte[] buf)
    {
        var hwnd = FindWindowByCaption(IntPtr.Zero, "Pageant");

        var mapName = String.Format("PageantRequest{0:x8}", GetCurrentThreadId());

        var fileMap = CreateFileMapping(INVALID_HANDLE_VALUE, IntPtr.Zero, FileMapProtection.PageReadWrite, 0, AGENT_MAX_MSGLEN, mapName);

        var sharedMemory = MapViewOfFile(fileMap, FileMapAccess.FileMapWrite, 0, 0, UIntPtr.Zero);

        Marshal.Copy(buf, 0, sharedMemory, buf.Length);

        var cds = new COPYDATASTRUCT();
        cds.dwData = AGENT_COPYDATA_ID;
        cds.cbData = mapName.Length + 1;
        var foo = Encoding.UTF8.GetBytes(mapName);
        var bar = new byte[foo.Length + 1];
        foo.CopyTo(bar, 0);
        bar[bar.Length - 1] = 0;
        var gch = GCHandle.Alloc(bar);
        cds.lpData = Marshal.UnsafeAddrOfPinnedArrayElement(bar, 0);
        var data = Marshal.AllocHGlobal(Marshal.SizeOf(cds));
        Marshal.StructureToPtr(cds, data, false);
        var rcode = SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, data);

        var len = (Marshal.ReadByte(sharedMemory, 0) << 24) |
                  (Marshal.ReadByte(sharedMemory, 1) << 16) |
                  (Marshal.ReadByte(sharedMemory, 2) << 8 ) |
                  (Marshal.ReadByte(sharedMemory, 3)      );
        var ret = new byte[len + 4];
        Marshal.Copy(sharedMemory, ret, 0, len + 4);

        UnmapViewOfFile(sharedMemory);
        CloseHandle(fileMap);

        return ret;
    }

    static void Main(string[] args)
    {
        var port = 13000;

        if(args.Length == 1)
            port = Int32.Parse(args[0]);
        else if(args.Length != 0)
        {
            Console.WriteLine("wsl-ssh-agent.exe <port: default 13000>");
            return;
        }

        var localAddr = IPAddress.Parse("127.0.0.1");

        var server = new TcpListener(localAddr, port);

        // Start listening for client requests.
        server.Start();

        Console.WriteLine($"Listening on 127.0.0.1:{port}");

        // Buffer for reading data
        var bytes = new Byte[AGENT_MAX_MSGLEN];

        // Enter the listening loop.
        while(true) 
        {
            var t = server.AcceptTcpClientAsync();
            t.Wait();

            TcpClient client = t.Result;

            // Get a stream object for reading and writing
            var stream = client.GetStream();

            int i;

            // Loop to receive all the data sent by the client.
            while((i = stream.Read(bytes, 0, bytes.Length))!=0) 
            {   
                var msg = Query(bytes);
                stream.Write(msg, 0, msg.Length);
            }

            client.Dispose();
        }
    }
}
