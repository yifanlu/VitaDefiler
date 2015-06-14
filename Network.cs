using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace VitaDefiler
{
    enum Command
    {
        Error = 0,
        AllocateData = 1,
        AllocateCode = 2,
        FreeData = 3,
        FreeCode = 4,
        WriteData = 5,
        WriteCode = 6,
        ReadData = 7,
        Execute = 8,
        Echo = 9,
        SetFuncPtrs = 10,
        Exit = 11,
        PushFile = 12,
        PullFile = 13 
    }

    class Network
    {
        private Socket _sock;

        public bool Connect(string host, int port)
        {
            Disconnect(); // disconnect previous connection
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(host);
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress,port);

                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _sock.Connect(remoteEP);
                _sock.SendTimeout = 20000;
                //_sock.ReceiveTimeout = 20000;
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error connecting to Vita network: {0}", ex.Message);
                return false;
            }
        }

        public void Disconnect()
        {
            if (_sock != null)
            {
                _sock.Shutdown(SocketShutdown.Both);
                _sock.Close();
            }
        }

        public int RunCommand(Command cmd)
        {
            byte[] ret;
            RunCommand(cmd, out ret);
            return BitConverter.ToInt32(ret, 0);
        }

        public int RunCommand(Command cmd, int data)
        {
            return RunCommand(cmd, BitConverter.GetBytes(data));
        }

        public Command RunCommand(Command cmd, uint[] data, out byte[] resp)
        {
            byte[] bdata = new byte[data.Length * sizeof(int)];
            for (int i = 0; i < data.Length; i++)
            {
                Array.Copy(BitConverter.GetBytes(data[i]), 0, bdata, i * sizeof(int), sizeof(int));
            }
            return RunCommand(cmd, bdata, out resp);
        }

        public int RunCommand(Command cmd, byte[] data)
        {
            byte[] resp;
            if (RunCommand(cmd, data, out resp) != Command.Error && resp.Length > 0)
            {
                return BitConverter.ToInt32(resp, 0);
            }
            else
            {
                return 0;
            }
        }

        public void RunCommand(Command cmd, out byte[] data)
        {
            RunCommand(cmd, new byte[0], out data);
        }

        public Command RunCommand(Command cmd, byte[] data, out byte[] response)
        {
            try
            {
                // create and send packet
                byte[] packet = new byte[data.Length + 2 * sizeof(int)];
                Array.Copy(BitConverter.GetBytes((int)cmd), 0, packet, 0, sizeof(int));
                Array.Copy(BitConverter.GetBytes(data.Length), 0, packet, sizeof(int), sizeof(int));
                Array.Copy(data, 0, packet, 2 * sizeof(int), data.Length);
#if DEBUG
                Console.Error.WriteLine("Sending packet of {0} bytes.", packet.Length);
                packet.PrintHexDump((uint)packet.Length, 16);
#endif
                _sock.Send(packet);
                // recieve packet
                int length = 2 * sizeof(int);
                int total = 0;
                byte[] recv = new byte[2 * sizeof(int)];
                while (total < length)
                {
                    total += _sock.Receive(recv, total, length - total, SocketFlags.None);
                }
                Command resp = (Command)BitConverter.ToInt32(recv, 0);
                length = BitConverter.ToInt32(recv, sizeof(int));
                total = 0;
#if DEBUG
                Console.Error.WriteLine("Recieving header of {0} bytes.", recv.Length);
                recv.PrintHexDump((uint)recv.Length, 16);
#endif
                // recieve response
                response = new byte[length];
                while (total < length)
                {
                    total += _sock.Receive(response, total, length - total, SocketFlags.None);
                }
#if DEBUG
                Console.Error.WriteLine("Recieving response of {0} bytes.", response.Length);
#endif
                // check for error
                if (resp == Command.Error)
                {
                    Console.Error.WriteLine("Error from Vita: {0}", Encoding.ASCII.GetString(response));
                    response = new byte[0];
                }
                return resp;
            }
            catch (SocketException ex)
            {
                Console.Error.WriteLine("Vita is not responding, socket error: {0}", ex.Message);
                response = new byte[0];
                return Command.Error;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error trying to run command {0}: {1}", cmd, ex.Message);
                response = new byte[0];
                return Command.Error;
            }
        }
    }
}
