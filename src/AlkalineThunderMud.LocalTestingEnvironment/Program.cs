// Emulate the server being run as a separate process by starting it on another thread.
Thread serverThread = new Thread(global::AlkalineThunderMud.Server.Program.MainNoArgs);
serverThread.Start();

global::AlkalineThunderMud.Shell.Program.Main(args);