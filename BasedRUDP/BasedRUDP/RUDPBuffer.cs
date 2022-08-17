using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasedRUDP
{
    public class RUDPBufferOverflowException : Exception
    {
        public RUDPBufferOverflowException(RUDPBuffer _src) : base($"RUDPBuffer overflow: max allowed size is: {_src.m_MaxSize} packets.")
        {            }
    }
    public class RUDPBuffer 
    {
        private Queue<Packet> m_Queue { get; set; }
        public UInt32 m_MaxSize { get; set; }
        public int m_DropPacketCount { get; set; }
        public int Count { get { return m_Queue.Count; } }

        private object m_lockObj = new object();

        public RUDPBuffer(UInt32 _maxSize, int _dropPacketCount) 
        {
            m_MaxSize = _maxSize;
            m_DropPacketCount = _dropPacketCount;
            m_Queue = new Queue<Packet>();
        }

        public void Enqueue(Packet _packet)
        {
            if (m_Queue.Count > m_MaxSize)
            {
                TryDropPackets(m_DropPacketCount);
                //throw new RUDPBufferOverflowException(this);
            }
            m_Queue.Enqueue(_packet);
        }

        public Packet TryDequeue()
        {
            lock (m_lockObj)
            {
                return m_Queue.Dequeue();
            }
        }

        public Packet DequeueBySequenceNumber(UInt32 _sequenceNumber)
        {
            lock (m_lockObj)
            {
                for(int i = 0; i < m_Queue.Count; i++)
                {
                    Packet candidatePacket = m_Queue.Dequeue();
                    if (candidatePacket.m_SequenceNumber != _sequenceNumber)
                    {
                        m_Queue.Enqueue(candidatePacket);
                    }
                }
            }
            return null;
        }

        private void TryDropPackets(int _packetsToDrop)
        {
            lock (m_lockObj)
            {
                // either drop as many packets as there are in the queue, or, if the packet count is very high, only drop _packetsToDrop packets
                int iters = _packetsToDrop > m_Queue.Count ? m_Queue.Count : _packetsToDrop;
                for(int i = 0; i < iters; i++)
                {
                    Packet dropCandidate = m_Queue.Dequeue();
                    // if packet is high priority -> uses reliable transfer protocol, re-enqueue this packet again so we dont lose it. otherwise, drop it like its hot.
                    if (dropCandidate.m_Priority == Packet.PRIORITY.HIGH)
                        m_Queue.Enqueue(dropCandidate);
                }
            }
        }
    }
}
