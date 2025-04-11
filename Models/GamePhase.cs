namespace Backgammon.Backend.Models;

public enum GamePhase
{
    WaitingForPlayers,
    StartingRoll, // Initial roll to determine first player
    PlayerTurn,
    // BearingOff is not a distinct phase, but a state condition during PlayerTurn
    GameOver
}