namespace Backgammon.Backend.Models;

public class MoveData
{
    // 0 = Bar for the current player
    // 1-24 = Board points
    public required int StartPointIndex { get; set; }

    // 1-24 = Board points
    // 25 = Bear off for Player 1 (White)
    // 0 = Bear off for Player 2 (Black)
    // Need consistent constants/logic for bear off destinations
    public required int EndPointIndex { get; set; }

    // It might be simpler for the client *not* to specify which dice value.
    // The server can determine which dice value(s) could make the requested move valid.
    // Let's omit DiceValueUsed for now and calculate validity on the server.
    // public required int DiceValueUsed { get; set; }
}