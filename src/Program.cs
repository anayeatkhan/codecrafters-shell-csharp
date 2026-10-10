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
                var command = command[5..];
                var isBuiltin = builtInCommands.FirstOrDefault(t => t == command);
                if(isBuiltin != null)
                {
                    Console.WriteLine($"{command}: not found");
                }
                else
                {
                    Console.WriteLine($"{command} is a shell builtin");
                }
            }
            else
            {
                 Console.WriteLine($"{command}: command not found");

            }
        }
    }
}
