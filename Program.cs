using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
    static byte[] Query(byte[] buf, int buflen)
    {
        var hwnd = FindWindowByCaption(IntPtr.Zero, "Pageant");

        var mapName = String.Format("PageantRequest{0:x8}", GetCurrentThreadId());

        var fileMap = CreateFileMapping(INVALID_HANDLE_VALUE, IntPtr.Zero, FileMapProtection.PageReadWrite, 0, AGENT_MAX_MSGLEN, mapName);

        var sharedMemory = MapViewOfFile(fileMap, FileMapAccess.FileMapWrite, 0, 0, UIntPtr.Zero);

        Marshal.Copy(buf, 0, sharedMemory, buflen);

        var cds = new COPYDATASTRUCT();
        cds.dwData = AGENT_COPYDATA_ID;
        cds.cbData = mapName.Length + 1;
        var mapNameBytes = Encoding.UTF8.GetBytes(mapName);
        var mapNameBytesZero = new byte[mapNameBytes.Length + 1];
        mapNameBytes.CopyTo(mapNameBytesZero, 0);
        mapNameBytesZero[mapNameBytesZero.Length - 1] = 0;
        var gch = GCHandle.Alloc(mapNameBytesZero);
        cds.lpData = Marshal.UnsafeAddrOfPinnedArrayElement(mapNameBytesZero, 0);
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

    private static void Callback(Task<Socket> t, object state)
    {
        var client = t.Result;
        client.ReceiveTimeout = 1000;
        client.SendTimeout = 1000;

        try
        {
            ServiceSocket(client);
        }
        catch (TimeoutException)
        {
            // Ignore timeouts, those should not explode our stuff
        }
        catch (SocketException e)
        {
            // Other socket errors can happen and shouldn't kill the app
            Console.Error.WriteLine(e);
        }

        client.Dispose();
    }

    private static bool ReadUntil(Socket client, byte[] buf, int buflen, int offset = 0)
    {
        int i;
        while ((i = client.Receive(buf, offset, buflen - offset, SocketFlags.None)) != 0)
        {
            offset += i;
            if (offset >= buflen)
            {
                return true;
            }
        }
        return false;
    }

    private static void ServiceSocket(Socket client)
    {
        var lenBytes = new byte[4]; // uint32

        while (true)
        {
            // Read length as uint32 (4 bytes)
            if (!ReadUntil(client, lenBytes, lenBytes.Length))
            {
                break;
            }
            var len = (lenBytes[0] << 24) |
                      (lenBytes[1] << 16) |
                      (lenBytes[2] << 8 ) |
                      (lenBytes[3]);

            // Read actual data and copy length to the start of it
            var bytes = new byte[4 + len];
            lenBytes.CopyTo(bytes, 0);
            if (!ReadUntil(client, bytes, bytes.Length, 4))
            {
                break;
            }

            var msg = Query(bytes, bytes.Length);
            client.Send(msg, 0, msg.Length, SocketFlags.None);
        }
    }

    static void Main(string[] args)
    {
        var socketPath = @".\ssh-agent.sock";

        if (args.Length == 1)
            socketPath = args[0];
        else if(args.Length != 0)
        {
            Console.WriteLine(@"wsl-ssh-agent.exe <path: .\ssh-agent.sock>");
            return;
        }

        File.Delete(socketPath);
        var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
        server.Bind(new UnixEndPoint(socketPath));
        server.Listen(5);

        Console.WriteLine(@"Listening on {0}", socketPath);

        // Enter the listening loop.
        while(true)
        {
            var t = server.AcceptAsync();
            t.ContinueWith(Callback, null);
            t.Wait();
        }
    }
}

