using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BasedRUDP
{
    public delegate void OnTickCallback();
    public delegate void OnRemoteSocketConnectedCallback(RemoteNetworkInstance _remoteInstance);
    public delegate void OnRemoteSocketDisconnectCallback(RemoteNetworkInstance _remoteInstance);

    public class RUDPSocket
    {
        public bool IsConnectedToAny { get { return ConnectedSockets.Count > 0; } }
        public List<RUDPLogMessage> LogBuffer = new List<RUDPLogMessage>();

        public Dictionary<IPEndPoint, RemoteNetworkInstance> ConnectedSockets = new Dictionary<IPEndPoint, RemoteNetworkInstance>();

        private Socket m_BaseSocket { get; set; }
        private byte[] m_RawBytes = new byte[65536];
        private UInt16 m_ConnectPacketID { get; set; }

        private RUDPBuffer m_SentPackets { get; set; }
        private RUDPBuffer m_PacketsToSend { get; set; }
        private RUDPBuffer m_IncomingPackets { get; set; }

        private Queue<UInt16> m_LastIDs;

        private Thread m_socketThread;

        private Random m_IDNumberGenerator = new Random();

        // TODO: implement proper sequencing (random) not 0->int32.max
        private UInt16 m_NextSequenceNumber { get {
                m_LastSequenceNumber = (UInt16)(m_IDNumberGenerator.Next());
                return m_LastSequenceNumber; 
            } }
        private UInt16 m_LastSequenceNumber;

        private UInt16 m_MaxPacketsOutPerFrame { get; set; } = 10;
        private UInt16 m_MaxPacketsInPerFrame { get; set; } = 50;
        private UInt32 m_MaxPacketsInBuffer = 500;
        private UInt16 m_AcknowledgeTimeoutTicks = 50;
        private int m_MaxIDsSaved = 500; // would be ~8MB for the ids, thatd be ok i think
        private ulong m_MaxTimeTimeoutTicks = 10000000;

        private UInt64 m_TickCount { get; set; }
        private int m_DropPacketCount = 200;

        private OnTickCallback m_onTickCallback;
        private OnTickCallback m_defaultOnTickCallback = () => { };

        private OnRemoteSocketConnectedCallback m_onRemoteSocketConnectedCallback = null;
        private OnRemoteSocketDisconnectCallback m_onRemoteSocketDisconnectCallback= null;

        public RUDPSocket(IPEndPoint _bindToAddress, OnTickCallback _onTickCallback, OnRemoteSocketConnectedCallback _onRemoteSocketConnected = null, OnRemoteSocketDisconnectCallback _onRemoteSocketDisconnected = null)
        {
            m_onRemoteSocketConnectedCallback = _onRemoteSocketConnected;
            m_onRemoteSocketDisconnectCallback = _onRemoteSocketDisconnected;
            m_onTickCallback = _onTickCallback == null ? m_defaultOnTickCallback : _onTickCallback;

            m_BaseSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_BaseSocket.Bind(_bindToAddress);

            m_SentPackets = new RUDPBuffer(m_MaxPacketsInBuffer, m_DropPacketCount);
            m_PacketsToSend = new RUDPBuffer(m_MaxPacketsInBuffer, m_DropPacketCount);
            m_IncomingPackets = new RUDPBuffer(m_MaxPacketsInBuffer, m_DropPacketCount);
            m_LastIDs = new Queue<UInt16>(m_MaxIDsSaved);
            m_socketThread = new Thread(_Tick);
            m_socketThread.Start();
        }

        private void Log(object _toLog)
        {
            Console.WriteLine(_toLog);
            lock (LogBuffer)
            {
                LogBuffer.Add(new RUDPLogMessage(_toLog.ToString(), RUDPLogMessage.Type.LOG));
            }
        }

        private void LogError(object _toLog)
        {
            Console.WriteLine(_toLog);
            lock (LogBuffer)
            {
                LogBuffer.Add(new RUDPLogMessage(_toLog.ToString(), RUDPLogMessage.Type.ERROR));
            }
        }

        private void _Tick()
        {
            while (true)
            {
                m_TickCount++;

                List<RemoteNetworkInstance> disconnectedSockets = new List<RemoteNetworkInstance>();
                lock (ConnectedSockets)
                {
                    foreach (var client in ConnectedSockets.Values)
                    {
                        if (client.LastComTimepoint >= m_MaxTimeTimeoutTicks)
                        {
                            disconnectedSockets.Add(client);
                        }
                    }
                    foreach (var skt in disconnectedSockets)
                        OnDisconnect(skt.EndPoint);
                }
                lock (m_SentPackets)
                {
                    UInt64 offsetTimeout = (UInt64)(m_TickCount - m_AcknowledgeTimeoutTicks);
                    for (int i = 0; i < m_SentPackets.Count; i++)
                    {
                        Packet candidatePacket = m_SentPackets.TryDequeue();
                        Log(offsetTimeout + "    " + candidatePacket.m_AcknowledgeLastTickCount);
                        // still in m_SentPackets AND timed out? resend packet!
                        if (candidatePacket.m_AcknowledgeLastTickCount < offsetTimeout)
                        {
                            m_PacketsToSend.Enqueue(candidatePacket);
                        }
                        else
                        {
                            m_SentPackets.Enqueue(candidatePacket);
                        }
                    }
                }

                // receive packets
                // check acks
                // send packets
                int packetCount = 0;
                EndPoint fromEP = new IPEndPoint(IPAddress.Any, 0);
                while (m_BaseSocket.Available > 0 && packetCount < m_MaxPacketsInPerFrame)
                {
                    packetCount++;
                    int byteLen = m_BaseSocket.ReceiveFrom(m_RawBytes, ref fromEP);
                    Packet packet = new Packet(m_RawBytes, (IPEndPoint) fromEP);
                    Log("Packet received.");
                    // if we already received this packed in the last n packets, continue;
                    if (m_LastIDs.Contains(packet.m_SequenceNumber))
                        continue;

                    if (packet.m_Priority == Packet.PRIORITY.HIGH)
                    {
                        Acknowledge(packet.m_SequenceNumber, (IPEndPoint)fromEP);
                    }
                    Log(packet.m_Type.ToString());
                    if (packet.m_Type == Packet.Type.ACK)
                    {
                        OnAcknowledge(packet);
                        // no need to do more stuff with this message; just ignore it after registering it.
                        continue;
                    }

                    if (packet.m_Type == Packet.Type.CONNECT)
                    {
                        OnConnect(packet.EndPoint);
                        if (m_onRemoteSocketConnectedCallback != null)
                        {
                            ConnectedSockets.TryGetValue(fromEP as IPEndPoint, out RemoteNetworkInstance remoteNetInst);
                            m_onRemoteSocketConnectedCallback(remoteNetInst);
                        }
                        // no need to do more stuff with this message; just ignore it after registering it.
                        continue;
                    }
                    else if (packet.m_Type == Packet.Type.DISCONNECT)
                    {
                        OnDisconnect(packet.EndPoint);
                        // no need to do more stuff with this message; just ignore it after registering it.
                        continue;
                    }
                    if (!ConnectedSockets.ContainsKey(fromEP as IPEndPoint))
                    {
                        Log("Illegal packet: packet did not originate from a connected EndPoint!");
                        continue;
                    }

                    try
                    {
                        m_IncomingPackets.Enqueue(packet);
                    }
                    catch(RUDPBufferOverflowException exception)
                    {
                        Log("Too many unread packets in incoming buffer! " + exception.Message);
                    }

                    m_LastIDs.Enqueue(packet.m_SequenceNumber);
                    if(m_LastIDs.Count > m_MaxIDsSaved)
                        m_LastIDs.Dequeue();
                }

                packetCount = 0;
                while (m_PacketsToSend.Count > 0 && packetCount < m_MaxPacketsOutPerFrame)
                {
                    Packet toSend = m_PacketsToSend.TryDequeue();
                    try
                    {
                        byte[] sendRawBytes = toSend.Pack();
                        int byteLen = m_BaseSocket.SendTo(sendRawBytes, toSend.EndPoint);
                        // m_SentPackets is only there for keeping track of the sent reliable UDP packets. unreliably sent packets are not kept track of
                        if(toSend.m_Priority == Packet.PRIORITY.HIGH)
                            m_SentPackets.Enqueue(toSend);
                    }
                    catch(SocketException ex)
                    {
                        Log($"Failed to send packet: \n{ex.Message}");
                        if (toSend.m_Priority == Packet.PRIORITY.HIGH)
                            m_PacketsToSend.Enqueue(toSend);
                    }
                    packetCount++;
                }
                Thread.Sleep(2);
                m_onTickCallback();
            }
        }

#if DEBUG
        private void OnMessageDebug(Packet _packet, IPEndPoint _fromEP, int _byteLen)
        {
            Log($"received packet from: {_fromEP} of type: {_packet.m_Type} regarding sequence number: {_packet.m_SequenceNumber} with byte count: {_byteLen}");
        }
#endif
        private void Acknowledge(UInt16 _sequenceNumber, IPEndPoint _ep)
        {
            Packet ackPack = new Packet(_sequenceNumber, Packet.Type.ACK, new byte[] { }, _ep, Packet.PRIORITY.LOW);
            m_PacketsToSend.Enqueue(ackPack);
        }

        public List<Packet> ReceivePackets()
        {
            List<Packet> packets = new List<Packet>();
            for (int i = 0; i < m_IncomingPackets.Count; i++)
                packets.Add(m_IncomingPackets.TryDequeue());
            return packets;
        }

        private void OnConnect(IPEndPoint _ep)
        {
            if (ConnectedSockets.ContainsKey(_ep))
                return;
            ConnectedSockets.Add(_ep, new RemoteNetworkInstance(_ep, m_TickCount));
            Log("client connected.");
        }

        private void OnDisconnect(IPEndPoint _ep)
        {
            lock (ConnectedSockets)
            {
                if (ConnectedSockets.TryGetValue(_ep, out RemoteNetworkInstance instance))
                    m_onRemoteSocketDisconnectCallback(instance);
                ConnectedSockets.Remove(_ep);
            }
            Log("client disconnected or timed out.");
        }

        private void OnAcknowledge(Packet _acknowledgePacket)
        {
            m_SentPackets.DequeueBySequenceNumber(_acknowledgePacket.m_SequenceNumber);
            if (_acknowledgePacket.m_SequenceNumber == m_ConnectPacketID)
                OnConnect(_acknowledgePacket.EndPoint);
        }

        public void Close()
        {
            m_socketThread.Abort();
            Log("socket closed.");
            foreach (IPEndPoint ep in ConnectedSockets.Keys)
            {
                SendTo(null, ep, Packet.Type.DISCONNECT);
            }
            m_BaseSocket.Close();
        }

        public void ConnectTo(IPEndPoint _ep)
        {
            SendTo(null, _ep, Packet.Type.CONNECT);
        }

        public void SendTo(byte[] _payload, IPEndPoint _target, Packet.PRIORITY _priority = Packet.PRIORITY.HIGH)
        {
            SendTo(_payload, _target, Packet.Type.STD, _priority);
        }

        private void SendTo(byte[] _payload, IPEndPoint _target, Packet.Type _type, Packet.PRIORITY _priority = Packet.PRIORITY.HIGH)
        {
            UInt16 seqNum = m_NextSequenceNumber;
            if (_type == Packet.Type.CONNECT)
                m_ConnectPacketID = seqNum;
            Packet toSend = new Packet(seqNum, _type, _payload, _target, _priority);
            m_PacketsToSend.Enqueue(toSend);
        }
    }
}

public struct RUDPLogMessage
{
    public enum Type
    {
        LOG, ERROR
    }
    Type MessageType;
    public string Msg { get; set; }

    public RUDPLogMessage(string _content, Type _type)
    {
        MessageType = _type;
        Msg = _content;
    }
}