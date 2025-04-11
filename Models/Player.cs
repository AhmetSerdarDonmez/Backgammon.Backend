using System.Text.Json.Serialization;

namespace Backgammon.Backend.Models;

public class Player
{
    public PlayerId Id { get; init; }
    public required string ConnectionId { get; set; } // SignalR connection ID
    public string Name { get; set; } = string.Empty; // e.g., "Player 1"
    public PlayerColor Color { get; init; }

    [JsonIgnore] // Don't serialize internal state like readiness directly if not needed
    public bool IsReady { get; set; } = false;

    // Calculated properties might be better handled by GameState directly
    // public int CheckersOnBar { get; set; } = 0;
    // public int CheckersBorneOff { get; set; } = 0;
}