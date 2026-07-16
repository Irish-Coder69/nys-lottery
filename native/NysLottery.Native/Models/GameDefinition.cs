namespace NysLottery.Native.Models;

public sealed class GameDefinition
{
    public required string Name { get; init; }
    public required int PickCount { get; init; }
    public required int MaxNumber { get; init; }
    public bool HasBonusBall { get; init; }
    public int BonusBallMax { get; init; }
}
