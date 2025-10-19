using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOULauncher.Views;

namespace AOULauncher.Tools;

public class EpicLauncher : IAmongUsLauncher
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool SetDllDirectory(string lpPathName);

    // Stolen from Legendary
    // https://github.com/derrod/legendary/blob/42af7b5db78eb22210ae6cf2dd1b913c64ca3183/legendary/api/egs.py#L16

    private const string EglVersion = "11.0.1-14907503+++Portal+Release-Live";

    private const string OAuth2Agent = $"EpicGamesLauncher/{EglVersion}";

    private const string UserAgent = $"UELauncher/{EglVersion} Windows/10.0.19041.1.256.64bit";

    private const string OAuthHost = "account-public-service-prod03.ol.epicgames.com";
    private const string UserBasic = "34a02cf8f4414e29b15921876da36f9a";
    private const string PwBasic = "daafbccc737745039dffe53d94fc76cf";
    private const string Label = "Live-EternalKnight";

    private const string ExchangeEndpoint = $"https://{OAuthHost}/account/api/oauth/exchange";
    private const string TokenEndpoint = $"https://{OAuthHost}/account/api/oauth/token";

    public bool CopyDoorstop => false;

    private string Path { get; }
    private HttpClient HttpClient { get; }

    public EpicLauncher(string exePath)
    {
        Path = exePath;
        HttpClient = new HttpClient();
        HttpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        HttpClient.DefaultRequestHeaders.Add("Authorization",
            $"basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{UserBasic}:{PwBasic}"))}");
    }

    public void Launch(Action onExit, params string[] args)
    {
        // TODO: Implement OAuth2 flow to get authorization code

        void OnCodeReceived(string code)
        {
            Task.Run(() =>
            {
                try
                {
                    var accessToken = GetAuthorizationCode(code).GetAwaiter().GetResult();
                    var exchangeCode = GetExchangeCode(accessToken).GetAwaiter().GetResult();
                    LaunchGame(onExit, exchangeCode, args);
                }
                catch (Exception e)
                {
                    var errorWindow = new Error($"An error occurred during Epic Games authentication: {e.Message}");
                    errorWindow.Show();
                }
            });
        }
    }

    private async Task<string> GetAuthorizationCode(string accessCode)
    {
        var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", accessCode),
            new KeyValuePair<string, string>("token_type, ", "eg1"),
        ]);

        var res = await HttpClient.PostAsync(TokenEndpoint, content);
        if (!res.IsSuccessStatusCode)
        {
            throw new Exception($"[EpicLauncher] Failed to obtain access token: {res}");
        }

        var tokenData = await res.Content.ReadAsAsync<Dictionary<string, object>>();
        return tokenData["access_token"].ToString() ?? throw new Exception("Access token missing in response");
    }

    private async Task<string> GetExchangeCode(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Remove("Authorization");
        HttpClient.DefaultRequestHeaders.Add("Authorization", $"bearer {accessToken}");
        var res = await HttpClient.GetAsync(ExchangeEndpoint);
        if (!res.IsSuccessStatusCode)
        {
            throw new Exception($"[EpicLauncher] Failed to obtain exchange code: {res}");
        }

        var exchangeData = await res.Content.ReadAsAsync<Dictionary<string, object>>();
        return exchangeData["code"].ToString() ?? throw new Exception("Exchange code missing in response");
    }

    private void LaunchGame(Action onExit, string exchangeCode, params string[] args)
    {
        // resolves doorstop without having to copy to game directory
        SetDllDirectory(Constants.ModFolder);

        var newArgs = args.Concat([$"-AUTH_PASSWORD={exchangeCode}"]);
        var psi = new ProcessStartInfo(Path)
        {
            Arguments = string.Join(" ", newArgs)
        };
        var process = Process.Start(psi);
        if (process is null)
        {
            return;
        }

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => onExit();
    }
}