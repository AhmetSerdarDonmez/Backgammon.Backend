namespace Backgammon.Backend.Models;

public class Checker
{
    public required string Id { get; init; } // Unique ID for each checker (e.g., "W1", "B15")
    public PlayerId PlayerId { get; init; }
    public PlayerColor Color { get; init; }
}