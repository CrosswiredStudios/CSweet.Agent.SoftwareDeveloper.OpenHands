using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSweet.Agent.Development;
using CSweet.Agent.SDK;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.OpenHands;

public sealed record OpenHandsEngineOptions(string? PythonExecutable = null, string? RuntimeDirectory = null, string? WorkerDirectory = null)
{
    internal Action<string>? TestDiagnostics { get; init; }
}

/// <summary>One local worker per bounded turn. No C-Sweet transport or provider credentials cross this boundary.</summary>
public sealed class OpenHandsExecutionEngine(OpenHandsEngineOptions? options = null) : IDevelopmentExecutionEngine
{
    public const string EngineFormat = "openhands-1.56.0:csweet-1";
    private const int MaximumLineCharacters = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly OpenHandsEngineOptions _options = options ?? new();

    public async Task<DevelopmentExecutionResult> ExecuteAsync(DevelopmentExecutionRequest request, CancellationToken cancellationToken)
    {
        var worker = _options.WorkerDirectory ?? Path.Combine(AppContext.BaseDirectory, "worker");
        var runtime = _options.RuntimeDirectory ?? "/run/csweet/workload/tools/openhands";
        var python = _options.PythonExecutable ?? await BootstrapAsync(worker, runtime, request.Context, cancellationToken);
        var sessionHash = Hash(Encoding.UTF8.GetBytes(request.Context.InstallationId + ":" + request.SessionKey));
        var directory = Path.Combine(runtime, "sessions", sessionHash);
        Directory.CreateDirectory(directory);
        var retained = await request.Context.Platform.ExecutionCheckpoints.ReadAsync(new(request.SessionKey, EngineFormat), cancellationToken);
        var revision = retained?.Revision ?? 0;
        var cursor = retained?.EventCursor ?? 0;
        var progressCount=0;
        if (retained is not null)
        {
            if (retained.WorkspaceId != request.WorkspaceId || retained.AssignmentRevision != request.AssignmentRevision)
                throw new InvalidOperationException("The retained OpenHands source assignment changed.");
            Directory.Delete(directory, recursive: true);
            Directory.CreateDirectory(directory);
            RestorePrivateArchive(retained.EngineArchive, directory);
            if (retained.SourceArchive is { } archive)
                await RestoreSourceAsync(archive, request.WorkspacePath, cancellationToken);
            var metadata = Path.Combine(directory, "workspace-state");
            if (Directory.Exists(metadata))
            {
                Directory.CreateDirectory(Path.Combine(request.WorkspacePath, ".csweet"));
                foreach (var file in Directory.EnumerateFiles(metadata))
                    File.Copy(file, Path.Combine(request.WorkspacePath, ".csweet", Path.GetFileName(file)), overwrite:true);
            }
        }
        var tools = request.Options.Tools?.OfType<AIFunction>().ToDictionary(x => x.Name, StringComparer.Ordinal) ?? [];
        foreach (var tool in request.Context.Platform.Research.GetTools().OfType<AIFunction>()) tools.Add(tool.Name, tool);
        await using var bridge = await ModelBridge.StartAsync(request.ChatClient, request.OutputTokens, cancellationToken);
        var home = Path.Combine(runtime, "homes", sessionHash);
        Directory.CreateDirectory(home);
        using var process = Start(python, _options.TestDiagnostics is null ? ["-I", Path.Combine(worker, "worker.py")] :
            ["-I", Path.Combine(worker, "worker.py"), "--diagnostics"], request.WorkspacePath, home);
        var stderr = DrainAsync(process.StandardError, cancellationToken, _options.TestDiagnostics);
        try
        {
            await SendAsync(process, new
            {
                protocol = 1, session = sessionHash, directory, workspace = request.WorkspacePath,
                prompt = request.Prompt, instructions = request.Options.Instructions,
                baseUrl = bridge.BaseUrl, bridgeToken = bridge.Token,
                contextTokens = request.ContextTokens, outputTokens = request.OutputTokens,
                maximumIterations = request.MaximumIterations,
                tools = tools.Values.Select(x => new { name = x.Name, description = x.Description, schema = x.JsonSchema })
            }, cancellationToken);
            while (true)
            {
                var line = await ReadBoundedLineAsync(process.StandardOutput, cancellationToken)
                    ?? throw new InvalidOperationException("The OpenHands worker ended before completing its protocol.");
                using var message = JsonDocument.Parse(line);
                var data = message.RootElement;
                if (data.GetProperty("protocol").GetInt32() != 1) throw new InvalidDataException("Unsupported worker protocol.");
                var kind = data.GetProperty("kind").GetString();
                if (request.ShouldPause?.Invoke() == true)
                {
                    await SendAsync(process, new { protocol = 1, ok = false, stop = true }, cancellationToken);
                    return new("Waiting for the authorized ticket response.");
                }
                if (kind == "tool")
                {
                    var name = data.GetProperty("name").GetString()!;
                    if (!tools.TryGetValue(name, out var function)) throw new InvalidDataException("The worker requested an undeclared tool.");
                    var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(data.GetProperty("arguments"), Json)!;
                    object? result;
                    try { result = await function.InvokeAsync(new AIFunctionArguments(args), cancellationToken); }
                    catch (Exception error) when (error is ArgumentException)
                    {
                        result = new { error = error.Message }; // Input correction is safe; authority/runtime/checkpoint failures stop the worker.
                    }
                    await SendAsync(process, new { protocol = 1, ok = true, result }, cancellationToken);
                }
                else if (kind == "checkpoint")
                {
                    cursor++;
                    if (request.CheckpointSource is not null) await request.CheckpointSource(cancellationToken);
                    var engineArchive = CapturePrivateArchive(directory, request.WorkspacePath, bridge);
                    var source = await PlatformGitWorkspaceClient.CaptureSourceAsync(request.WorkspacePath, cancellationToken);
                    var saved = await request.Context.Platform.ExecutionCheckpoints.WriteAsync(new(request.SessionKey, EngineFormat,
                        revision, cursor, engineArchive, $"openhands:{sessionHash}:{cursor}:{Hash(engineArchive)}",
                        request.WorkspaceId, request.AssignmentRevision, source), cancellationToken);
                    revision = saved.Revision;
                    await SendAsync(process, new { protocol = 1, ok = true }, cancellationToken);
                }
                else if (kind == "gate")
                    await SendAsync(process, new { protocol = 1, ok = true }, cancellationToken);
                else if (kind == "progress")
                {
                    if(++progressCount<=100)
                    {
                        try { await request.Context.ReportProgressAsync(new { stage = "openhands", message = "OpenHands completed a workspace action.", operation = cursor }, cancellationToken); }
                        catch(Exception error) when(error is not OperationCanceledException) { /* Telemetry failure must not repeat a completed action. */ }
                    }
                    await SendAsync(process, new { protocol = 1, ok = true }, cancellationToken);
                }
                else if (kind == "completed")
                {
                    await process.WaitForExitAsync(cancellationToken);
                    if (process.ExitCode != 0) throw new InvalidOperationException("The OpenHands worker failed during finalization.");
                    return new(data.GetProperty("text").GetString() ?? "");
                }
                else throw new OperationalDevelopmentException("openhands.runtime.failed: The OpenHands execution engine could not complete this turn. " +
                    "Retained checkpoints were preserved. Review the runtime and selected model readiness before retrying.",bridge.Failure);
            }
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10)); }
            await stderr.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static async Task<string> BootstrapAsync(string worker, string runtime, AgentRuntimeContext context, CancellationToken token)
    {
        if (!OperatingSystem.IsLinux()) throw new OperationalDevelopmentException("Production OpenHands execution requires the certified Linux development environment.");
        var requirements = Path.Combine(worker, "requirements-linux.lock");
        var buildRequirements = Path.Combine(worker, "build-requirements.lock");
        var fingerprint = Hash(Encoding.UTF8.GetBytes(await File.ReadAllTextAsync(requirements,token)+await File.ReadAllTextAsync(buildRequirements,token)));
        var path = Path.Combine(runtime, "environments", fingerprint);
        var python = Path.Combine(path, "bin", "python");
        var ready = Path.Combine(path, ".ready");
        if (File.Exists(ready) && await File.ReadAllTextAsync(ready, token) == fingerprint) return python;
        await context.ReportProgressAsync(new { stage = "openhands-setup", message = "Preparing the pinned OpenHands runtime." }, token);
        Directory.CreateDirectory(path);
        await RunSetupAsync("python3.12", ["-m", "venv", path], path, token);
        await RunSetupAsync(python, ["-m", "pip", "install", "--disable-pip-version-check", "--require-hashes", "--only-binary=:all:", "-r", buildRequirements], path, token);
        await RunSetupAsync(python, ["-m", "pip", "install", "--disable-pip-version-check", "--require-hashes", "--no-build-isolation", "-r", requirements], path, token);
        await RunSetupAsync(python, ["-c", "import importlib.metadata as m; assert m.version('openhands-sdk') == '1.56.0'; assert m.version('openhands-tools') == '1.56.0'"], path, token);
        await File.WriteAllTextAsync(ready, fingerprint, token);
        return python;
    }

    private static async Task RunSetupAsync(string executable, IEnumerable<string> arguments, string cwd, CancellationToken token)
    {
        using var process = Start(executable, arguments, cwd);
        var stdout = DrainAsync(process.StandardOutput, token);
        var stderr = DrainAsync(process.StandardError, token);
        try
        {
            await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromMinutes(5), token);
            await Task.WhenAll(stdout, stderr);
            if (process.ExitCode != 0) throw new InvalidOperationException("OpenHands runtime setup failed. Confirm the approved public internet grant and runtime readiness.");
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    internal static Process Start(string executable, IEnumerable<string> arguments, string cwd, string? home = null)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = cwd, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        info.Environment.Clear();
        foreach (var key in new[] { "PATH", "SystemRoot", "TEMP", "TMP", "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY", "SSL_CERT_FILE", "SSL_CERT_DIR" })
            if (Environment.GetEnvironmentVariable(key) is { } value) info.Environment[key] = value;
        info.Environment["PYTHONUNBUFFERED"] = "1";
        info.Environment["PYTHONUTF8"] = "1";
        info.Environment["DO_NOT_TRACK"] = "1";
        info.Environment["OTEL_SDK_DISABLED"] = "true";
        info.Environment["LITELLM_LOCAL_MODEL_COST_MAP"] = "True";
        info.Environment["OPENHANDS_LOG_LEVEL"] = "ERROR";
        info.Environment["OPENHANDS_SUPPRESS_BANNER"] = "1";
        info.Environment["HOME"] = home ?? (OperatingSystem.IsLinux() ? "/run/csweet/workload/home" : Path.GetTempPath());
        if (OperatingSystem.IsWindows()) info.Environment["USERPROFILE"] = info.Environment["HOME"];
        info.Environment["PIP_CACHE_DIR"] = OperatingSystem.IsLinux() ? "/run/csweet/workload/cache/pip" : Path.GetTempPath();
        return Process.Start(info) ?? throw new InvalidOperationException("The OpenHands worker could not start.");
    }

    private static async Task SendAsync(Process process, object data, CancellationToken token)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(data, Json).AsMemory(), token);
        await process.StandardInput.FlushAsync(token);
    }

    internal static async Task<string?> ReadBoundedLineAsync(StreamReader reader, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[1];
        while (true)
        {
            var count = await reader.ReadAsync(buffer, token);
            if (count == 0) return result.Length == 0 ? null : result.ToString();
            if (buffer[0] == '\n') return result.ToString();
            if (result.Length >= MaximumLineCharacters) throw new InvalidDataException("Worker protocol frame exceeds its bound.");
            result.Append(buffer[0]);
        }
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken token, Action<string>? testDiagnostics = null)
    {
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer, token)) != 0) testDiagnostics?.Invoke(new string(buffer,0,count)); // Production never logs raw worker output.
    }

    private static byte[] CapturePrivateArchive(string directory, string workspace, ModelBridge bridge)
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in PrivateFiles(directory).Order())
            {
                if (new FileInfo(file).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Private state cannot contain links.");
                var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
                if (relative.Split('/').Any(x => x is "logs" or "__pycache__" or "workspace-state")) continue;
                var data = File.ReadAllBytes(file);
                if (file.EndsWith(".json", StringComparison.Ordinal))
                    data = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(data).Replace(bridge.Token, "redacted").Replace(bridge.BaseUrl, "http://127.0.0.1:1/v1"));
                using var output = zip.CreateEntry(relative).Open(); output.Write(data);
            }
            var metadata = Path.Combine(workspace, ".csweet");
            if (Directory.Exists(metadata)) foreach (var file in Directory.EnumerateFiles(metadata, "*.json"))
            {
                if (new FileInfo(file).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Workspace metadata cannot contain links.");
                using var output = zip.CreateEntry("workspace-state/" + Path.GetFileName(file)).Open(); output.Write(File.ReadAllBytes(file));
            }
        }
        if (bytes.Length > 16 * 1024 * 1024) throw new InvalidDataException("OpenHands state exceeds the private checkpoint limit.");
        return bytes.ToArray();
    }

    private static void RestorePrivateArchive(byte[] archive, string directory)
    {
        // Server validated paths and links; verify again before materializing private state.
        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            expanded = checked(expanded + entry.Length);
            if (expanded > 64 * 1024 * 1024 || entry.FullName.Split('/').Any(x => x is ".." or "." or "" || x.Contains(':') || x.Contains('\\')))
                throw new InvalidDataException("Invalid private checkpoint archive.");
            var target = Path.GetFullPath(Path.Combine(directory, entry.FullName));
            if (!target.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("Invalid private checkpoint path.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var output = File.Create(target); using var input = entry.Open(); input.CopyTo(output);
        }
    }

    private static async Task RestoreSourceAsync(byte[] archive, string workspace, CancellationToken token)
    {
        var target = Path.GetFullPath(workspace);
        var staging = target + ".restore-" + Guid.NewGuid().ToString("N");
        await PlatformGitWorkspaceClient.RestoreSourceAsync(archive, staging, token);
        var metadata = Path.Combine(target, ".csweet");
        if (Directory.Exists(metadata)) Directory.Move(metadata, Path.Combine(staging, ".csweet"));
        var old = target + ".discard-" + Guid.NewGuid().ToString("N");
        Directory.Move(target, old);
        Directory.Move(staging, target);
        Directory.Delete(old, recursive: true);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static IEnumerable<string> PrivateFiles(string root)
    {
        var pending=new Stack<string>();pending.Push(root);
        while(pending.TryPop(out var directory))
            foreach(var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes=File.GetAttributes(entry);
                if(attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Private state cannot contain links.");
                if(attributes.HasFlag(FileAttributes.Directory)) pending.Push(entry);
                else yield return entry;
            }
    }
}
