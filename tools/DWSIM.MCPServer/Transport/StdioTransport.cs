using System;
using System.IO;
using DWSIM.MCPServer.Rpc;

namespace DWSIM.MCPServer.Transport
{
    public class StdioTransport : ITransport
    {
        private readonly JsonRpcDispatcher _dispatcher;
        private readonly TextWriter _stdout;

        public StdioTransport(JsonRpcDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            // Use the real stdout captured before Console.Out was redirected to Null.
            _stdout = Program.StdoutWriter;
        }

        public void Run()
        {
            Console.Error.WriteLine("[dwsim-mcp] Stdio transport ready");

            using (var reader = new StreamReader(Console.OpenStandardInput()))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    Console.Error.WriteLine($"[dwsim-mcp] << {(line.Length > 200 ? line.Substring(0, 200) + "..." : line)}");

                    var response = _dispatcher.HandleMessage(line);
                    if (response != null)
                    {
                        Console.Error.WriteLine($"[dwsim-mcp] >> {(response.Length > 200 ? response.Substring(0, 200) + "..." : response)}");
                        _stdout.WriteLine(response);
                        _stdout.Flush();
                    }
                }
            }

            Console.Error.WriteLine("[dwsim-mcp] Stdin closed, exiting.");
        }
    }
}
