using System;
using System.Threading.Tasks;

namespace IMEInd;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("IMEInd Utility Tool");
        Console.WriteLine("===================");

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: IMEInd [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --help    Show this help message");
            Console.WriteLine("  --version Show version information");
            return 0;
        }

        if (args[0] == "--help")
        {
            ShowHelp();
            return 0;
        }

        if (args[0] == "--version")
        {
            Console.WriteLine("Version 1.0.0");
            return 0;
        }

        // Add your utility logic here
        var service = new UtilityService();
        await service.RunAsync(args);

        return 0;
    }

    static void ShowHelp()
    {
        Console.WriteLine("IMEInd - A Windows utility tool");
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  --help    Display this help message");
        Console.WriteLine("  --version Display version information");
    }
}
