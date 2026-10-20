using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CSweet.Agent.OpenHands;

/// <summary>Local OpenAI-compatible transport; all inference remains on the public SDK IChatClient.</summary>
internal sealed class ModelBridge : IAsyncDisposable
{
    private readonly WebApplication _app;
    internal string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    internal string BaseUrl { get; private set; } = "";
    private readonly SemaphoreSlim _calls = new(1, 1);
    private readonly IChatClient _client;
    internal Exception? Failure { get; private set; }
    private readonly int _outputTokens;
    private readonly CancellationToken _workToken;
    private readonly ExecutionContext? _callbackContext = ExecutionContext.Capture();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ModelBridge(IChatClient client, int outputTokens, CancellationToken token)
    {
        _client = client; _outputTokens = outputTokens; _workToken = token;
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Listen(IPAddress.Loopback, 0);
            server.Limits.MaxRequestBodySize = 4 * 1024 * 1024;
        });
        _app = builder.Build();
        _app.Use(async (context, next) =>
        {
            var actual = Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString());
            var expected = Encoding.UTF8.GetBytes("Bearer " + Token);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected)) { context.Response.StatusCode = 401; return; }
            await next(context);
        });
        _app.MapGet("/v1/models", () => new { @object = "list", data = new[] { new { id = "csweet-broker", @object = "model", owned_by = "csweet" } } });
        _app.MapPost("/v1/chat/completions", (HttpContext http) =>
        {
            // SDK clients require the originating async work scope even on Kestrel request threads.
            Task completion = Task.CompletedTask;
            if (_callbackContext is null) return CompleteAsync(http);
            ExecutionContext.Run(_callbackContext.CreateCopy(), _ => completion = CompleteAsync(http), null);
            return completion;
        });
    }

    internal static async Task<ModelBridge> StartAsync(IChatClient client, int outputTokens, CancellationToken token)
    {
        var bridge = new ModelBridge(client, outputTokens, token);
        await bridge._app.StartAsync(token);
        bridge.BaseUrl = bridge._app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single() + "/v1";
        return bridge;
    }

    private async Task CompleteAsync(HttpContext http)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_workToken, http.RequestAborted);
        var token = linked.Token;
        await _calls.WaitAsync(token);
        try
        {
            using var body = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: token);
            var input = body.RootElement;
            var messages = ParseMessages(input.GetProperty("messages"));
            var requestedTokens = input.TryGetProperty("max_completion_tokens", out var max) || input.TryGetProperty("max_tokens", out max)
                ? max.GetInt32() : _outputTokens;
            var options = new ChatOptions { MaxOutputTokens = Math.Clamp(requestedTokens, 1, _outputTokens) };
            if (input.TryGetProperty("temperature", out var temperature) && temperature.ValueKind == JsonValueKind.Number)
                options.Temperature = temperature.GetSingle();
            if (input.TryGetProperty("top_p", out var topP) && topP.ValueKind == JsonValueKind.Number) options.TopP = topP.GetSingle();
            if (input.TryGetProperty("stop", out var stop)) options.StopSequences = stop.ValueKind == JsonValueKind.String ? [stop.GetString()!] : stop.EnumerateArray().Select(x => x.GetString()!).ToList();
            if (input.TryGetProperty("tools", out var tools)) options.Tools = tools.EnumerateArray().Select(x => (AITool)new SchemaFunction(x.GetProperty("function"))).ToList();
            if (input.TryGetProperty("reasoning_effort", out var effort))
                options.Reasoning = new ReasoningOptions { Effort = effort.GetString() switch
                {
                    "none" => ReasoningEffort.None, "low" => ReasoningEffort.Low, "medium" => ReasoningEffort.Medium,
                    "high" => ReasoningEffort.High, "xhigh" => ReasoningEffort.ExtraHigh,
                    _ => throw new ArgumentException("Unsupported reasoning effort.")
                }, Output = ReasoningOutput.Summary };
            if (input.TryGetProperty("tool_choice", out var choice)) options.ToolMode = choice.ValueKind == JsonValueKind.String
                ? choice.GetString() switch { "none" => ChatToolMode.None, "required" => ChatToolMode.RequireAny, _ => ChatToolMode.Auto }
                : ChatToolMode.RequireSpecific(choice.GetProperty("function").GetProperty("name").GetString()!);
            if (input.TryGetProperty("response_format", out var format))
            {
                var type = format.GetProperty("type").GetString();
                if (type == "json_object") options.ResponseFormat = ChatResponseFormat.Json;
                else if (type == "json_schema")
                {
                    var schema = format.GetProperty("json_schema");
                    options.ResponseFormat = ChatResponseFormat.ForJsonSchema(schema.GetProperty("schema"), schema.GetProperty("name").GetString());
                }
            }
            var stream = input.TryGetProperty("stream", out var streaming) && streaming.GetBoolean();
            var id = "chatcmpl-" + Guid.NewGuid().ToString("N");
            var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var text = new StringBuilder();
            var reasoning = new StringBuilder();
            var calls = new List<object>();
            var callIndexes = new Dictionary<string, int>();
            long inputs = 0, outputs = 0;
            var finish = "stop";
            if (stream) http.Response.ContentType = "text/event-stream";
            await foreach (var update in _client.GetStreamingResponseAsync(messages, options, token))
            {
                foreach (var content in update.Contents)
                {
                    object? delta = null;
                    switch (content)
                    {
                        case TextContent value:
                            text.Append(value.Text); delta = new { content = value.Text }; break;
                        case TextReasoningContent value:
                            reasoning.Append(value.Text); delta = new { reasoning_content = value.Text }; break;
                        case FunctionCallContent call:
                            if (!callIndexes.TryGetValue(call.CallId, out var index)) callIndexes.Add(call.CallId, index = callIndexes.Count);
                            var function = new { name = call.Name, arguments = JsonSerializer.Serialize(call.Arguments ?? new Dictionary<string, object?>(), Json) };
                            calls.Add(new { id = call.CallId, type = "function", function });
                            delta = new { tool_calls = new[] { new { index, id = call.CallId, type = "function", function } } };
                            finish = "tool_calls";
                            break;
                        case UsageContent usage:
                            inputs += usage.Details.InputTokenCount ?? 0; outputs += usage.Details.OutputTokenCount ?? 0; break;
                    }
                    if (stream && delta is not null)
                        await WriteEventAsync(http, new { id, @object = "chat.completion.chunk", created, model = "csweet-broker", choices = new[] { new { index = 0, delta, finish_reason = (string?)null } } }, token);
                }
                if (update.FinishReason == ChatFinishReason.Length) finish = "length";
                else if (update.FinishReason == ChatFinishReason.ContentFilter) finish = "content_filter";
            }
            var usageResult = new { prompt_tokens = inputs, completion_tokens = outputs, total_tokens = inputs + outputs };
            if (stream)
            {
                await WriteEventAsync(http, new { id, @object = "chat.completion.chunk", created, model = "csweet-broker", choices = new[] { new { index = 0, delta = new { }, finish_reason = finish } }, usage = usageResult }, token);
                await http.Response.WriteAsync("data: [DONE]\n\n", token);
            }
            else
                await http.Response.WriteAsJsonAsync(new { id, @object = "chat.completion", created, model = "csweet-broker", choices = new[] {
                    new { index = 0, message = new { role = "assistant", content = text.Length == 0 ? null : text.ToString(),
                        reasoning_content = reasoning.Length == 0 ? null : reasoning.ToString(), tool_calls = calls.Count == 0 ? null : calls }, finish_reason = finish }
                }, usage = usageResult }, token);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Failure=error;
            if (!http.Response.HasStarted)
            {
                http.Response.StatusCode = error is JsonException or ArgumentException ? 400 : 502;
                await http.Response.WriteAsJsonAsync(new { error = new { message = "The governed model request failed. Check C-Sweet's inference diagnostics.", type = "csweet_model_error" } }, token);
            }
            else http.Abort();
            // No upstream error text, request body or bridge credential is written to logs.
        }
        finally { _calls.Release(); }
    }

    internal static List<ChatMessage> ParseMessages(JsonElement source)
    {
        var messages = new List<ChatMessage>();
        foreach (var item in source.EnumerateArray())
        {
            var role = item.GetProperty("role").GetString()!;
            var message = new ChatMessage(new ChatRole(role), new List<AIContent>());
            if(item.TryGetProperty("name",out var name) && name.ValueKind==JsonValueKind.String) message.AuthorName=name.GetString();
            if(item.TryGetProperty("reasoning_content",out var reasoning) && reasoning.ValueKind==JsonValueKind.String)
                message.Contents.Add(new TextReasoningContent(reasoning.GetString()!));
            if (item.TryGetProperty("content", out var content) && content.ValueKind != JsonValueKind.Null)
            {
                if (content.ValueKind == JsonValueKind.String) message.Contents.Add(new TextContent(content.GetString()!));
                else foreach (var part in content.EnumerateArray())
                {
                    if (part.GetProperty("type").GetString() != "text") throw new ArgumentException("Only text content is enabled in this pilot.");
                    message.Contents.Add(new TextContent(part.GetProperty("text").GetString()!));
                }
            }
            if (item.TryGetProperty("tool_calls", out var calls))
                foreach (var call in calls.EnumerateArray())
                {
                    var function = call.GetProperty("function");
                    var arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(function.GetProperty("arguments").GetString()!, Json);
                    message.Contents.Add(new FunctionCallContent(call.GetProperty("id").GetString()!, function.GetProperty("name").GetString()!, arguments));
                }
            if (role == "tool")
            {
                var result = string.Concat(message.Contents.OfType<TextContent>().Select(x => x.Text));
                message.Contents.Clear();
                message.Contents.Add(new FunctionResultContent(item.GetProperty("tool_call_id").GetString()!, result));
            }
            messages.Add(message);
        }
        return messages;
    }

    private static async Task WriteEventAsync(HttpContext http, object value, CancellationToken token)
    {
        await http.Response.WriteAsync("data: " + JsonSerializer.Serialize(value, Json) + "\n\n", token);
        await http.Response.Body.FlushAsync(token);
    }

    private sealed class SchemaFunction(JsonElement function) : AIFunction
    {
        private readonly JsonElement _function = function.Clone();
        public override string Name => _function.GetProperty("name").GetString()!;
        public override string Description => _function.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        public override JsonElement JsonSchema => _function.GetProperty("parameters");
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => throw new InvalidOperationException("OpenHands owns tool execution; nested model tool execution is unavailable.");
    }

    public async ValueTask DisposeAsync() { await _app.StopAsync(CancellationToken.None); await _app.DisposeAsync(); _calls.Dispose(); }
}
