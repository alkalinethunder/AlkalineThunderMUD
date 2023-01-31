namespace AlkalineThunderMud.Shell;

public class Program
{
    public static void Main(string[] args)
    {
        using ShellClient client = new ShellClient();
        client.Run();
    }
}