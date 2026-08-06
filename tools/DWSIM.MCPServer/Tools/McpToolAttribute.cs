using System;

namespace DWSIM.MCPServer.Tools
{
    [AttributeUsage(AttributeTargets.Method)]
    public class McpToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }

        public McpToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class McpParamAttribute : Attribute
    {
        public string Description { get; }
        public bool Required { get; set; } = true;
        public string JsonType { get; set; }

        public McpParamAttribute(string description)
        {
            Description = description;
        }
    }
}
