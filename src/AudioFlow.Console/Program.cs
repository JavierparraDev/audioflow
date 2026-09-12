using AudioFlow.Core.Logging;

namespace AudioFlow.ConsoleApp;

internal static class Program
{
    private static async Task<int> Main(string[] args)
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
        var rest = args.Length > 1 ? args[1..] : Array.Empty<string>();

        try
        {
            switch (command)
            {
                case "devices":
                    return Commands.Devices();
                case "sessions":
                    return Commands.Sessions();
                case "monitor":
                    return Commands.Monitor();
                case "rules":
                    return Commands.Rules();
                case "plan":
                    return Commands.Plan();
                case "apply":
                    return Commands.Apply();
                case "verify":
                    return Commands.Verify(rest);
                case "route-pid":
                    return Commands.RoutePid(rest);
                case "loopback-probe":
                    return Commands.LoopbackProbe(rest);
                case "loopback-capture":
                    return Commands.LoopbackCapture(rest);
                case "mute-pid":
                    return Commands.MutePid(rest);
                case "live-route":
                    return Commands.LiveRoute(rest);
                case "set-default":
                    return Commands.SetDefault(rest);
                case "set-rule":
                    return Commands.SetRule(rest);
                case "remove-rule":
                    return Commands.RemoveRule(rest);
                case "lock":
                    return Commands.Lock(rest);
                case "unlock":
                    return Commands.Unlock();
                case "version":
                    return Commands.Version();
                case "update":
                    return await Commands.Update(rest);
                case "help" or "--help" or "-h":
                    return Commands.Help();
                default:
                    return Unknown(command);
            }
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
