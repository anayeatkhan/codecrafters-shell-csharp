class Program
{
    static void Main()
    {
        List<string> builtInCommands = new List<string> { "echo", "type" };
        while (true)
        {
            Console.Write("$ ");
            var command = Console.ReadLine();
            if(command == "exit")
            {
                break;
            }
            else if(command.StartsWith("echo "))
            {
                Console.WriteLine(command[5..]);
                continue;
            }
            else if(command.StartsWith("type "))
            {
                var checkCommand = command[5..];
                var isBuiltin = builtInCommands.FirstOrDefault(t => t == checkCommand);
                if(isBuiltin != null)
                {
                    Console.WriteLine($"{checkCommand}: not found");
                }
                else
                {
                    Console.WriteLine($"{checkCommand} is a shell builtin");
                }
            }
            else
            {
                 Console.WriteLine($"{command}: command not found");

            }
        }
    }
}
