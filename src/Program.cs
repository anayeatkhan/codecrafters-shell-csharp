using System.Diagnostics;

class Program
{
    static readonly List<string> builtInCommands = new List<string> { "echo", "type", "exit" };

    static bool IsExecutable(string path)
    {
        // Windows has no execute bits; any existing file counts
        if (OperatingSystem.IsWindows()) return true;

        var mode = File.GetUnixFileMode(path);
        const UnixFileMode anyExec = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return (mode & anyExec) != 0;
    }

    // Returns the full path of the first executable named `name` in PATH, or null
    static string? FindExecutable(string name, string[] paths)
    {
        foreach (var path in paths)
        {
            string fullPath = Path.Combine(path, name);
            if (File.Exists(fullPath) && IsExecutable(fullPath))
            {
                return fullPath;
            }
        }
        return null;
    }

    static void RunExternal(string[] args)
    {
        // Pass the bare name, not the full path: .NET uses FileName as argv[0],
        // and the program must see the name the user typed
        var psi = new ProcessStartInfo(args[0]);
        foreach (var arg in args[1..])
        {
            psi.ArgumentList.Add(arg);
        }

        // Output is not redirected, so the program writes straight to our terminal
        using var process = Process.Start(psi)!;
        process.WaitForExit();
    }

    static void Main()
    {
        string[] paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        while (true)
        {
            Console.Write("$ ");
            var command = Console.ReadLine();
            if (command == null)
            {
                break;
            }

            var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0)
            {
                continue;
            }

            if (command == "exit")
            {
                break;
            }
            else if (command.StartsWith("echo "))
            {
                Console.WriteLine(command[5..]);
            }
            else if (command.StartsWith("type "))
            {
                var checkCommand = command[5..];
                if (builtInCommands.Contains(checkCommand))
                {
                    Console.WriteLine($"{checkCommand} is a shell builtin");
                }
                else if (FindExecutable(checkCommand, paths) is string fullPath)
                {
                    Console.WriteLine($"{checkCommand} is {fullPath}");
                }
                else
                {
                    Console.WriteLine($"{checkCommand}: not found");
                }
            }
            else if (FindExecutable(args[0], paths) != null)
            {
                RunExternal(args);
            }
            else
            {
                Console.WriteLine($"{command}: command not found");
            }
        }
    }
}
