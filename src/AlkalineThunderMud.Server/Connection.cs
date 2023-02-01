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
        this.connected = true;
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
        new Thread(RunConnection).Start();
    }

    private void RunConnection()
    {
        bool running = false;


        stream.WaitForConnection();

        lock (lockObject)
        {
            running = connected;
        }

        bool reading = false;
        ValueTask readTask = default;
        byte[] buffer = new byte[1];

        while (running)
        {
            while (sentMessages.TryDequeue(out NetworkMessageData? messageToSend))
            {
                using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                messageToSend.Write(writer);
            }

            if (!reading)
            {
                reading = true;
                readTask = stream.ReadExactlyAsync(buffer, 0, buffer.Length);
            }
            else
            {
                if (!readTask.IsCompleted)
                    continue;

                readTask = default;
                byte firstByte = buffer[0];



                Revision revision = (Revision)firstByte;

                using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
                int messageLength = reader.ReadInt32();
                byte[] messageData = reader.ReadBytes(messageLength);

                NetworkMessageData message = new NetworkMessageData(revision, messageData);
                receivedMessages.Enqueue(message);
            }

            lock (lockObject)
            {
                running = connected;
            }
        }
    }
}