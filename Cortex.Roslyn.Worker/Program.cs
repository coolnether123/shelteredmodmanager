using System.Text;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Serialization;

namespace Cortex.Roslyn.Worker
{
    internal static class Program
    {
        private static int Main()
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            var server = new RoslynLanguageServiceServer();
            var writeSync = new object();
            var pendingResponses = new List<System.Threading.Tasks.Task>();
            while (true)
            {
                var line = Console.In.ReadLine();
                if (line == null)
                {
                    WaitForPendingResponses(pendingResponses);
                    return 0;
                }

                line = SanitizeInputLine(line);
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                LanguageServiceEnvelope request;
                try
                {
                    request = ManualJson.Deserialize<LanguageServiceEnvelope>(line) ?? new LanguageServiceEnvelope();
                }
                catch (Exception ex)
                {
                    WriteResponse(new LanguageServiceEnvelope
                    {
                        RequestId = string.Empty,
                        Command = string.Empty,
                        Success = false,
                        PayloadJson = string.Empty,
                        ErrorMessage = "Invalid request: " + ex.Message
                    });
                    continue;
                }

                pendingResponses.Add(WriteResponseAsync(server, request, writeSync));
                PruneCompleted(pendingResponses);
                if (string.Equals(request.Command, LanguageServiceCommands.Shutdown, StringComparison.OrdinalIgnoreCase))
                {
                    WaitForPendingResponses(pendingResponses);
                    return 0;
                }
            }
        }

        private static System.Threading.Tasks.Task WriteResponseAsync(RoslynLanguageServiceServer server, LanguageServiceEnvelope request, object writeSync)
        {
            return System.Threading.Tasks.Task.Run(async delegate
            {
                LanguageServiceEnvelope response;
                try
                {
                    response = await server.HandleQueuedAsync(request);
                }
                catch (Exception ex)
                {
                    response = new LanguageServiceEnvelope
                    {
                        RequestId = request != null ? request.RequestId ?? string.Empty : string.Empty,
                        Command = request != null ? request.Command ?? string.Empty : string.Empty,
                        Success = false,
                        PayloadJson = string.Empty,
                        ErrorMessage = ex.Message ?? "Unhandled Roslyn worker failure."
                    };
                }

                lock (writeSync)
                {
                    WriteResponse(response);
                }
            });
        }

        private static void PruneCompleted(List<System.Threading.Tasks.Task> pendingResponses)
        {
            if (pendingResponses == null || pendingResponses.Count == 0)
            {
                return;
            }

            pendingResponses.RemoveAll(task => task == null || task.IsCompleted);
        }

        private static void WaitForPendingResponses(List<System.Threading.Tasks.Task> pendingResponses)
        {
            if (pendingResponses == null || pendingResponses.Count == 0)
            {
                return;
            }

            System.Threading.Tasks.Task.WaitAll(pendingResponses.ToArray());
        }

        private static void WriteResponse(LanguageServiceEnvelope response)
        {
            Console.Out.WriteLine(ManualJson.Serialize(response));
            Console.Out.Flush();
        }

        private static string SanitizeInputLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }

            return line.TrimStart('\uFEFF', '\u200B', '\0');
        }
    }
}
