class Program
{
    static void Main()
    {
        List<string> builtInCommands = new List<string> { "echo", "type" , "exit" };
        string[] paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator , StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
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
                var isBuiltin = builtInCommands.Any(t => t == checkCommand);
                if(!isBuiltin)
                {
                    // Console.WriteLine($"{checkCommand}: not found");
                    var found = false;
                    foreach (var path in paths)
                    {
                        string fullPath = Path.Combine(path, checkCommand);
                        if(!File.Exists(fullPath))
                        {
                            continue;
                        }
                        var mode = File.GetUnixFileMode(fullPath);
                        bool isExec = (mode & UnixFileMode.UserExecute) != 0 || (mode & UnixFileMode.GroupExecute) != 0 || (mode & UnixFileMode.OtherExecute) != 0;
                        if ( File.Exists(fullPath) && isExec)
                        {
                            Console.WriteLine($"{checkCommand} is {fullPath}");
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        Console.WriteLine($"{checkCommand}: not found");
                    }
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
