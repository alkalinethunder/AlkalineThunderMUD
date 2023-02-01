using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using AlkalineThunderMud.Common;

namespace AlkalineThunderMud.Server;

internal class ServerState : IDisposable
{
    private readonly List<Connection> connections = new List<Connection>();
    private readonly Dictionary<int, int> connectionIds = new Dictionary<int, int>();
    private object lockObject = new();
    private bool running = true;
    private NamedPipeServerStream? pipeServer;
    private ConcurrentQueue<Action> actionQueue = new ConcurrentQueue<Action>();

    public void Dispose()
    {
        pipeServer?.Dispose();
        pipeServer = null;
    }

    private void HandleLogins()
    {
        bool waitForLogins = false;
        
        lock (lockObject)
        {
            waitForLogins = running;
        }

        int nextConnectionId = 0;
        
        while (waitForLogins)
        {
            if (pipeServer == null)
                break;

            if (pipeServer.IsConnected)
                pipeServer.Disconnect();
            
            Console.WriteLine("Waiting for another login connection...");
            pipeServer.WaitForConnection();

            if (!pipeServer.IsConnected)
                continue;

            
            using BinaryWriter writer = new BinaryWriter(pipeServer, Encoding.UTF8, true);
            writer.Write(nextConnectionId);

            int connectionId = nextConnectionId;
            actionQueue.Enqueue(() =>
            {
                CreateNewConnection(connectionId);
            });
            
            nextConnectionId++;

            lock (lockObject)
            {
                waitForLogins = running;
            }
        }
    }

    private void CreateNewConnection(int id)
    {
        string pipeId = $"AlkalineThunderMud_{id}";
        NamedPipeServerStream pipe = new NamedPipeServerStream(pipeId);

        Connection connection = new Connection(id, pipe);
        connectionIds.Add(id, connections.Count);
        connections.Add(connection);

        connection.StartThread();
    }
    
    public void Run()
    {
        pipeServer = new NamedPipeServerStream("AlkalineThunderMud");
        
        // We need to handle multiple users, but we're not using TCP... we can only have one client
        // on the main AlkalineThunderMud pipe.
        //
        // But that's okay. We can use the main pipe to wait for new connections from the ssh shell
        // process. As soon as we get a connection on the main pipe, we send them a unique connection 
        // ID and create a pipe based on that. The client will then connect to that and we'll listen for
        // messages on another thread.
        // 
        // We also wait for logins on another thread so we don't block existing users.
        new Thread(HandleLogins).Start();
        
        // This is where the "game" loop starts.
        bool shouldRun = false;

        lock (lockObject)
        {
            shouldRun = running;
        }
        
        while (shouldRun)
        {
            while (actionQueue.TryDequeue(out Action? action))
                action?.Invoke();
            
            for (var i = 0; i < connections.Count; i++)
            {
                Connection connection = connections[i];

                while (connection.TryDequeueMessage(out NetworkMessageData? message))
                {
                    if (message != null)
                        connection.EnqueueMessage(message);
                }
            }
            
            lock (lockObject)
            {
                shouldRun = running;
            }
        }
    }
}