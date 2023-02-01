using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using AlkalineThunderMud.Common;

namespace AlkalineThunderMud.Shell;

public class ShellClient : IDisposable
{
    private int connectionId;
    private NamedPipeClientStream? pipeClient;
    private ConcurrentQueue<NetworkMessageData> messagesToSend = new ConcurrentQueue<NetworkMessageData>();
    private object lockObject = new();
    private bool shouldDisconnect = false;

    public void Dispose()
    {
        pipeClient?.Dispose();
        pipeClient = null;
    }

    private void RunConsole()
    {
        Console.WriteLine("Connected!");
        Console.WriteLine();
        Console.WriteLine(
            "Type text, and it will be sent to the daemon. The daemon will send it back, and we will print it.");

        bool run = true;

        while (run)
        {
            Console.Write("> ");

            string? textToSend = Console.ReadLine();

            if (string.IsNullOrEmpty(textToSend))
                continue;

            if (textToSend == "exit")
            {
                lock (lockObject)
                {
                    shouldDisconnect = true;
                }

                break;
            }

            byte[] data = Encoding.UTF8.GetBytes(textToSend);
            Revision revision = Revision.Begin;

            NetworkMessageData message = new NetworkMessageData(revision, data);
            this.messagesToSend.Enqueue(message);
        }
    }
    
    public void Run()
    {
        pipeClient = new NamedPipeClientStream("AlkalineThunderMud");

        Console.WriteLine("Waiting to connect to the daemon . . .");
        pipeClient.Connect(30);

        bool running = pipeClient.IsConnected;

        if (!running)
        {
            Console.WriteLine("Could not communicate with the daemon. The MUD is currently unavailable!");
            return;
        }

        using (BinaryReader reader = new BinaryReader(pipeClient, Encoding.UTF8, true))
        {
            this.connectionId = reader.ReadInt32();
        }

        pipeClient.Dispose();
        
        // At this point we have a connection ID from the daemon, but we need to reconnect to the
        // daemon using a different pipe. That's WHY we have a connection ID, that's how both of us
        // know what pipe to use.
        string pipeId = $"AlkalineThunderMud_{connectionId}";
        pipeClient = new NamedPipeClientStream(pipeId);
        
        // Wait for connection on our private pipe
        pipeClient.Connect(30);

        running = pipeClient.IsConnected;

        if (!running)
        {
            Console.WriteLine("Could not communicate with the daemon. The MUD is currently unavailable!");
            return;
        }

        new Thread(RunConsole).Start();

        bool dc = false;
        byte[] buffer = new byte[1];
        bool reading = false;
        ValueTask readTask = default;
        while (running)
        {
            while (messagesToSend.TryDequeue(out NetworkMessageData? messageToSend))
            {
                using BinaryWriter writer = new BinaryWriter(pipeClient, Encoding.UTF8, true);
                messageToSend.Write(writer);
            }

            if (!reading)
            {
                reading = true;
                readTask = pipeClient.ReadExactlyAsync(buffer, 0, buffer.Length);
            }
            else
            {
                if (!readTask.IsCompleted)
                    continue;

                reading = false;
                readTask = default;

                byte firstByte = buffer[0];


                Revision revision = (Revision)firstByte;

                using var messageReader = new BinaryReader(pipeClient, Encoding.UTF8, true);
                int messageLength = messageReader.ReadInt32();
                byte[] messageData = messageReader.ReadBytes(messageLength);

                NetworkMessageData message = new NetworkMessageData(revision, messageData);

                string text = Encoding.UTF8.GetString(messageData);
                Console.WriteLine(text);
            }

            lock (lockObject)
            {
                dc = shouldDisconnect;
            }

            if (dc)
                pipeClient.Dispose();
            
            running = pipeClient.IsConnected;
        }
        
        Console.WriteLine("Goodbye");
    }
}