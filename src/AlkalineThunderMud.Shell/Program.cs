// See https://aka.ms/new-console-template for more information

Console.WriteLine("You have connected to the Socially Distant MUD successfully.");

bool running = true;

Console.WriteLine("Type 'exit' to disconnect. This prompt will echo whatever you type.");

while (running)
{
    Console.Write("> ");
    string line = Console.ReadLine();
    if (line == "exit")
        running = false;

    Console.WriteLine(line);
}