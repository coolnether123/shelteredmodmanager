using System.Text;
using System.Text.Json;
using Cortex.LanguageService.Protocol;

namespace Cortex.Roslyn.Worker
{
    internal static class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            IncludeFields = true
        };

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
                    request = JsonSerializer.Deserialize<LanguageServiceEnvelope>(line, JsonOptions) ?? new LanguageServiceEnvelope();
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
            Console.Out.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
            Console.Out.Flush();
        }
    }
}
