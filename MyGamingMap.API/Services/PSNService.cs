using MyGamingMap.API.Models;
using System.Diagnostics;
using System.Text.Json;

namespace MyGamingMap.API.Services;

public class PSNService
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string CachePath = "../psn-service/debug/playerGames.json";

    public async Task<List<PlayerGame>> GetPlayerGames(string username, bool useCache = false)
    {
        if (useCache && File.Exists(CachePath))
        {
            Console.WriteLine("Loading cached player games...");

            var json = await File.ReadAllTextAsync(CachePath);

            return JsonSerializer.Deserialize<List<PlayerGame>>(
                json,
                s_jsonSerializerOptions
            ) ?? [];
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Users\RoryJ\.nvm\versions\node\v20.15.1\bin\npx.cmd",
                Arguments = $"tsx src/PSNservice.ts {username}",
                WorkingDirectory = Path.GetFullPath("../psn-service"),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (!string.IsNullOrWhiteSpace(error)) Console.WriteLine(error);

        if (process.ExitCode != 0) throw new Exception(error);
        
        return JsonSerializer.Deserialize<List<PlayerGame>>(
            output,
            s_jsonSerializerOptions
        ) ?? [];
    }
}