using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using NysLottery.Native.Models;
using NysLottery.Native.Services;

namespace NysLottery.Native;

public partial class MainWindow : Window
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/Irish-Coder69/nys-lottery/master/version.json";
    private static readonly HttpClient Http = new();
    private readonly LotteryGeneratorService _generator = new();
    private readonly VersionManifestService _versionService = new();

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
        await RefreshExternalDataAsync();
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
        var current = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        StatusText.Text = "Checking for updates...";
        var manifest = await _versionService.GetLatestAsync(ManifestUrl);
        if (manifest is null)
        {
            StatusText.Text = "Could not reach update server.";
            return;
        }

        if (VersionManifestService.IsNewer(manifest.Version, current))
        {
            var result = MessageBox.Show(
                $"Version {manifest.Version} is available. Open download page?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = manifest.DownloadUrl ?? "https://github.com/Irish-Coder69/nys-lottery/releases/latest",
                    UseShellExecute = true
                });
            }

            StatusText.Text = $"Update available: {manifest.Version}";
            return;
        }

        StatusText.Text = "You are up to date.";
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
}
