using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using AlkalineThunderMud.Common;

namespace AlkalineThunderMud.Server;

public class Connection : IDisposable
{
    private object lockObject = new();
    private readonly int id;
    private readonly NamedPipeServerStream stream;
    private bool connected;
    private ConcurrentQueue<NetworkMessageData> receivedMessages = new ConcurrentQueue<NetworkMessageData>();
    private ConcurrentQueue<NetworkMessageData> sentMessages = new ConcurrentQueue<NetworkMessageData>();

    internal Connection(int id, NamedPipeServerStream stream)
    {
        this.id = id;
        this.stream = stream;
    }
    
    public void Dispose()
    {
        this.stream.Dispose();
    }

    public void EnqueueMessage(NetworkMessageData message)
    {
        this.sentMessages.Enqueue(message);
    }

    public bool TryDequeueMessage(out NetworkMessageData? message)
    {
        return receivedMessages.TryDequeue(out message);
    }

    public void StartThread()
    {
        connected = true;

        new Thread(RunConnection).Start();
    }

    private void RunConnection()
    {
        bool running = false;
        
        
        stream.WaitForConnection();
        
        lock (lockObject)
        {
            connected = stream.IsConnected;
            running = connected;
        }
        
        while (running)
        {
            while (sentMessages.TryDequeue(out NetworkMessageData? messageToSend))
            {
                using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                messageToSend.Write(writer);
            }
            
            int firstByte = stream.ReadByte();
            if (firstByte > -1)
            {
                Revision revision = (Revision)firstByte;

                using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
                int messageLength = reader.ReadInt32();
                byte[] messageData = reader.ReadBytes(messageLength);
                
                NetworkMessageData message = new NetworkMessageData(revision, messageData);
                receivedMessages.Enqueue(message);
            }
            
            lock (lockObject)
            {
                connected = stream.IsConnected;
                running = connected;
            }
        }
    }
}