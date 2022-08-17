using BasedRUDP;
using BasedSerializer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RUDPTesteyBoii
{
    class Program
    {
        static void Main(string[] args)
        {
            IPEndPoint a = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3020);
            IPEndPoint b = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3021);
            string inp = ";";

            if (Console.ReadLine() == "s")
            {
                RUDPSocket socket = new RUDPSocket(a, null);
                while (inp.ToLower() != "q")
                {
                    inp = Console.ReadLine();
                    socket.SendTo(Encoding.ASCII.GetBytes(inp), b, Utils.RandomBool(1f) ? Packet.PRIORITY.LOW : Packet.PRIORITY.HIGH);
                }
                socket.Close();
            }
            else
            {
                RUDPSocket socket = new RUDPSocket(b, null);
                socket.ConnectTo(a);

                while (inp.ToLower() != "q")
                {
                    Thread.Sleep(2);
                    continue;
                    try
                    {
                        inp = Console.ReadLine();
                        socket.SendTo(Encoding.ASCII.GetBytes(inp), a, Utils.RandomBool(1f) ? Packet.PRIORITY.LOW : Packet.PRIORITY.HIGH);
                    }
                    catch (RUDPBufferOverflowException ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                socket.Close();
            }
        }
    }
}
