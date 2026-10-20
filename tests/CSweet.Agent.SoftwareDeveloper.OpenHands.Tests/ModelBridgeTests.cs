using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CSweet.Agent.OpenHands;
using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.SoftwareDeveloper.OpenHands.Tests;

public sealed class ModelBridgeTests
{
    private static readonly AsyncLocal<string?> Scope = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BridgePreservesWorkScopeToolIdentityOptionsAndUsage(bool stream)
    {
        Scope.Value = "authorized-work";
        var client = new ScriptedClient((messages, options) =>
        {
            Assert.Equal("authorized-work", Scope.Value);
            Assert.Equal("call-1", Assert.IsType<FunctionResultContent>(messages.Last().Contents.Single()).CallId);
            Assert.Equal(123, options!.MaxOutputTokens);
            Assert.Equal(ReasoningEffort.High, options.Reasoning!.Effort);
            Assert.Equal("file_editor", Assert.Single(options.Tools!).Name);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, [new TextContent("ready"),
                new FunctionCallContent("call-2", "file_editor", new Dictionary<string, object?> { ["path"] = "x" })]));
        });
        await using var bridge = await ModelBridge.StartAsync(client, 500, default);
        Scope.Value = "another-work";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bridge.Token);
        var body = JsonSerializer.Serialize(new { stream, max_tokens=123, reasoning_effort="high", messages=new object[] {
            new { role="assistant", content=(string?)null, tool_calls=new[]{new { id="call-1", type="function", function=new{name="file_editor",arguments="{}"}}}},
            new { role="tool", tool_call_id="call-1", content="done" }
        }, tools = new[]{ new { type="function", function=new { name="file_editor",description="edit",parameters=new{type="object",properties=new{path=new{type="string"}}}}}} });
        using var response = await http.PostAsync(bridge.BaseUrl + "/chat/completions", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadAsStringAsync();
        Assert.Contains("call-2", result);
        Assert.Contains("ready", result);
        Assert.Contains("tool_calls", result);
        Assert.Equal(stream, result.Contains("[DONE]"));
    }

    [Fact]
    public async Task MissingBridgeCredentialCannotInvokeModel()
    {
        await using var bridge = await ModelBridge.StartAsync(new ScriptedClient((_, _) => throw new Exception("Must not be called")), 100, default);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await http.GetAsync(bridge.BaseUrl+"/models")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderAndBudgetFailuresStopTheRequestWithoutExposingDiagnostics(bool exhausted)
    {
        Exception failure=exhausted
            ? new PlatformCapabilityException("llm.chat.stream.v1",PlatformCapabilityErrorCode.Denied,"private budget context",failureCode:"budget_exhausted")
            : new HttpRequestException("private provider credential context");
        await using var bridge=await ModelBridge.StartAsync(new ScriptedClient((_,_)=>throw failure),100,default);
        using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(10)};
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",bridge.Token);
        using var response=await http.PostAsync(bridge.BaseUrl+"/chat/completions",new StringContent("{\"messages\":[{\"role\":\"user\",\"content\":\"work\"}]}",Encoding.UTF8,"application/json"));
        Assert.Equal(System.Net.HttpStatusCode.BadGateway,response.StatusCode);
        Assert.DoesNotContain("private",await response.Content.ReadAsStringAsync());
        Assert.Same(failure,bridge.Failure);
    }

    [Fact]
    public async Task CallbackCancellationReachesAnInFlightProviderRequest()
    {
        using var cancel=new CancellationTokenSource();
        var client=new WaitingClient();
        await using var bridge=await ModelBridge.StartAsync(client,100,cancel.Token);
        using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(10)};
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",bridge.Token);
        var pending=http.PostAsync(bridge.BaseUrl+"/chat/completions",new StringContent("{\"messages\":[{\"role\":\"user\",\"content\":\"work\"}]}",Encoding.UTF8,"application/json"));
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancel.CancelAsync();
        await client.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { using var response=await pending; Assert.False(response.IsSuccessStatusCode); }
        catch(HttpRequestException) { /* The cancelled callback may abort the local HTTP connection. */ }
    }

    private sealed class WaitingClient : IChatClient
    {
        internal TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,ChatOptions? options=null,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,ChatOptions? options=null,[EnumeratorCancellation] CancellationToken cancellationToken=default)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite,cancellationToken); }
            finally { Cancelled.TrySetResult(); }
            yield break;
        }
        public object? GetService(Type serviceType,object? serviceKey=null)=>null;
        public void Dispose() { }
    }

    internal sealed class ScriptedClient(Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> response) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(response(messages.ToArray(), options));
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = response(messages.ToArray(), options);
            yield return new(ChatRole.Assistant, result.Messages.SelectMany(x => x.Contents).ToList());
            yield return new(ChatRole.Assistant, [new UsageContent(new() { InputTokenCount=11, OutputTokenCount=7 })]) { FinishReason=ChatFinishReason.Stop };
            await Task.CompletedTask;
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() { }
    }
}
