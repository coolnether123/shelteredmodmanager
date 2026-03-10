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
            while (true)
            {
                var line = Console.In.ReadLine();
                if (line == null)
                {
                    return 0;
                }

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

                var response = server.Handle(request);
                WriteResponse(response);
                if (string.Equals(request.Command, LanguageServiceCommands.Shutdown, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }
            }
        }

        private static void WriteResponse(LanguageServiceEnvelope response)
        {
            Console.Out.WriteLine(ManualJson.Serialize(response));
            Console.Out.Flush();
        }
    }
}
