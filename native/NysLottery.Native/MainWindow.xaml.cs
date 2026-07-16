using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NysLottery.Native.Models;
using NysLottery.Native.Services;

namespace NysLottery.Native;

public partial class MainWindow : Window
{
    private const string GitHubLatestReleaseApi = "https://api.github.com/repos/Irish-Coder69/nys-lottery/releases/latest";
    private static readonly HttpClient Http = new();
    private readonly LotteryGeneratorService _generator = new();

    private readonly List<GameDefinition> _games =
    [
        new() { Name = "Powerball", PickCount = 5, MaxNumber = 69, DatasetId = "d6yy-54nr", PrizeName = "Jackpot", EstimatedPrize = "$478 Million", HasBonusBall = true, BonusBallMax = 26 },
        new() { Name = "Mega Millions", PickCount = 5, MaxNumber = 70, DatasetId = "5xaw-6ayf", PrizeName = "Jackpot", EstimatedPrize = "$843 Million", HasBonusBall = true, BonusBallMax = 25 },
        new() { Name = "Lotto", PickCount = 6, MaxNumber = 59, DatasetId = "6nbc-h7bj", PrizeName = "Jackpot", EstimatedPrize = "$8.2 Million" },
        new() { Name = "Take 5", PickCount = 5, MaxNumber = 39, DatasetId = "dg63-4siq", PrizeName = "Top Prize", EstimatedPrize = "$57,575" },
        new() { Name = "Pick 10", PickCount = 10, MaxNumber = 80, DatasetId = "bycu-cw7c", PrizeName = "Top Prize", EstimatedPrize = "$500,000" }
    ];

    public MainWindow()
    {
        InitializeComponent();
        GameComboBox.ItemsSource = _games;
        GameComboBox.SelectedIndex = 0;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadBrandingLogo();
        AboutMenuItem.Header = $"About (v{GetCurrentVersion()})";
        await RefreshExternalDataAsync();
        _ = CheckForAndInstallUpdateAsync(showUpToDateMessage: false);
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new Window
        {
            Title = "About",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 420,
            Content = BuildAboutContent()
        };

        aboutWindow.ShowDialog();
    }

    private UIElement BuildAboutContent()
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(24),
            Orientation = Orientation.Vertical
        };

        var logoPath = FindBrandingLogoPath();
        if (!string.IsNullOrWhiteSpace(logoPath))
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(logoPath, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();

            panel.Children.Add(new Image
            {
                Source = image,
                Width = 96,
                Height = 96,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = "NYS Lottery Native",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"Version: {GetCurrentVersion()}",
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Created by Judson M. Fitzpatrick",
            TextAlignment = TextAlignment.Center
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Irish_Coders_Programming",
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Copyright © 2026 Irish_Coders_Programming. All rights reserved.",
            TextAlignment = TextAlignment.Center,
            Opacity = 0.8,
            Margin = new Thickness(0, 0, 0, 16)
        });

        var okButton = new Button
        {
            Content = "OK",
            Width = 96,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsDefault = true,
            IsCancel = true
        };
        okButton.Click += (_, _) =>
        {
            var parent = Window.GetWindow(okButton);
            parent?.Close();
        };

        panel.Children.Add(okButton);
        return panel;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (GameComboBox.SelectedItem is not GameDefinition game)
        {
            StatusText.Text = "Select a game first.";
            return;
        }

        if (!int.TryParse(PickCountTextBox.Text, out var totalPicks) || totalPicks < 1 || totalPicks > 100)
        {
            StatusText.Text = "Enter a pick count between 1 and 100.";
            return;
        }

        ResultsListBox.Items.Clear();
        var history = new StringBuilder();

        for (var i = 0; i < totalPicks; i++)
        {
            var numbers = _generator.GenerateMainNumbers(game);
            var bonus = _generator.GenerateBonusBall(game);
            var line = bonus.HasValue
                ? $"{string.Join(" ", numbers)} | Bonus: {bonus.Value}"
                : string.Join(" ", numbers);

            ResultsListBox.Items.Add(line);
            history.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {game.Name} | {line}");
        }

        if (HistoryTextBox.Text.Length > 0)
        {
            HistoryTextBox.AppendText(Environment.NewLine);
        }

        HistoryTextBox.AppendText(history.ToString());
        StatusText.Text = $"Generated {totalPicks} picks for {game.Name}.";
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await CheckForAndInstallUpdateAsync(showUpToDateMessage: true);
    }

    private async Task CheckForAndInstallUpdateAsync(bool showUpToDateMessage)
    {
        try
        {
            await ShowUpdateProgressAsync("Checking for updates...");
            StatusText.Text = "Checking for updates...";
            var release = await GetLatestReleaseAsync();
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                StatusText.Text = "Could not check updates right now.";
                await HideUpdateProgressAsync();
                return;
            }

            var currentVersion = GetCurrentVersion();
            var latestVersion = release.TagName.TrimStart('v', 'V');

            if (CompareVersionStrings(latestVersion, currentVersion) <= 0)
            {
                if (showUpToDateMessage)
                {
                    StatusText.Text = "You are up to date.";
                }

                await HideUpdateProgressAsync();

                return;
            }

            if (string.IsNullOrWhiteSpace(release.InstallerDownloadUrl))
            {
                StatusText.Text = $"Update {latestVersion} found, but installer asset is missing.";
                await HideUpdateProgressAsync();
                return;
            }

            StatusText.Text = $"Update {latestVersion} found. Downloading installer...";
            var installerPath = await DownloadInstallerWithProgressAsync(release.InstallerDownloadUrl, latestVersion);
            StatusText.Text = "Download complete. Starting installer...";
            await ShowUpdateProgressAsync("Launching installer...");

            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/NORESTART /SP-",
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            StatusText.Text = "Installer launched. Follow installer steps to complete update.";
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Update failed. Please try again.";
            await HideUpdateProgressAsync();
            Console.WriteLine(ex.Message);
        }
    }

    private async void RefreshResults_Click(object sender, RoutedEventArgs e)
    {
        await RefreshExternalDataAsync();
    }

    private async void GameComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        await RefreshExternalDataAsync();
    }

    private async Task RefreshExternalDataAsync()
    {
        if (GameComboBox.SelectedItem is not GameDefinition game)
        {
            return;
        }

        StatusText.Text = $"Refreshing history and {game.PrizeName.ToLowerInvariant()} for {game.Name}...";
        PrizeLabelText.Text = game.PrizeName;
        CurrentPrizeText.Text = "Checking...";
        WinningNumbersListBox.Items.Clear();

        var historyTask = LoadWinningNumbersAsync(game);
        var prizeTask = LoadCurrentPrizeAsync(game);
        await Task.WhenAll(historyTask, prizeTask);

        StatusText.Text = $"Updated {game.Name} results and prize.";
    }

    private async Task LoadWinningNumbersAsync(GameDefinition game)
    {
        try
        {
            var url = $"https://data.ny.gov/resource/{game.DatasetId}.json?$limit=25&$order=draw_date DESC";
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var date = row.TryGetProperty("draw_date", out var drawDate)
                    ? drawDate.GetString()?.Split('T')[0]
                    : "Unknown date";

                var numbers = ExtractNumbers(row);
                var extra = ExtractExtra(row);
                var line = numbers.Count > 0 ? string.Join(" - ", numbers) : "No numbers found";
                if (!string.IsNullOrWhiteSpace(extra))
                {
                    line = $"{line} | {extra}";
                }

                WinningNumbersListBox.Items.Add($"{date}  |  {line}");
            }

            if (WinningNumbersListBox.Items.Count == 0)
            {
                WinningNumbersListBox.Items.Add("No historical results found.");
            }
        }
        catch
        {
            WinningNumbersListBox.Items.Clear();
            WinningNumbersListBox.Items.Add("Could not load winning numbers.");
        }
    }

    private async Task LoadCurrentPrizeAsync(GameDefinition game)
    {
        try
        {
            var html = await Http.GetStringAsync("https://nylottery.ny.gov/");
            var prize = TryExtractPrize(game.Name, html) ?? game.EstimatedPrize;
            CurrentPrizeText.Text = prize;
        }
        catch
        {
            CurrentPrizeText.Text = $"{game.EstimatedPrize} (est.)";
        }
    }

    private static List<int> ExtractNumbers(JsonElement row)
    {
        if (row.TryGetProperty("winning_numbers", out var winNums) && winNums.ValueKind == JsonValueKind.String)
        {
            return Regex.Matches(winNums.GetString() ?? string.Empty, "\\d+")
                .Select(m => int.Parse(m.Value))
                .ToList();
        }

        var numbers = new List<int>();
        var altFields = new[]
        {
            "evening_winning_numbers",
            "midday_winning_numbers",
            "take_5_evening_winning_numbers",
            "take_5_midday_winning_numbers"
        };

        foreach (var field in altFields)
        {
            if (row.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String)
            {
                numbers.AddRange(Regex.Matches(value.GetString() ?? string.Empty, "\\d+").Select(m => int.Parse(m.Value)));
            }
        }

        for (var i = 1; i <= 20; i++)
        {
            var key = $"winning_number_{i}";
            if (row.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var n))
            {
                numbers.Add(n);
            }
        }

        return numbers;
    }

    private static string? ExtractExtra(JsonElement row)
    {
        var extras = new[] { "mega_ball", "powerball", "cash_ball", "bonus", "bonus_ball" };
        foreach (var extra in extras)
        {
            if (row.TryGetProperty(extra, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var txt = value.GetString();
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    return $"Extra: {txt}";
                }
            }
        }

        return null;
    }

    private static string? TryExtractPrize(string gameName, string html)
    {
        string pattern;

        if (gameName == "Mega Millions")
        {
            pattern = "mega millions[^$]{0,160}\\$\\s*([0-9.,]+(?:\\s*(?:million|billion))?)";
        }
        else if (gameName == "Powerball")
        {
            pattern = "powerball[^$]{0,160}\\$\\s*([0-9.,]+(?:\\s*(?:million|billion))?)";
        }
        else if (gameName == "Lotto")
        {
            pattern = "(?:new york lotto|lotto)[^$]{0,160}\\$\\s*([0-9.,]+(?:\\s*(?:million|billion))?)";
        }
        else
        {
            return null;
        }

        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        return "$" + match.Groups[1].Value.Trim();
    }

    private void LoadBrandingLogo()
    {
        var logoPath = FindBrandingLogoPath();
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(logoPath, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        BrandingLogoImage.Source = image;
    }

    private static string? FindBrandingLogoPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Irish_Coders_Programming.png"),
            Path.Combine(AppContext.BaseDirectory, "Irish_Coders_Programming Logo", "Irish_Coders_Programming.png"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Irish_Coders_Programming Logo", "Irish_Coders_Programming.png")),
            Path.Combine(AppContext.BaseDirectory, "New_York_Lottery.svg.ico"),
            Path.Combine(AppContext.BaseDirectory, "Icon", "New_York_Lottery.svg.ico"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Icon", "New_York_Lottery.svg.ico"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string GetCurrentVersion()
    {
        var raw = Assembly.GetExecutingAssembly().GetName().Version;
        if (raw is null)
        {
            return "1.0.0";
        }

        return $"{raw.Major}.{raw.Minor}.{Math.Max(0, raw.Build)}";
    }

    private static int CompareVersionStrings(string a, string b)
    {
        var aParts = a.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var bParts = b.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var length = Math.Max(aParts.Length, bParts.Length);

        for (var i = 0; i < length; i++)
        {
            var av = i < aParts.Length ? aParts[i] : 0;
            var bv = i < bParts.Length ? bParts[i] : 0;
            if (av > bv)
            {
                return 1;
            }

            if (av < bv)
            {
                return -1;
            }
        }

        return 0;
    }

    private async Task<string> DownloadInstallerWithProgressAsync(string url, string version)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "NysLotteryNativeUpdater");

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        var tempFile = Path.Combine(Path.GetTempPath(), $"NysLottery-Native-Setup-{version}.exe");

        await using var input = await response.Content.ReadAsStreamAsync();
        await using var fs = File.Create(tempFile);

        var buffer = new byte[81920];
        long downloaded = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read == 0)
            {
                break;
            }

            await fs.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                var percent = (int)Math.Round(downloaded * 100d / totalBytes.Value);
                await ShowUpdateProgressAsync($"Downloading update... {percent}%", percent);
            }
            else
            {
                await ShowUpdateProgressAsync($"Downloading update... {downloaded / 1024 / 1024} MB");
            }
        }

        await ShowUpdateProgressAsync("Download complete. Preparing installer...", 100);
        return tempFile;
    }

    private async Task ShowUpdateProgressAsync(string message, int? percent = null)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressText.Visibility = Visibility.Visible;
            UpdateProgressText.Text = message;
            StatusText.Text = message;

            if (percent.HasValue)
            {
                UpdateProgressBar.IsIndeterminate = false;
                UpdateProgressBar.Value = Math.Max(0, Math.Min(100, percent.Value));
            }
            else
            {
                UpdateProgressBar.IsIndeterminate = true;
            }
        }, DispatcherPriority.Render);
    }

    private async Task HideUpdateProgressAsync()
    {
        await Dispatcher.InvokeAsync(() =>
        {
            UpdateProgressBar.IsIndeterminate = false;
            UpdateProgressBar.Value = 0;
            UpdateProgressBar.Visibility = Visibility.Collapsed;
            UpdateProgressText.Visibility = Visibility.Collapsed;
            UpdateProgressText.Text = string.Empty;
        }, DispatcherPriority.Render);
    }

    private static async Task<GitHubReleaseInfo?> GetLatestReleaseAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, GitHubLatestReleaseApi);
        request.Headers.Add("User-Agent", "NysLotteryNativeUpdater");

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var tagProperty)
            ? tagProperty.GetString()
            : null;

        var installerUrl = string.Empty;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var assetName) ? assetName.GetString() : null;
                var downloadUrl = asset.TryGetProperty("browser_download_url", out var browserUrl) ? browserUrl.GetString() : null;

                if (!string.IsNullOrWhiteSpace(name) &&
                    name.Contains("NysLottery-Native-Setup-", StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(downloadUrl))
                {
                    installerUrl = downloadUrl;
                    break;
                }
            }
        }

        return new GitHubReleaseInfo
        {
            TagName = tagName,
            InstallerDownloadUrl = installerUrl
        };
    }

    private sealed class GitHubReleaseInfo
    {
        public string? TagName { get; init; }
        public string? InstallerDownloadUrl { get; init; }
    }
}
