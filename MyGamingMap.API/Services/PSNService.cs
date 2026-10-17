using MyGamingMap.API.Models.DTOs;
using MyGamingMap.API.Exceptions;
using System.Diagnostics;
using System.Text.Json;

namespace MyGamingMap.API.Services;

public class PlayerGamesResult
{
    public bool? Auth { get; set; }
    public bool Found { get; set; }
    public List<PlayerGame> Games { get; set; } = [];
}

public class RefreshNpssoResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
}

public class PSNService
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string CachePath = "../psn-service/debug/playerGames.json";
    
    private static readonly string s_nodePath =
        @"C:\Users\RoryJ\.nvm\versions\node\v20.15.1\bin\npx.cmd";

    private static readonly string s_workingDirectory =
        Path.GetFullPath("../psn-service");

    public async Task<PlayerGamesResult> GetPlayerGames(string username, bool useCache = false)
    {
        if (useCache && File.Exists(CachePath))
        {
            Console.WriteLine("Loading cached player games...");

            var cached = await File.ReadAllTextAsync(CachePath);

            return JsonSerializer.Deserialize<PlayerGamesResult>(
                cached,
                s_jsonSerializerOptions
            ) ?? new PlayerGamesResult();
        }

        var output = await RunTsCli($"tsx src/PSNservice.ts {username}");

        return JsonSerializer.Deserialize<PlayerGamesResult>(
            output, s_jsonSerializerOptions) ?? new PlayerGamesResult();
    }

    public async Task RefreshNPSSO(string npsso)
    {
        if (string.IsNullOrWhiteSpace(npsso))
            throw new ArgumentException("NPSSO cannot be empty.", nameof(npsso));

        // Pass the NPSSO via stdin so it doesn't appear in the process list.
        var output = await RunTsCli(
            "tsx src/PSNservice.ts refresh-npsso",
            stdin: npsso);

        var result = JsonSerializer.Deserialize<RefreshNpssoResult>(
            output, s_jsonSerializerOptions);

        if (result is null || !result.Ok) throw new InvalidNpssoException();
    }

    /// Spawns the TypeScript CLI, forwards stdio, and returns stdout
    /// on success. Throws on non-zero exit.
    private static async Task<string> RunTsCli(string arguments, string? stdin = null)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = s_nodePath,
                Arguments = arguments,
                WorkingDirectory = s_workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdin is not null,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        if (stdin is not null)
        {
            await process.StandardInput.WriteLineAsync(stdin);
            process.StandardInput.Close();
        }

        // Read both streams concurrently to avoid deadlocks on large output.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stderr))
            Console.WriteLine(stderr);

        if (process.ExitCode != 0) throw new InvalidNpssoException();

        return stdout;
    }
}