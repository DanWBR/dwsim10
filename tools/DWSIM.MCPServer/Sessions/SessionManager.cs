using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DWSIM.Automation.FluentAPI;

namespace DWSIM.MCPServer.Sessions
{
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, FlowsheetSession> _sessions =
            new ConcurrentDictionary<string, FlowsheetSession>();

        public string CreateFlowsheet(string name = null)
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 12);
            Console.Error.WriteLine($"[dwsim-mcp] Creating flowsheet '{name ?? id}' with handle {id}...");

            // Create our custom McpFlowsheet (based on Flowsheet3 with FlowsheetSolver2)
            var mcpFs = new McpFlowsheet();
            mcpFs.Init();
            if (!string.IsNullOrEmpty(name))
                mcpFs.FlowsheetOptions.SimulationName = name;

            // Wrap with FluentAPI to get builder methods (AddMixer, WithCompounds, etc.)
            var fluent = Flowsheet.Wrap(mcpFs);

            _sessions[id] = new FlowsheetSession
            {
                Flowsheet = fluent,
                McpFlowsheet = mcpFs,
                CreatedAt = DateTime.UtcNow
            };

            Console.Error.WriteLine($"[dwsim-mcp] Flowsheet {id} created. Active sessions: {_sessions.Count}");
            return id;
        }

        public string LoadFlowsheet(string filepath)
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 12);
            Console.Error.WriteLine($"[dwsim-mcp] Loading flowsheet from '{filepath}' with handle {id}...");

            // Load via FluentAPI (uses Automation3 internally)
            var fluent = Flowsheet.Load(filepath);

            _sessions[id] = new FlowsheetSession
            {
                Flowsheet = fluent,
                McpFlowsheet = null, // loaded flowsheets use Automation3's Flowsheet2 internally
                CreatedAt = DateTime.UtcNow,
                FilePath = filepath
            };

            Console.Error.WriteLine($"[dwsim-mcp] Flowsheet {id} loaded. Active sessions: {_sessions.Count}");
            return id;
        }

        public Flowsheet GetFlowsheet(string id)
        {
            if (_sessions.TryGetValue(id, out var session))
            {
                session.LastAccessedAt = DateTime.UtcNow;
                return session.Flowsheet;
            }
            throw new InvalidOperationException($"Flowsheet not found: {id}");
        }

        /// <summary>
        /// Gets the McpFlowsheet if available (for created flowsheets), or null (for loaded).
        /// </summary>
        public McpFlowsheet GetMcpFlowsheet(string id)
        {
            if (_sessions.TryGetValue(id, out var session))
                return session.McpFlowsheet;
            return null;
        }

        public bool CloseFlowsheet(string id)
        {
            Console.Error.WriteLine($"[dwsim-mcp] Closing flowsheet {id}");
            return _sessions.TryRemove(id, out _);
        }

        public List<FlowsheetInfo> ListFlowsheets()
        {
            return _sessions.Select(kv => new FlowsheetInfo
            {
                Id = kv.Key,
                CreatedAt = kv.Value.CreatedAt,
                LastAccessedAt = kv.Value.LastAccessedAt,
                FilePath = kv.Value.FilePath
            }).ToList();
        }

        public object WithLock(string id, Func<Flowsheet, object> action)
        {
            if (!_sessions.TryGetValue(id, out var session))
                throw new InvalidOperationException($"Flowsheet not found: {id}");

            lock (session.SolveLock)
            {
                session.LastAccessedAt = DateTime.UtcNow;
                return action(session.Flowsheet);
            }
        }

        private class FlowsheetSession
        {
            public Flowsheet Flowsheet { get; set; }
            public McpFlowsheet McpFlowsheet { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime LastAccessedAt { get; set; }
            public string FilePath { get; set; }
            public readonly object SolveLock = new object();
        }
    }

    public class FlowsheetInfo
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessedAt { get; set; }
        public string FilePath { get; set; }
    }
}
