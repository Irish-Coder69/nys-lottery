using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows;
using NysLottery.Native.Models;
using NysLottery.Native.Services;

namespace NysLottery.Native;

public partial class MainWindow : Window
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/Irish-Coder69/nys-lottery/master/version.json";
    private readonly LotteryGeneratorService _generator = new();
    private readonly VersionManifestService _versionService = new();

    private readonly List<GameDefinition> _games =
    [
        new() { Name = "Powerball", PickCount = 5, MaxNumber = 69, HasBonusBall = true, BonusBallMax = 26 },
        new() { Name = "Mega Millions", PickCount = 5, MaxNumber = 70, HasBonusBall = true, BonusBallMax = 25 },
        new() { Name = "Lotto", PickCount = 6, MaxNumber = 59 },
        new() { Name = "Take 5", PickCount = 5, MaxNumber = 39 },
        new() { Name = "Pick 10", PickCount = 10, MaxNumber = 80 }
    ];

    public MainWindow()
    {
        InitializeComponent();
        GameComboBox.ItemsSource = _games;
        GameComboBox.SelectedIndex = 0;
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
}
