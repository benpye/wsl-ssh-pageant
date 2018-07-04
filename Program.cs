using Microsoft.Extensions.CommandLineUtils;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WslSSHPageant
{
    class Program
    {
        static void Main(string[] args)
        {
            CommandLineApplication commandLineApplication = new CommandLineApplication(throwOnUnexpectedArg: false);

            CommandOption wslSocketPath = commandLineApplication.Option(
                "--wsl <path>",
                "Which path to listen on with the AF_UNIX socket for WSL",
                CommandOptionType.SingleValue);

            CommandOption winsshPipeName = commandLineApplication.Option(
                "--winssh <name>",
                "Which pipe to listen on for Windows 10 OpenSSH Client",
                CommandOptionType.SingleValue);

            commandLineApplication.HelpOption("-? | -h | --help");

            List<Task> runningServers = new List<Task>();

            commandLineApplication.OnExecute(() =>
            {
                if (wslSocketPath.HasValue())
                {
                    WSLSocket wslSocket = new WSLSocket(wslSocketPath.Value());
                    runningServers.Add(wslSocket.Listen());
                }
                if (winsshPipeName.HasValue())
                {
                    WinSSHSocket winsshSocket = new WinSSHSocket(winsshPipeName.Value());
                    runningServers.Add(winsshSocket.Listen());
                }

                if (runningServers.Count < 1)
                {
                    commandLineApplication.ShowHelp();
                    return 1;
                }

                Task.WaitAny(runningServers.ToArray());

                return 0;
            });

            commandLineApplication.Execute(args);
        }
    }
}