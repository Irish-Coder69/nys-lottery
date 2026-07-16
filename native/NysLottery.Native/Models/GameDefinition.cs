namespace NysLottery.Native.Models;

public sealed class GameDefinition
{
    public required string Name { get; init; }
    public required int PickCount { get; init; }
    public required int MaxNumber { get; init; }
    public required string DatasetId { get; init; }
    public required string PrizeName { get; init; }
    public required string EstimatedPrize { get; init; }
    public bool HasBonusBall { get; init; }
    public int BonusBallMax { get; init; }
}
