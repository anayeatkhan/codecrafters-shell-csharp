class Program
{
    static void Main()
    {
        Console.Write("$ ");
        var command = Console.ReadLine();
        Console.WriteLine($"{command}: command not found");
    }
}
