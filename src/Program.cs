class Program
{
    static void Main()
    {
        while (true)
        {
            Console.Write("$ ");
            var command = Console.ReadLine();
            if(command == "exit")
            {
                break;
            }
            else if(command.startsWith("echo "))
            {
                Console.WriteLine(command[5..]);
                continue;
            }
            else
            {
                 Console.WriteLine($"{command}: command not found");

            }
        }
    }
}
