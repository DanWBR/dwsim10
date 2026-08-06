using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using DWSIM.MCPServer.Rpc;

namespace DWSIM.MCPServer.Transport
{
    public class HttpSseTransport : ITransport
    {
        private readonly JsonRpcDispatcher _dispatcher;
        private readonly int _port;
        private readonly string _token;

        public HttpSseTransport(JsonRpcDispatcher dispatcher, int port, string token = null)
        {
            _dispatcher = dispatcher;
            _port = port;
            _token = token;
        }

        public void Run()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{_port}/");
            listener.Start();
            Console.Error.WriteLine($"[dwsim-mcp] HTTP transport listening on http://localhost:{_port}/");

            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }

                ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                // CORS headers for cross-platform UI clients (browser, Electron, etc.)
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-MCP-Token, Authorization");

                // Handle CORS preflight
                if (ctx.Request.HttpMethod == "OPTIONS")
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.ContentLength64 = 0;
                    ctx.Response.OutputStream.Close();
                    return;
                }

                // Token auth (only when --token is set)
                if (!string.IsNullOrEmpty(_token))
                {
                    var provided = ctx.Request.Headers["X-MCP-Token"]
                                ?? ctx.Request.Headers["Authorization"]?.Replace("Bearer ", "");
                    if (provided != _token)
                    {
                        ctx.Response.StatusCode = 401;
                        WriteText(ctx.Response, "{\"error\":\"Unauthorized. Provide token via X-MCP-Token header or Authorization: Bearer <token>\"}");
                        return;
                    }
                }

                if (ctx.Request.HttpMethod == "POST" && ctx.Request.Url.AbsolutePath == "/mcp")
                {
                    HandleJsonRpc(ctx);
                }
                else if (ctx.Request.HttpMethod == "GET" && ctx.Request.Url.AbsolutePath == "/sse")
                {
                    HandleSse(ctx);
                }
                else if (ctx.Request.HttpMethod == "GET" && ctx.Request.Url.AbsolutePath == "/health")
                {
                    ctx.Response.StatusCode = 200;
                    var tokenStatus = string.IsNullOrEmpty(_token) ? "disabled" : "enabled";
                    WriteText(ctx.Response, $"{{\"status\":\"ok\",\"auth\":\"{tokenStatus}\"}}");
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    WriteText(ctx.Response, "{\"error\":\"Not found. Available endpoints: POST /mcp, GET /sse, GET /health\"}");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    ctx.Response.StatusCode = 500;
                    WriteText(ctx.Response, ex.Message);
                }
                catch { }
            }
        }

        private void HandleJsonRpc(HttpListenerContext ctx)
        {
            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            var response = _dispatcher.HandleMessage(body);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            WriteText(ctx.Response, response ?? "{}");
        }

        private void HandleSse(HttpListenerContext ctx)
        {
            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            ctx.Response.Headers.Add("Connection", "keep-alive");

            var postEndpoint = $"http://localhost:{_port}/mcp";
            var data = $"data: {{\"endpoint\":\"{postEndpoint}\"}}\n\n";
            var bytes = Encoding.UTF8.GetBytes($"event: endpoint\n{data}");

            try
            {
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.OutputStream.Flush();

                while (ctx.Response.OutputStream.CanWrite)
                {
                    Thread.Sleep(30000);
                    var keepalive = Encoding.UTF8.GetBytes(":keepalive\n\n");
                    ctx.Response.OutputStream.Write(keepalive, 0, keepalive.Length);
                    ctx.Response.OutputStream.Flush();
                }
            }
            catch { }
        }

        private static void WriteText(HttpListenerResponse response, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
    }
}
