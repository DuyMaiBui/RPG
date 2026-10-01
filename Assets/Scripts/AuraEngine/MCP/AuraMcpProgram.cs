using System;

namespace AuraEngine.MCP
{
    public static class AuraMcpProgram
    {
        public static void Main(string[] args)
        {
            var server = new AuraMcpServer();
            server.Run(Console.In, Console.Out);
        }
    }
}
