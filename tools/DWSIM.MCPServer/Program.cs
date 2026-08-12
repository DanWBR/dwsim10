using System;
using System.Globalization;
using System.IO;
using System.Threading;
using DWSIM.Automation;
using DWSIM.MCPServer.Rpc;
using DWSIM.MCPServer.Sessions;
using DWSIM.MCPServer.Tools;
using DWSIM.MCPServer.Tools.Flowsheet;
using DWSIM.MCPServer.Tools.Thermo;
using DWSIM.MCPServer.Tools.Streams;
using DWSIM.MCPServer.Tools.UnitOps;
using DWSIM.MCPServer.Tools.Solve;
using DWSIM.MCPServer.Tools.Graphics;
using DWSIM.MCPServer.Transport;

namespace DWSIM.MCPServer
{
    class Program
    {
        /// <summary>
        /// The real stdout writer, used exclusively for JSON-RPC responses.
        /// Console.Out is redirected to Null to prevent DWSIM engine output
        /// from corrupting the stdio protocol.
        /// </summary>
        internal static TextWriter StdoutWriter;

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Capture the real stdout before redirecting Console.Out.
            // DWSIM engine (Automation3, Thermodynamics, etc.) may write to Console.Out
            // during initialization and operations. Any stray output on stdout corrupts
            // the JSON-RPC stdio protocol and causes the MCP client to kill the process.
            StdoutWriter = Console.Out;
            Console.SetOut(TextWriter.Null);

            Console.Error.WriteLine($"[dwsim-mcp] Process started. PID={System.Diagnostics.Process.GetCurrentProcess().Id}");

            var mode = "stdio";
            int port = 5901;
            string token = null;
            string host = "localhost";

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--stdio":
                        mode = "stdio";
                        break;
                    case "--http":
                        mode = "http";
                        break;
                    case "--port":
                        if (i + 1 < args.Length) port = int.Parse(args[++i]);
                        break;
                    case "--token":
                        if (i + 1 < args.Length) token = args[++i];
                        break;
                    case "--host":
                        // Bind address for the HTTP transport. "localhost" (default) is loopback
                        // only; "0.0.0.0", "*" or "+" bind every interface, for a networked service.
                        if (i + 1 < args.Length) host = args[++i];
                        break;
                }
            }

            Console.Error.WriteLine("[dwsim-mcp] Initializing DWSIM automation engine...");
            DWSIM.Automation.FluentAPI.Flowsheet.RegisterAssemblyResolver();

            Console.Error.WriteLine("[dwsim-mcp] Loading compounds and property packages...");
            var automation = new Automation3();

            var sessions = new SessionManager();
            var registry = new ToolRegistry();

            registry.RegisterToolsFrom(new FlowsheetTools(sessions));
            registry.RegisterToolsFrom(new ThermoTools(sessions, automation));
            registry.RegisterToolsFrom(new StreamTools(sessions));
            registry.RegisterToolsFrom(new UnitOpTools(sessions));
            registry.RegisterToolsFrom(new SolveTools(sessions));
            registry.RegisterToolsFrom(new GraphicTools(sessions));

            var dispatcher = new JsonRpcDispatcher(registry);

            Console.Error.WriteLine($"[dwsim-mcp] Registered {registry.ListTools().Count} tools");

            ITransport transport;
            switch (mode)
            {
                case "http":
                    transport = new HttpSseTransport(dispatcher, port, token, host);
                    break;
                default:
                    transport = new StdioTransport(dispatcher);
                    break;
            }

            transport.Run();
        }
    }
}
