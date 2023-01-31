namespace AlkalineThunderMud.Server;

public class Program
{
    private static void Main(string[] args)
    {
        using ServerState state = new ServerState();
        state.Run();
    }

    public static void MainNoArgs() => Main(Array.Empty<string>());
}