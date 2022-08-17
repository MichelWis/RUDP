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
                binaryFormatter.Serialize(ms, _objToTest.Payload);

                res = ms.ToArray();
            }

            byte[] packetHeader = new byte[sizeof(UInt16) * 3 + res.Length];
            Array.Copy(res, 0, packetHeader, sizeof(UInt16) * 3, res.Length);

            var headerData = BitConverter.GetBytes(_objToTest.SequenceNumber);
            Array.Copy(BitConverter.GetBytes(_objToTest.SequenceNumber), 0, packetHeader, 0, headerData.Length);

            headerData = BitConverter.GetBytes((UInt16)_objToTest.Priority);
            Array.Copy(BitConverter.GetBytes((UInt16)_objToTest.Priority), 0, packetHeader, sizeof(UInt16), headerData.Length);

            headerData = BitConverter.GetBytes((UInt16)_objToTest.Type);
            Array.Copy(BitConverter.GetBytes((UInt16)_objToTest.Type), 0, packetHeader, 2 * sizeof(UInt16), headerData.Length);

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
                byte[] data = (byte[])bf.Deserialize(ms);
                return new PacketData((Packet.Type)type, seqNum, (Packet.PRIORITY)priority, data);
            }
        }
    }
}