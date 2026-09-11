using AudioFlow.Core.Logging;

namespace AudioFlow.ConsoleApp;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            // Some terminals do not support changing the encoding.
        }

        var command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "devices";

        try
        {
            return command switch
            {
                "devices" => Commands.Devices(),
                "sessions" => Commands.Sessions(),
                "monitor" => Commands.Monitor(),
                "help" or "--help" or "-h" => Commands.Help(),
                _ => Unknown(command)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected failure");
            return 1;
        }
    }

    private static int Unknown(string command)
    {
        Console.WriteLine($"Unknown command: {command}");
        Console.WriteLine();
        return Commands.Help();
    }
}
