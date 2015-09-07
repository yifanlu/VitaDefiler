using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace VitaDefiler.PSM
{
    public delegate Connection GetConnection(string serial);

    class VitaUSBConnection : Connection
    {
        private int handle;

        public VitaUSBConnection(string port)
        {
            this.handle = TransportFunctions.CreateFile(1, @"\\.\" + port);
            if (this.handle < 0)
            {
                throw new IOException("Error opening port for connection.");
            }
        }

        protected override void TransportClose()
        {
            TransportFunctions.CloseHandle(1, handle);
            this.handle = -1;
        }

        protected override unsafe int TransportReceive(byte[] buf, int buf_offset, int len)
        {
            while (this.handle != -1)
            {
                int recieve = TransportFunctions.GetReceiveSize(1, this.handle);
                uint read = 0;
                if (recieve >= len)
                {
                    fixed (byte* p_buf = buf)
                    {
                        if (TransportFunctions.ReadFile(1, this.handle, (IntPtr)(p_buf + buf_offset), (uint)len, out read) == 0)
                        {
                            throw new IOException("Cannot read from Vita.");
                        }
                        else
                        {
                            return (int)read;
                        }
                    }
                }
                //Thread.Sleep(30);
            }
            return 0;
        }

        protected override unsafe int TransportSend(byte[] buf, int buf_offset, int len)
        {
            int towrite = len;
            uint written = 0;
            fixed (byte* p_buf = buf)
            {
                while (towrite > 0)
                {
                    if (TransportFunctions.WriteFile(1, this.handle, (IntPtr)(p_buf + buf_offset), (uint)towrite, out written) == 0)
                    {
                        throw new IOException("Cannot write to Vita.");
                    }
                    towrite -= (int)written;
                }
            }
            return len;
        }

        protected override void TransportSetTimeouts(int send_timeout, int receive_timeout)
        {
            return;
        }
    }

    class TcpConnection : Connection
    {
        // Fields
        private Socket socket;

        // Methods
        internal TcpConnection(Socket socket)
        {
            this.socket = socket;
        }

        protected override void TransportClose()
        {
            this.socket.Close();
        }

        protected override int TransportReceive(byte[] buf, int buf_offset, int len)
        {
            return this.socket.Receive(buf, buf_offset, len, SocketFlags.None);
        }

        protected override int TransportSend(byte[] buf, int buf_offset, int len)
        {
            return this.socket.Send(buf, buf_offset, len, SocketFlags.None);
        }

        protected override void TransportSetTimeouts(int send_timeout, int receive_timeout)
        {
            this.socket.SendTimeout = send_timeout;
            this.socket.ReceiveTimeout = receive_timeout;
        }

        // Properties
        internal EndPoint EndPoint
        {
            get
            {
                return this.socket.RemoteEndPoint;
            }
        }
    }

    class ConnectionFinder
    {
        public static readonly int[] PLAYER_MULTICAST_PORTS = {54997, 34997, 57997, 58997};
        public const string PLAYER_MULTICAST_GROUP = "225.0.0.222";
        private static List<Socket> _multicastSockets = null;

        [StructLayout(LayoutKind.Sequential)]
        public struct PlayerInfo : IPlayer
        {
            public IPEndPoint m_IPEndPoint;
            public uint m_Flags;
            public uint m_Guid;
            public uint m_EditorGuid;
            public int m_Version;
            public string m_Id;
            public bool m_AllowDebugging;
            public uint m_DebuggerPort;

            public override string ToString()
            {
                return string.Format("{0}:{1}", new object[] { this.m_IPEndPoint.Address, this.m_IPEndPoint.Port });
            }

            public static PlayerInfo Parse(string playerString)
            {
                PlayerInfo info = new PlayerInfo();
                try
                {
                    MatchCollection matchs = new Regex(@"\[IP\] (?<ip>.*) \[Port\] (?<port>.*) \[Flags\] (?<flags>.*) \[Guid\] (?<guid>.*) \[EditorId\] (?<editorid>.*) \[Version\] (?<version>.*) \[Id\] (?<id>[^:]+)(:(?<debuggerPort>\d+))? \[Debug\] (?<debug>.*)").Matches(playerString);
                    if (matchs.Count != 1)
                    {
                        throw new Exception(string.Format("Player string not recognised {0}", playerString));
                    }
                    string ipString = matchs[0].Groups["ip"].Value;
                    info.m_IPEndPoint = new IPEndPoint(IPAddress.Parse(ipString), ushort.Parse(matchs[0].Groups["port"].Value));
                    info.m_Flags = uint.Parse(matchs[0].Groups["flags"].Value);
                    info.m_Guid = uint.Parse(matchs[0].Groups["guid"].Value);
                    info.m_EditorGuid = uint.Parse(matchs[0].Groups["guid"].Value);
                    info.m_Version = int.Parse(matchs[0].Groups["version"].Value);
                    info.m_Id = matchs[0].Groups["id"].Value;
                    info.m_AllowDebugging = 0 != int.Parse(matchs[0].Groups["debug"].Value);
                    if (matchs[0].Groups["debuggerPort"].Success)
                    {
                        info.m_DebuggerPort = uint.Parse(matchs[0].Groups["debuggerPort"].Value);
                    }
                }
                catch (Exception exception)
                {
                    throw new ArgumentException("Unable to parse player string", exception);
                }
                return info;
            }
        }

        private static List<Socket> InitSockets()
        {
            if (_multicastSockets != null)
            {
                return _multicastSockets;
            }
            List<Socket> multicastSockets = new List<Socket>();
            NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface netiface in allNetworkInterfaces)
            {
                if (netiface.Supports(NetworkInterfaceComponent.IPv4))
                {
                    IPv4InterfaceProperties properties2 = netiface.GetIPProperties().GetIPv4Properties();
                    if (properties2 != null)
                    {
                        foreach (int num in PLAYER_MULTICAST_PORTS)
                        {
                            Socket item = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            try
                            {
                                item.ExclusiveAddressUse = false;
                            }
                            catch (SocketException)
                            {
                            }
                            item.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, num);
                            item.Bind(localEP);
                            IPAddress group = IPAddress.Parse(PLAYER_MULTICAST_GROUP);
                            item.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(group, properties2.Index));
                            multicastSockets.Add(item);
                        }
                    }
                }
            }
            _multicastSockets = multicastSockets;
            return multicastSockets;
        }

        public static Connection GetConnectionForUSB(string serial)
        {
            string port;

            if (string.IsNullOrEmpty(serial))
            {
                Defiler.ErrLine("USB device not connected.");
                throw new NotSupportedException("Must call ConnectUSB() first!");
            }
            port = TransportFunctions.GetVitaPortWithSerial(serial);
            if (port == null)
            {
                Defiler.ErrLine("Cannot find serial port for {0}", serial);
                throw new IOException("Cannot find serial port.");
            }
            return new VitaUSBConnection(port);
        }

        public static PlayerInfo[] FindPlayers()
        {
            List<PlayerInfo> players = new List<PlayerInfo>();
            List<Socket> multicastSockets = InitSockets();
            foreach (Socket socket in multicastSockets)
            {
                while ((socket != null) && (socket.Available > 0))
                {
                    byte[] buffer = new byte[0x400];
                    int count = socket.Receive(buffer);
                    string playerString = Encoding.ASCII.GetString(buffer, 0, count);
                    players.Add(PlayerInfo.Parse(playerString));
                }
            }
            return players.ToArray();
        }

        public static PlayerInfo GetPlayerForWireless(PlayerInfo? player = null)
        {
            if (player.HasValue)
            {
                Defiler.MsgLine("Waiting for {0}...", player.Value.m_IPEndPoint);
            }
            else
            {
                Defiler.MsgLine("Waiting for Vita connection on the network...");
            }
            List<Socket> multicastSockets = InitSockets();
            while (true)
            {
                foreach (Socket socket in multicastSockets)
                {
                    while ((socket != null) && (socket.Available > 0))
                    {
                        byte[] buffer = new byte[0x400];
                        int count = socket.Receive(buffer);
                        string playerString = Encoding.ASCII.GetString(buffer, 0, count);
                        PlayerInfo current = PlayerInfo.Parse(playerString);
                        if (player.HasValue && current.m_Guid != player.Value.m_Guid)
                        {
                            continue;
                        }
                        return current;
                    }
                }
            }
        }
    }

    class ExploitFinder
    {
        private static readonly int VITADEFILER_PORT = 4445;

        public static void CreateFromUSB(string package, bool noExploit, out Exploit exploit, out string host, out int port)
        {
            exploit = new Exploit(ConnectionFinder.GetConnectionForUSB);
            ManualResetEvent doneinit = new ManualResetEvent(false);
            string _host = string.Empty;
            int _port = 0;

            // install package if we have to
            if (!string.IsNullOrEmpty(package))
            {
                exploit.PackageInstallUSB(package);
            }

            if (noExploit)
            {
                host = _host;
                port = _port;
                return;
            }

            // run exploit
            exploit.Connect(true, (text) =>
            {
                if (text.StartsWith("XXVCMDXX:"))
                {
#if DEBUG
                    Defiler.LogLine("[Vita] {0}", text);
#endif
                    string[] cmd = text.Trim().Split(':');
                    switch (cmd[1])
                    {
                        case "IP":
                            _host = cmd[2];
                            _port = Int32.Parse(cmd[3]);
                            Defiler.MsgLine("Found Vita network at {0}:{1}", _host, _port);
                            break;
                        case "DONE":
                            Defiler.MsgLine("Vita done initializing");
                            doneinit.Set();
                            break;
                        default:
                            Defiler.ErrLine("Unrecognized startup command");
                            break;
                    }
                }
                else
                {
                    Defiler.LogLine("[Vita] {0}", text);
                }
            });
            Defiler.MsgLine("Waiting for app to finish launching...");
            doneinit.WaitOne();
            host = _host;
            port = _port;
        }

        public static void CreateFromWireless(ConnectionFinder.PlayerInfo? player, string package, bool noExploit, out Exploit exploit, out string host, out int port)
        {
            string _host = string.Empty;
            int _port = 0;
            ManualResetEvent doneinit = new ManualResetEvent(false);
            exploit = new Exploit((serial) =>
            {
                ConnectionFinder.PlayerInfo info = ConnectionFinder.GetPlayerForWireless(player);
                Defiler.MsgLine("Found: {0}", info);
                Socket debugsock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Socket logsock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    debugsock.Connect(info.m_IPEndPoint.Address, (int)info.m_DebuggerPort);
                    logsock.Connect(info.m_IPEndPoint);
                }
                catch (Exception)
                {
                    Defiler.ErrLine("Unable to connect to {0}:{1}", info.m_IPEndPoint.Address, info.m_DebuggerPort);
                    throw;
                }

                // get network connection
                _host = info.m_IPEndPoint.Address.ToString();
                _port = VITADEFILER_PORT;

                // connect to console output
                (new Thread(() =>
                {
                    string line;
                    using (StreamReader read = new StreamReader(new NetworkStream(logsock)))
                    {
                        try
                        {
                            while ((line = read.ReadLine()) != null)
                            {
                                if (line.Contains("kernel avail main") || line.Contains("Not in scene!"))
                                {
                                    continue; // skip unity logs
                                }
                                /*
                                if (line.StartsWith("\t"))
                                {
                                    continue; // skip unity stack traces
                                }
                                 */

                                int index = line.LastIndexOf('\0'); // Unity output has some crazy garbage in front of it, so get rid of that.
                                if (index >= 0)
                                {
                                    line = line.Substring(index);
                                }

                                Defiler.LogLine("[Vita] {0}", line);
                            }
                        }
                        catch (Exception e)
                        {
                            Defiler.ErrLine(e.ToString()); // We want to know what went wrong.
                        }
                    }
                })).Start();

                // ready for network connection
                doneinit.Set();

                // return debug connection
                return new TcpConnection(debugsock);
            });

            // install package if we have to
            if (!string.IsNullOrEmpty(package))
            {
                exploit.PackageInstallUSB(package);
            }

            if (noExploit)
            {
                host = _host;
                port = _port;
                return;
            }

            // run exploit
            exploit.Connect(false, (text) =>
            {
                Defiler.LogLine("[Vita] {0}", text);
            });
            Defiler.MsgLine("Waiting for network connection...");
            doneinit.WaitOne();
            // suspend
            exploit.SuspendVM();
            host = _host;
            port = _port;
        }
    }
}
