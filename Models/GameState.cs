namespace Backgammon.Backend.Models;

public class GameState
{
    public string GameId { get; set; } = Guid.NewGuid().ToString(); // Simple unique ID for the game session
    public Dictionary<PlayerId, Player> Players { get; set; } = new Dictionary<PlayerId, Player>();
    public List<BoardPoint> Board { get; set; } = new List<BoardPoint>(24); // 24 points
    public Dictionary<PlayerId, List<Checker>> Bar { get; set; } = new Dictionary<PlayerId, List<Checker>> {
        { PlayerId.Player1, new List<Checker>() },
        { PlayerId.Player2, new List<Checker>() }
    };
    public Dictionary<PlayerId, List<Checker>> BorneOff { get; set; } = new Dictionary<PlayerId, List<Checker>> {
        { PlayerId.Player1, new List<Checker>() },
        { PlayerId.Player2, new List<Checker>() }
    };
    public PlayerId? CurrentPlayerId { get; set; } = null;
    public int[]? CurrentDiceRoll { get; set; } = null; // The actual dice shown, e.g., [6, 4]
    public List<int>? RemainingMoves { get; set; } = null; // Dice numbers left to play, e.g., [6, 4] or [5, 5, 5, 5] for doubles
    public GamePhase Phase { get; set; } = GamePhase.WaitingForPlayers;
    public PlayerId? WinnerId { get; set; } = null;
    public int[]? InitialRoll { get; set; } // To store the roll for starting player [P1_die, P2_die]

    // Constructor or Init method needed to set up the initial board state
    public GameState()
    {
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        // Initialize 24 empty points
        for (int i = 1; i <= 24; i++)
        {
            Board.Add(new BoardPoint { PointIndex = i });
        }

        // Standard Backgammon setup
        // Player 1 (White) moves 1 -> 24
        // Player 2 (Black) moves 24 -> 1

        // Player 1 (White) Checkers
        PlaceCheckers(PlayerId.Player1, PlayerColor.White, 1, 2);  // 2 checkers on point 1
        PlaceCheckers(PlayerId.Player1, PlayerColor.White, 12, 5); // 5 checkers on point 12
        PlaceCheckers(PlayerId.Player1, PlayerColor.White, 17, 3); // 3 checkers on point 17
        PlaceCheckers(PlayerId.Player1, PlayerColor.White, 19, 5); // 5 checkers on point 19

        // Player 2 (Black) Checkers
        PlaceCheckers(PlayerId.Player2, PlayerColor.Black, 24, 2); // 2 checkers on point 24 (White's 1)
        PlaceCheckers(PlayerId.Player2, PlayerColor.Black, 13, 5); // 5 checkers on point 13 (White's 12)
        PlaceCheckers(PlayerId.Player2, PlayerColor.Black, 8, 3);  // 3 checkers on point 8 (White's 17)
        PlaceCheckers(PlayerId.Player2, PlayerColor.Black, 6, 5);  // 5 checkers on point 6 (White's 19)
    }

    private void PlaceCheckers(PlayerId player, PlayerColor color, int pointIndex, int count)
    {
        var point = Board.First(p => p.PointIndex == pointIndex);
        for (int i = 0; i < count; i++)
        {
            // Simple unique ID generation - improve if needed
            string checkerId = $"{color.ToString()[0]}{pointIndex}-{i + 1}";
            point.Checkers.Add(new Checker { Id = checkerId, PlayerId = player, Color = color });
        }
    }

    public int GetCheckersOnBarCount(PlayerId playerId) => Bar[playerId].Count;
    public int GetCheckersBorneOffCount(PlayerId playerId) => BorneOff[playerId].Count;
}