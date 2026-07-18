using System.Diagnostics;
using System.Text.Json;
using MyGamingMap.API.Models;

namespace MyGamingMap.API.Services;

public class PSNServiceClient
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<PlayerGame>> GetPlayerGames(string username)
    {
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

        if (process.ExitCode != 0) throw new Exception(error);

        return JsonSerializer.Deserialize<List<PlayerGame>>(
            output,
            s_jsonSerializerOptions
        ) ?? [];
    }
}