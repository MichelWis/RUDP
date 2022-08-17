using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace BasedRUDP
{
    [Serializable]
    public struct PacketData 
    {
        public Packet.PRIORITY Priority { get; set; }

        public Packet.Type Type { get; set; }

        public UInt16 SequenceNumber { get; set; }

        public byte[] Payload { get; set; }

        public PacketData(Packet.Type _type, UInt16 _sequenceNumber, Packet.PRIORITY _priority, byte[] _payload)
        {
            Type = _type;
            SequenceNumber = _sequenceNumber;
            Priority = _priority;
            Payload = _payload;
        }
    }

    public class Packet : IEqualityComparer<Packet>
    {
        public IPEndPoint EndPoint { get; set; }
        
        public enum PRIORITY : UInt16
        {
            LOW = 0, HIGH
        }
        public enum Type : UInt16
        {
            ACK = 0, DISCONNECT, CONNECT, ALIVE, STD
        }

        public Type m_Type { get; set; }

        public UInt16 m_SequenceNumber { get { return m_sequenceNumber; } }
        private UInt16 m_sequenceNumber = 0;

        public PRIORITY m_Priority { get; set; }

        public byte[] m_Payload { get; set; }

        public UInt64 m_AcknowledgeLastTickCount { get; set; }

        public Packet(UInt16 _sequenceNumber, Type _type, byte[] _payload, IPEndPoint _endPoint, PRIORITY _priority = PRIORITY.HIGH)
        {
            m_Priority = _priority;
            m_sequenceNumber = _sequenceNumber;
            m_Type = _type;
            m_Payload = _payload;
            EndPoint = _endPoint;
        }

        public Packet(byte[] _encodedRaw, IPEndPoint _ep)
        {
            PacketData data = BasedSerializer.Serializer.Deserialize(_encodedRaw);
                
            m_Priority = data.Priority;
            m_sequenceNumber = data.SequenceNumber;
            m_Type = data.Type;
            m_Payload = data.Payload;
            EndPoint = _ep;
        }

        public byte[] Pack()
        {
            if (m_Payload == null)
                m_Payload = new byte[] { };
            return BasedSerializer.Serializer.Serialize(new PacketData(m_Type, m_sequenceNumber, m_Priority, m_Payload));
        }

        // checks by sequence number
        public bool Equals(Packet x, Packet y)
        {
            if (x.GetHashCode() == y.GetHashCode())
                return true;
            return false;
        }

        // returns sequence number as int (starting at int.MinValue)
        public int GetHashCode(Packet obj)
        {
            return (int)(m_SequenceNumber-(int.MaxValue - 1));
        }
    }
}