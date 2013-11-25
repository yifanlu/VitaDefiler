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
        Echo = 9
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
                _sock.SendTimeout = 10000;
                _sock.ReceiveTimeout = 10000;
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

        public Command RunCommand(Command cmd, int[] data, out byte[] resp)
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
            if (RunCommand(cmd, data, out resp) != Command.Error)
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
                byte[] packet = new byte[data.Length + 8];
                BitConverter.GetBytes(((ulong)cmd << 32) | (uint)data.Length);
                Array.Copy(BitConverter.GetBytes((int)cmd), 0, packet, 0, 4);
                Array.Copy(BitConverter.GetBytes(data.Length), 0, packet, 4, 4);
                Array.Copy(data, 0, packet, 8, data.Length);
                _sock.Send(packet);
                // recieve packet
                int length = 8;
                int total = 0;
                byte[] recv = new byte[8];
                while (total < length)
                {
                    total += _sock.Receive(recv, total, length - total, SocketFlags.None);
                }
                Command resp = (Command)BitConverter.ToInt32(recv, 0);
                length = BitConverter.ToInt32(recv, 4);
                total = 0;
                // recieve response
                response = new byte[length];
                while (total < length)
                {
                    total += _sock.Receive(response, total, length - total, SocketFlags.None);
                }
                // check for error
                if (resp == Command.Error)
                {
                    Console.Error.WriteLine("Error from Vita: {0}", Encoding.ASCII.GetString(response));
                    response = new byte[0];
                }
                return cmd;
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
