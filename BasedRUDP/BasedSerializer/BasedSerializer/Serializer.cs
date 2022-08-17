using System;
using BasedRUDP;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;

namespace BasedSerializer
{
    public static class Serializer
    {
        public static byte[] Serialize(PacketData _objToTest)
        {
            byte[] res;
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                binaryFormatter.Serialize(ms, _objToTest.m_Payload);

                res = ms.ToArray();
                Console.WriteLine("\tpayload byte count: " + res.Length);
            }

            byte[] packetHeader = new byte[sizeof(UInt16) * 3 + res.Length];
            Array.Copy(res, 0, packetHeader, sizeof(UInt16) * 3, res.Length);

            var headerData = BitConverter.GetBytes(_objToTest.m_SequenceNumber);
            Array.Copy(BitConverter.GetBytes(_objToTest.m_SequenceNumber), 0, packetHeader, 0, headerData.Length);

            headerData = BitConverter.GetBytes((UInt16)_objToTest.m_Priority);
            Array.Copy(BitConverter.GetBytes((UInt16)_objToTest.m_Priority), 0, packetHeader, sizeof(UInt16), headerData.Length);

            headerData = BitConverter.GetBytes((UInt16)_objToTest.m_Type);
            Array.Copy(BitConverter.GetBytes((UInt16)_objToTest.m_Type), 0, packetHeader, 2 * sizeof(UInt16), headerData.Length);

            Console.WriteLine("\tbyte count: " + packetHeader.Length);
            return packetHeader;
        }

        public static PacketData Deserialize(byte[] _rawData)
        {
            var seqNum = BitConverter.ToUInt16(_rawData, 0);
            var priority = BitConverter.ToUInt16(_rawData, sizeof(UInt16));
            var type = BitConverter.ToUInt16(_rawData, 2 * sizeof(UInt16));

            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(_rawData.Skip(sizeof(UInt16) * 3).ToArray()))
            {
                object data = bf.Deserialize(ms);
                return new PacketData((Packet.Type)type, seqNum, (Packet.PRIORITY)priority, data);
            }
        }
    }
}
