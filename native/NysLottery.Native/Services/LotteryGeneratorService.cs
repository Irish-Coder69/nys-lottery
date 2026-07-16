using NysLottery.Native.Models;

namespace NysLottery.Native.Services;

public sealed class LotteryGeneratorService
{
    private readonly Random _random = new();

    public IReadOnlyList<int> GenerateMainNumbers(GameDefinition game)
    {
        var set = new SortedSet<int>();
        while (set.Count < game.PickCount)
        {
            set.Add(_random.Next(1, game.MaxNumber + 1));
        }

        return set.ToList();
    }

    public int? GenerateBonusBall(GameDefinition game)
    {
        if (!game.HasBonusBall || game.BonusBallMax <= 0)
        {
            return null;
        }

        return _random.Next(1, game.BonusBallMax + 1);
    }
}
