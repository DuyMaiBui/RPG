using System;
using System.Collections.Generic;
using System.IO;
using AuraEngine.Core;

namespace AuraEngine.MCP
{
    public sealed class AuraMcpServer
    {
        private const string ProtocolVersion = "2024-11-05";
        private readonly AuraMcpSession _session = new AuraMcpSession();

        public void Run(TextReader input, TextWriter output)
        {
            string line;
            while ((line = input.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var response = HandleLine(line);
                if (response == null)
                    continue;

                output.WriteLine(response.ToJson());
                output.Flush();
            }
        }

        public AuraJsonValue HandleLine(string line)
        {
            AuraJsonValue request;
            try
            {
                request = AuraJsonValue.Parse(line);
            }
            catch (Exception exception)
            {
                return Error(AuraJsonValue.Null, -32700, "Parse error: " + exception.Message);
            }

            var method = request["method"].AsString();
            var id = request["id"];
            var isNotification = !request.Has("id");
            if (method.StartsWith("notifications/", StringComparison.Ordinal) || isNotification)
                return null;

            switch (method)
            {
                case "initialize":
                    return Result(id, AuraJsonValue.Obj(
                        ("protocolVersion", AuraJsonValue.String(ProtocolVersion)),
                        ("capabilities", AuraJsonValue.Obj(("tools", AuraJsonValue.Obj()))),
                        ("serverInfo", AuraJsonValue.Obj(
                            ("name", AuraJsonValue.String("AuraEngine")),
                            ("version", AuraJsonValue.String("0.1.0"))))));
                case "ping":
                    return Result(id, AuraJsonValue.Obj());
                case "tools/list":
                    return Result(id, AuraJsonValue.Obj(("tools", ToolSchemas())));
                case "resources/list":
                    return Result(id, AuraJsonValue.Obj(("resources", AuraJsonValue.Arr(
                        AuraJsonValue.Obj(("uri", AuraJsonValue.String("aura://architecture")), ("name", AuraJsonValue.String("AuraEngine architecture"))),
                        AuraJsonValue.Obj(("uri", AuraJsonValue.String("aura://status")), ("name", AuraJsonValue.String("AuraEngine status")))))));
                case "resources/read":
                    return ReadResource(id, request["params"]["uri"].AsString());
                case "prompts/list":
                    return Result(id, AuraJsonValue.Obj(("prompts", AuraJsonValue.Arr(
                        AuraJsonValue.Obj(("name", AuraJsonValue.String("inspect_world")), ("description", AuraJsonValue.String("Inspect the current AuraEngine world state."))),
                        AuraJsonValue.Obj(("name", AuraJsonValue.String("run_determinism_check")), ("description", AuraJsonValue.String("Run a deterministic fixed-tick state check.")))))));
                case "prompts/get":
                    return GetPrompt(id, request["params"]["name"].AsString());
                case "tools/call":
                    return CallTool(id, request["params"]);
                default:
                    return Error(id, -32601, "Method not found: " + method);
            }
        }

        private AuraJsonValue CallTool(AuraJsonValue id, AuraJsonValue parameters)
        {
            var name = parameters["name"].AsString();
            var arguments = parameters["arguments"];
            try
            {
                var payload = Dispatch(name, arguments);
                return Result(id, AuraJsonValue.Obj(
                    ("content", AuraJsonValue.Arr(AuraJsonValue.Obj(
                        ("type", AuraJsonValue.String("text")),
                        ("text", AuraJsonValue.String(payload.ToJson()))))),
                    ("isError", AuraJsonValue.Bool(false))));
            }
            catch (Exception exception)
            {
                return Result(id, AuraJsonValue.Obj(
                    ("content", AuraJsonValue.Arr(AuraJsonValue.Obj(
                        ("type", AuraJsonValue.String("text")),
                        ("text", AuraJsonValue.String(exception.Message))))),
                    ("isError", AuraJsonValue.Bool(true))));
            }
        }

        private static AuraJsonValue ReadResource(AuraJsonValue id, string uri)
        {
            var text = uri switch
            {
                "aura://architecture" => "Physics runs in the native Jolt/Box2D C++ kernel; gameplay orchestration remains C#.",
                "aura://status" => "Use world_state and the standalone kernel test runner for validation.",
                _ => throw new InvalidOperationException("Unknown resource: " + uri),
            };
            return Result(id, AuraJsonValue.Obj(("contents", AuraJsonValue.Arr(
                AuraJsonValue.Obj(("uri", AuraJsonValue.String(uri)), ("mimeType", AuraJsonValue.String("text/plain")), ("text", AuraJsonValue.String(text)))))));
        }

        private static AuraJsonValue GetPrompt(AuraJsonValue id, string name)
        {
            var text = name switch
            {
                "inspect_world" => "Call world_state, then inspect the reported body count, tick and state hash.",
                "run_determinism_check" => "Run the standalone kernel test suite and compare repeated state hashes.",
                _ => throw new InvalidOperationException("Unknown prompt: " + name),
            };
            return Result(id, AuraJsonValue.Obj(("description", AuraJsonValue.String(text)), ("messages", AuraJsonValue.Arr(
                AuraJsonValue.Obj(("role", AuraJsonValue.String("user")), ("content", AuraJsonValue.Obj(("type", AuraJsonValue.String("text")), ("text", AuraJsonValue.String(text)))))))));
        }

        private AuraJsonValue Dispatch(string name, AuraJsonValue arguments)
        {
            switch (name)
            {
                case "world_create":
                    _session.CreateWorld();
                    return AuraJsonValue.Obj(("tick", AuraJsonValue.Number(_session.Tick)));
                case "body_create_box":
                    _session.AddBox(
                        Vec(arguments, "position"),
                        Vec(arguments, "halfExtents"),
                        arguments["dynamic"].AsBool());
                    return AuraJsonValue.Obj(("bodyCount", AuraJsonValue.Number(_session.BodyCount())));
                case "body_create_sphere":
                    _session.AddSphere(
                        Vec(arguments, "position"),
                        arguments["radius"].AsFloat(),
                        arguments["dynamic"].AsBool());
                    return AuraJsonValue.Obj(("bodyCount", AuraJsonValue.Number(_session.BodyCount())));
                case "world_step":
                    var count = Math.Max(1, arguments["count"].AsInt());
                    _session.Step(count);
                    return AuraJsonValue.Obj(
                        ("tick", AuraJsonValue.Number(_session.Tick)),
                        ("stateHash", AuraJsonValue.String(_session.StateHash().ToString("x16"))));
                case "world_raycast":
                    var hit = _session.Raycast(
                        Vec(arguments, "origin"),
                        Vec(arguments, "direction"),
                        arguments["maxDistance"].AsFloat(),
                        out var queryHit);
                    return AuraJsonValue.Obj(
                        ("hit", AuraJsonValue.Bool(hit)),
                        ("distance", AuraJsonValue.Number(queryHit.Distance)),
                        ("point", VecValue(queryHit.Point)));
                case "world_state":
                    return AuraJsonValue.Obj(
                        ("tick", AuraJsonValue.Number(_session.Tick)),
                        ("bodyCount", AuraJsonValue.Number(_session.BodyCount())),
                        ("stateHash", AuraJsonValue.String(_session.StateHash().ToString("x16"))));
                default:
                    throw new InvalidOperationException("Unknown tool: " + name);
            }
        }

        private static AuraVector3 Vec(AuraJsonValue arguments, string key)
        {
            var value = arguments[key];
            if (value.Kind == AuraJsonKind.Object)
                return new AuraVector3(value["x"].AsFloat(), value["y"].AsFloat(), value["z"].AsFloat());

            if (value.Kind == AuraJsonKind.Array)
                return new AuraVector3(value[0].AsFloat(), value[1].AsFloat(), value[2].AsFloat());

            return AuraVector3.Zero;
        }

        private static AuraJsonValue VecValue(AuraVector3 value) => AuraJsonValue.Obj(
            ("x", AuraJsonValue.Number(value.X)),
            ("y", AuraJsonValue.Number(value.Y)),
            ("z", AuraJsonValue.Number(value.Z)));

        private static AuraJsonValue Result(AuraJsonValue id, AuraJsonValue result) => AuraJsonValue.Obj(
            ("jsonrpc", AuraJsonValue.String("2.0")),
            ("id", id),
            ("result", result));

        private static AuraJsonValue Error(AuraJsonValue id, int code, string message) => AuraJsonValue.Obj(
            ("jsonrpc", AuraJsonValue.String("2.0")),
            ("id", id),
            ("error", AuraJsonValue.Obj(
                ("code", AuraJsonValue.Number(code)),
                ("message", AuraJsonValue.String(message)))));

        private static AuraJsonValue ToolSchemas()
        {
            var tools = new List<AuraJsonValue>
            {
                Tool("world_create", "Create a fresh managed physics world.", AuraJsonValue.Obj()),
                Tool("body_create_box", "Add an axis-aligned box body.",
                    AuraJsonValue.Obj(
                        ("position", VecSchema("Box center.")),
                        ("halfExtents", VecSchema("Box half extents.")),
                        ("dynamic", AuraJsonValue.Obj(("type", AuraJsonValue.String("boolean")))))),
                Tool("body_create_sphere", "Add a sphere body.",
                    AuraJsonValue.Obj(
                        ("position", VecSchema("Sphere center.")),
                        ("radius", AuraJsonValue.Obj(("type", AuraJsonValue.String("number")))),
                        ("dynamic", AuraJsonValue.Obj(("type", AuraJsonValue.String("boolean")))))),
                Tool("world_step", "Advance the simulation by a number of fixed ticks.",
                    AuraJsonValue.Obj(("count", AuraJsonValue.Obj(("type", AuraJsonValue.String("integer")))))),
                Tool("world_raycast", "Cast a ray and report the first hit.",
                    AuraJsonValue.Obj(
                        ("origin", VecSchema("Ray origin.")),
                        ("direction", VecSchema("Ray direction.")),
                        ("maxDistance", AuraJsonValue.Obj(("type", AuraJsonValue.String("number")))))),
                Tool("world_state", "Report tick, body count and state hash.", AuraJsonValue.Obj()),
            };

            return AuraJsonValue.Arr(tools.ToArray());
        }

        private static AuraJsonValue Tool(string name, string description, AuraJsonValue properties) => AuraJsonValue.Obj(
            ("name", AuraJsonValue.String(name)),
            ("description", AuraJsonValue.String(description)),
            ("inputSchema", AuraJsonValue.Obj(
                ("type", AuraJsonValue.String("object")),
                ("properties", properties))));

        private static AuraJsonValue VecSchema(string description) => AuraJsonValue.Obj(
            ("type", AuraJsonValue.String("object")),
            ("description", AuraJsonValue.String(description)),
            ("properties", AuraJsonValue.Obj(
                ("x", AuraJsonValue.Obj(("type", AuraJsonValue.String("number")))),
                ("y", AuraJsonValue.Obj(("type", AuraJsonValue.String("number")))),
                ("z", AuraJsonValue.Obj(("type", AuraJsonValue.String("number")))))));
    }
}
