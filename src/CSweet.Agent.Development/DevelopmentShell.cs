using Microsoft.Agents.AI.Tools.Shell;
namespace CSweet.Agent.Development;
internal static partial class DevelopmentExecution
{
    internal static BoundedWorkspaceShell CreateShell(string workspacePath, TimeSpan? commandTimeout = null)
    {
        var environment = new Dictionary<string, string?>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        {
            [OperatingSystem.IsWindows() ? "Path" : "PATH"] = "/run/csweet/workload/tools/bin" + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"),
            ["PATHEXT"] = OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD" : null,
            ["HOME"] = "/run/csweet/workload/home",
            ["npm_config_cache"] = "/run/csweet/workload/cache/npm",
            ["PIP_CACHE_DIR"] = "/run/csweet/workload/cache/pip",
            ["CSWEET_RUNTIME_NETWORK_MODE"] = Environment.GetEnvironmentVariable("CSWEET_RUNTIME_NETWORK_MODE"),
            ["CSWEET_WORKLOAD_TOOLS"] = "/run/csweet/workload/tools",
            ["DOTNET_CLI_HOME"] = "/run/csweet/workload/home/dotnet",
            ["NUGET_PACKAGES"] = "/run/csweet/workload/cache/nuget",
            ["HTTP_PROXY"] = Environment.GetEnvironmentVariable("HTTP_PROXY"),
            ["HTTPS_PROXY"] = Environment.GetEnvironmentVariable("HTTPS_PROXY"),
            ["ALL_PROXY"] = Environment.GetEnvironmentVariable("ALL_PROXY"),
            ["NO_PROXY"] = Environment.GetEnvironmentVariable("NO_PROXY"),
            ["CI"] = "true"
        };
        var timeout = commandTimeout ?? BoundedWorkspaceShell.CommandTimeout;
        var policy = CreateShellPolicy();
        return new BoundedWorkspaceShell(new LocalShellExecutor(new LocalShellExecutorOptions
        {
            // Confined commands already re-anchor at the workspace. Do not share shell
            // stdin, jobs, or option state between independent validation commands.
            Mode = ShellMode.Stateless,
            WorkingDirectory = workspacePath,
            ConfineWorkingDirectory = true,
            CleanEnvironment = true,
            Environment = environment,
            Timeout = timeout,
            MaxOutputBytes = 32 * 1024,
            AcknowledgeUnsafe = true,
            Policy = policy
        }), timeout, policy: policy, workingDirectory: workspacePath);
    }

    internal static ShellPolicy CreateShellPolicy() => new ShellPolicy(
                denyList:
                [
                    @"(^|[;&|]\s*)(sudo|su|doas|mount|umount|nsenter)\b",
                    @"(^|[;&|]\s*)(docker|podman|kubectl|crictl)\b",
                    @"(^|[;&|]\s*)(ps|top|htop|pstree|lsof)\b",
                    @"(^|[\s""'])(/proc|/sys|/dev|/run/secrets)(/|[\s""']|$)",
                    @"(^|[;&|]\s*)(env|printenv|set)\s*($|[;&|])",
                    @"\bgit\s+(push|remote\s+(remove|rename|set-url)|rebase|reset\s+--hard|filter-branch)\b",
                    @"\bgit\b.*\s(--force|-f|--delete)\b",
                    @"(^|[;&|]\s*)(shutdown|reboot|kill|pkill|killall)\b",
                    @"(^|[;&|]\s*)chown\s+.*(/|\\)(etc|run|proc|sys|dev)\b",
                    @"(^|[;&|]\s*)chmod\s+.*(/|\\)(etc|proc|sys|dev)\b",
                    @"(^|[;&|]\s*)chmod\s+.*(/|\\)run(?!/csweet/workload/tools(?:/|[\s""']|$))\b",
                    @"(^|[;&|]\s*)chmod\s+.*\.\.(?:/|\\)"
                ],
                allowList: null,
                custom: null);

}
