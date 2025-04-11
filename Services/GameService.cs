using Backgammon.Backend.Models;
using System.Collections.Concurrent;
using System.Linq; // Add LINQ for easier querying

namespace Backgammon.Backend.Services;

public class GameService
{
    private readonly ConcurrentDictionary<string, PlayerId> _connectedPlayers = new();
    private GameState _gameState = new GameState();
    private static readonly Random _random = new Random(); // For dice rolls

    public GameState CurrentState => _gameState;

    // --- Connection Management --- (Keep existing methods: AddPlayer, RemovePlayer, etc.)
    #region Connection Management Methods
    public PlayerId? AddPlayer(string connectionId)
    {
        if (_connectedPlayers.Count >= 2) return null;
        PlayerId newPlayerId = (_connectedPlayers.IsEmpty || !_connectedPlayers.Values.Contains(PlayerId.Player1)) ? PlayerId.Player1 : PlayerId.Player2;
        PlayerColor color = (newPlayerId == PlayerId.Player1) ? PlayerColor.White : PlayerColor.Black;

        var player = new Player { Id = newPlayerId, ConnectionId = connectionId, Color = color, Name = $"Player {(int)newPlayerId}" };
        if (_connectedPlayers.TryAdd(connectionId, newPlayerId))
        {
            _gameState.Players[newPlayerId] = player;
            Console.WriteLine($"Player {newPlayerId} connected with ConnectionId: {connectionId}");
            return newPlayerId;
        }
        return null;
    }

    public PlayerId? RemovePlayer(string connectionId)
    {
        if (_connectedPlayers.TryRemove(connectionId, out var playerId))
        {
            _gameState.Players.Remove(playerId);
            Console.WriteLine($"Player {playerId} disconnected.");
            if (_gameState.Phase != GamePhase.WaitingForPlayers && _gameState.Phase != GamePhase.GameOver)
            {
                Console.WriteLine("A player disconnected mid-game. Resetting game state.");
                ResetGame(); // Reset includes changing phase to WaitingForPlayers
            }
            else if (_connectedPlayers.IsEmpty)
            {
                Console.WriteLine("Last player left. Resetting game state.");
                ResetGame();
            }
            else
            {
                // If one player left from waiting state, just update the state potentially
                _gameState.Phase = GamePhase.WaitingForPlayers;
                _gameState.CurrentPlayerId = null; // No current player if waiting
            }
            return playerId;
        }
        return null;
    }

    public Player? GetPlayerByConnectionId(string connectionId)
    {
        if (_connectedPlayers.TryGetValue(connectionId, out var playerId))
        {
            return _gameState.Players.GetValueOrDefault(playerId);
        }
        return null;
    }

    public Player? GetOpponent(PlayerId currentPlayerId)
    {
        PlayerId opponentId = (currentPlayerId == PlayerId.Player1) ? PlayerId.Player2 : PlayerId.Player1;
        return _gameState.Players.GetValueOrDefault(opponentId);
    }

    public bool AreBothPlayersConnected() => _connectedPlayers.Count == 2;
    #endregion

    // --- Game Initialization & Reset --- (Keep existing methods)
    #region Initialization and Reset
    // In Services/GameService.cs

    public void InitializeGameStart()
    {
        if (!AreBothPlayersConnected()) return;
        Console.WriteLine("Both players connected. Initializing game start...");

        // Reset state (creates new board, clears bar/borneoff etc)
        _gameState = new GameState();
        // Re-add players from the concurrent dictionary
        foreach (var kvp in _connectedPlayers)
        {
            var playerId = kvp.Value;
            var player = new Player
            {
                Id = playerId,
                ConnectionId = kvp.Key,
                Color = (playerId == PlayerId.Player1) ? PlayerColor.White : PlayerColor.Black,
                Name = $"Player {(int)playerId}"
            };
            _gameState.Players[playerId] = player;
        }

        // --- Initial Roll Logic ---
        int die1, die2;
        PlayerId startingPlayer;

        Console.WriteLine("Performing initial roll to determine starting player...");
        do
        {
            die1 = _random.Next(1, 7);
            die2 = _random.Next(1, 7);
            Console.WriteLine($"Initial roll: Player 1 ({_gameState.Players[PlayerId.Player1].Color}) rolled {die1}, Player 2 ({_gameState.Players[PlayerId.Player2].Color}) rolled {die2}");

            if (die1 == die2)
            {
                Console.WriteLine("Initial roll tied. Rolling again.");
                // Optionally, add a small delay or signal to clients about the re-roll
                 Task.Delay(500).Wait(); // Simple synchronous wait (consider async if needed)
            }

        } while (die1 == die2); // Re-roll if tied

        // Determine winner and set starting state
        if (die1 > die2)
        {
            startingPlayer = PlayerId.Player1;
        }
        else
        {
            startingPlayer = PlayerId.Player2;
        }

        Console.WriteLine($"Player {startingPlayer} wins the initial roll and starts the game.");

        _gameState.CurrentPlayerId = startingPlayer;
        // The first turn uses the numbers from the initial winning roll
        _gameState.CurrentDiceRoll = new int[] { die1, die2 }; // Store the winning roll as the current dice
        _gameState.RemainingMoves = new List<int> { die1, die2 }; // Both dice values are available for the first move
        _gameState.Phase = GamePhase.PlayerTurn; // Game starts immediately with the first player's turn
        _gameState.InitialRoll = new int[] { die1, die2 }; // Store for potential display


        Console.WriteLine($"Game starting. Player {_gameState.CurrentPlayerId}'s turn with roll [{string.Join(", ", _gameState.RemainingMoves)}].");
        // The existing code in GameHub OnConnectedAsync already broadcasts the GameState
        // after calling InitializeGameStart, so the clients will receive this starting state.
    }

    // --- Rest of GameService.cs remains the same ---
    // (RollDice, MakeMove, ValidateMove, etc.)
    public void ResetGame()
    {
        Console.WriteLine("Resetting game state.");
        _gameState = new GameState(); // Creates a fresh state with initial setup
        _gameState.Phase = GamePhase.WaitingForPlayers;
        // Players dictionary in GameState is now empty.
        // _connectedPlayers dictionary still holds connection IDs until they disconnect.
    }
    #endregion

    // --- Core Game Logic ---

    public void RollDice(PlayerId playerId)
    {
        if (_gameState.CurrentPlayerId != playerId || _gameState.Phase != GamePhase.PlayerTurn) return; // Basic validation
        if (_gameState.RemainingMoves != null && _gameState.RemainingMoves.Any()) return; // Dice already rolled

        int die1 = _random.Next(1, 7); // 1 to 6
        int die2 = _random.Next(1, 7);
        _gameState.CurrentDiceRoll = new int[] { die1, die2 };

        Console.WriteLine($"Player {playerId} rolled: {die1}, {die2}");

        if (die1 == die2)
        {
            // Doubles: 4 moves of that value
            _gameState.RemainingMoves = new List<int> { die1, die1, die1, die1 };
            Console.WriteLine("Doubles rolled!");
        }
        else
        {
            _gameState.RemainingMoves = new List<int> { die1, die2 };
        }

        // Check if any moves are possible with this roll
        if (!CanPlayerMove(playerId, _gameState.RemainingMoves))
        {
            Console.WriteLine("No possible moves for this roll. Turn skipped.");
            EndTurn(); // Automatically end turn if no moves are possible
        }
        else
        {
            Console.WriteLine($"Possible moves available for {playerId}. Remaining: [{string.Join(", ", _gameState.RemainingMoves)}]");
        }
    }

    public bool MakeMove(PlayerId playerId, MoveData move)
    {
        if (_gameState.CurrentPlayerId != playerId || _gameState.Phase != GamePhase.PlayerTurn) return false;
        if (_gameState.RemainingMoves == null || !_gameState.RemainingMoves.Any()) return false; // No dice rolled or moves left

        // Validate the specific requested move
        var validationResult = ValidateMove(playerId, move);
        if (!validationResult.IsValid)
        {
            Console.WriteLine($"Invalid move attempt: {validationResult.Reason}");
            return false;
        }

        // Execute the move
        ExecuteMove(playerId, move, validationResult.DiceValueUsed);

        // Remove the used dice value
        _gameState.RemainingMoves.Remove(validationResult.DiceValueUsed);
        Console.WriteLine($"Move successful. Remaining moves: [{string.Join(", ", _gameState.RemainingMoves)}]");


        // Check if turn should end
        // Turn ends if no moves remaining OR if remaining moves are impossible to play
        if (!_gameState.RemainingMoves.Any() || !CanPlayerMove(playerId, _gameState.RemainingMoves))
        {
            if (_gameState.RemainingMoves.Any())
            {
                Console.WriteLine($"No more possible moves with remaining dice: [{string.Join(", ", _gameState.RemainingMoves)}].");
            }
            EndTurn();
        }


        return true;
    }

    // --- Move Validation ---

    private (bool IsValid, int DiceValueUsed, string Reason) ValidateMove(PlayerId playerId, MoveData requestedMove)
    {
        Player player = _gameState.Players[playerId];
        int barCount = _gameState.GetCheckersOnBarCount(playerId);
        bool isPlayerWhite = player.Color == PlayerColor.White;
        int moveDirection = isPlayerWhite ? 1 : -1; // White moves 1->24, Black moves 24->1

        // 1. Check Bar Rule: If checkers are on the bar, must enter first.
        if (barCount > 0 && requestedMove.StartPointIndex != 0) // 0 represents the bar
        {
            return (false, 0, "Must move checkers from the bar first.");
        }
        if (barCount == 0 && requestedMove.StartPointIndex == 0)
        {
            return (false, 0, "No checkers on the bar to move.");
        }

        int targetPointIndex;
        int diceValueNeeded;

        // Determine required dice value and target point index based on move type
        if (requestedMove.StartPointIndex == 0) // Entering from Bar
        {
            diceValueNeeded = isPlayerWhite ? requestedMove.EndPointIndex : 25 - requestedMove.EndPointIndex;
            targetPointIndex = requestedMove.EndPointIndex;

            // Basic validation for entry point based on dice
            if (targetPointIndex < 1 || targetPointIndex > 24) return (false, 0, "Invalid entry point.");
            if (!isPlayerWhite && (targetPointIndex < 19 || targetPointIndex > 24)) return (false, 0, "Black must enter in opponent's home board (points 19-24).");
            if (isPlayerWhite && (targetPointIndex < 1 || targetPointIndex > 6)) return (false, 0, "White must enter in opponent's home board (points 1-6).");

        }
        else if (IsBearingOffMove(playerId, requestedMove)) // Bearing Off
        {
            if (!CanBearOff(playerId))
            {
                return (false, 0, "Cannot bear off until all checkers are in the home board.");
            }
            targetPointIndex = isPlayerWhite ? 25 : 0; // Special values for bear off destination
            diceValueNeeded = Math.Abs(targetPointIndex - requestedMove.StartPointIndex);

            // Check if the starting point for bearing off is valid
            if (requestedMove.StartPointIndex < (isPlayerWhite ? 19 : 1) || requestedMove.StartPointIndex > (isPlayerWhite ? 24 : 6))
            {
                return (false, 0, "Can only bear off from the home board.");
            }
        }
        else // Normal move on the board
        {
            targetPointIndex = requestedMove.EndPointIndex;
            diceValueNeeded = Math.Abs(targetPointIndex - requestedMove.StartPointIndex);

            // Check direction
            if (Math.Sign(targetPointIndex - requestedMove.StartPointIndex) != moveDirection)
            {
                return (false, 0, "Checkers must move towards the home board.");
            }
        }


        // 2. Check Dice: Does the player have the required dice value available?
        if (!_gameState.RemainingMoves.Contains(diceValueNeeded))
        {
            // Special check for bearing off: can use a higher dice roll if no checker on that exact point
            if (IsBearingOffMove(playerId, requestedMove))
            {
                // Find the highest dice roll available >= diceValueNeeded
                var higherDice = _gameState.RemainingMoves
                                    .Where(d => d >= diceValueNeeded)
                                    .OrderByDescending(d => d)
                                    .ToList();

                if (!higherDice.Any()) return (false, 0, $"Required dice value {diceValueNeeded} not available.");

                // Check if there are checkers on higher points *within the home board*
                int highestOccupiedPoint = GetHighestOccupiedPointInHomeBoard(playerId);

                // If the start point IS the highest occupied point, then using a higher die is allowed
                if (requestedMove.StartPointIndex == highestOccupiedPoint)
                {
                    // Player MUST use a die roll that matches the highest point if possible.
                    // If they rolled exactly highestOccupiedPoint's value, they must use it.
                    // If they rolled higher, and the highest point is the one they're trying to move,
                    // they can use the higher die *only if* the exact die isn't available.
                    int actualDiceToUse = -1;
                    if (_gameState.RemainingMoves.Contains(diceValueNeeded))
                    {
                        actualDiceToUse = diceValueNeeded;
                    }
                    else
                    {
                        // Use the smallest available die that is >= needed value
                        actualDiceToUse = _gameState.RemainingMoves.Where(d => d >= diceValueNeeded).Min();
                        if (!_gameState.RemainingMoves.Contains(actualDiceToUse)) return (false, 0, "Logic error in bear off higher dice check."); // safety
                    }
                    diceValueNeeded = actualDiceToUse; // Use the determined die value
                }
                else
                {
                    // Trying to bear off from a lower point when a higher point is occupied.
                    // Must use a die value exactly matching the point index if possible,
                    // or move a checker from a higher point instead.
                    // This check means the player cannot *choose* to use a higher die value
                    // for a lower point checker *if* a higher point checker exists.
                    if (!_gameState.RemainingMoves.Contains(diceValueNeeded))
                    {
                        return (false, 0, $"Must move from higher point ({highestOccupiedPoint}) or use exact dice roll ({diceValueNeeded}) if available.");
                    }
                    // If exact dice is available, proceed with validation using diceValueNeeded
                }
            }
            else // Not bearing off, exact dice needed
            {
                return (false, 0, $"Required dice value {diceValueNeeded} not available.");
            }
        }


        // 3. Check Start Point: Does the player have checkers at the start point? (Skip for bar)
        if (requestedMove.StartPointIndex > 0) // Not starting from bar
        {
            BoardPoint startPoint = _gameState.Board[requestedMove.StartPointIndex - 1]; // Adjust for 0-based index
            if (!startPoint.Checkers.Any() || startPoint.Checkers.First().PlayerId != playerId)
            {
                return (false, diceValueNeeded, "No checker at the starting point.");
            }
        }

        // 4. Check End Point: Is the target point valid/open? (Skip for bear off)
        if (targetPointIndex > 0 && targetPointIndex < 25) // Not bearing off
        {
            BoardPoint endPoint = _gameState.Board[targetPointIndex - 1]; // Adjust for 0-based index
            if (endPoint.Checkers.Count > 1 && endPoint.Checkers.First().PlayerId != playerId)
            {
                return (false, diceValueNeeded, "Target point is blocked by opponent.");
            }
        }

        // 5. Bearing Off Specific Checks
        if (IsBearingOffMove(playerId, requestedMove))
        {
            // Already checked CanBearOff and home board range
            // Already checked dice availability (including higher dice rule)
            // Need to ensure the start point is correct according to the dice roll used (handled by dice check logic)
        }


        // If all checks pass
        return (true, diceValueNeeded, string.Empty);
    }

    private bool IsBearingOffMove(PlayerId playerId, MoveData move)
    {
        Player player = _gameState.Players[playerId];
        bool isPlayerWhite = player.Color == PlayerColor.White;
        // White bears off to 25, Black to 0 (using our convention)
        return (isPlayerWhite && move.EndPointIndex == 25) || (!isPlayerWhite && move.EndPointIndex == 0);
    }

    private bool CanBearOff(PlayerId playerId)
    {
        // Check if all player's checkers are in their home board
        if (_gameState.GetCheckersOnBarCount(playerId) > 0) return false;

        Player player = _gameState.Players[playerId];
        bool isPlayerWhite = player.Color == PlayerColor.White;
        int homeStart = isPlayerWhite ? 19 : 1; // White home: 19-24, Black home: 1-6
        int homeEnd = isPlayerWhite ? 24 : 6;

        for (int i = 1; i <= 24; i++)
        {
            // If outside home board
            if (i < homeStart || i > homeEnd)
            {
                BoardPoint point = _gameState.Board[i - 1];
                if (point.Checkers.Any(c => c.PlayerId == playerId))
                {
                    return false; // Found a checker outside home board
                }
            }
        }
        return true; // All checkers are home (or borne off)
    }

    private int GetHighestOccupiedPointInHomeBoard(PlayerId playerId)
    {
        Player player = _gameState.Players[playerId];
        bool isPlayerWhite = player.Color == PlayerColor.White;
        int highestPoint = -1;

        if (isPlayerWhite) // Home 19-24, highest is 24
        {
            for (int i = 24; i >= 19; i--)
            {
                if (_gameState.Board[i - 1].Checkers.Any(c => c.PlayerId == playerId))
                {
                    highestPoint = i;
                    break;
                }
            }
        }
        else // Home 1-6, highest is 6
        {
            for (int i = 6; i >= 1; i--)
            {
                if (_gameState.Board[i - 1].Checkers.Any(c => c.PlayerId == playerId))
                {
                    highestPoint = i;
                    break;
                }
            }
        }
        return highestPoint;
    }

    // --- Move Execution ---

    private void ExecuteMove(PlayerId playerId, MoveData move, int diceValueUsed)
    {
        Player player = _gameState.Players[playerId];
        Checker checkerToMove;

        // 1. Remove checker from start location
        if (move.StartPointIndex == 0) // From Bar
        {
            checkerToMove = _gameState.Bar[playerId].First();
            _gameState.Bar[playerId].RemoveAt(0);
            Console.WriteLine($"Player {playerId} moving checker {checkerToMove.Id} from Bar.");
        }
        else // From Board Point
        {
            BoardPoint startPoint = _gameState.Board[move.StartPointIndex - 1];
            checkerToMove = startPoint.Checkers.Last(); // Take the top checker
            startPoint.Checkers.RemoveAt(startPoint.Checkers.Count - 1);
            Console.WriteLine($"Player {playerId} moving checker {checkerToMove.Id} from point {move.StartPointIndex}.");
        }

        // 2. Place checker at end location (or bear off)
        if (IsBearingOffMove(playerId, move)) // Bearing Off
        {
            _gameState.BorneOff[playerId].Add(checkerToMove);
            Console.WriteLine($"Player {playerId} bore off checker {checkerToMove.Id} using die {diceValueUsed}. Total borne off: {_gameState.GetCheckersBorneOffCount(playerId)}");

            // Check for win condition
            if (_gameState.GetCheckersBorneOffCount(playerId) == 15)
            {
                _gameState.Phase = GamePhase.GameOver;
                _gameState.WinnerId = playerId;
                _gameState.CurrentPlayerId = null; // No more turns
                _gameState.RemainingMoves?.Clear();
                _gameState.CurrentDiceRoll = null;
                Console.WriteLine($"Game Over! Player {playerId} wins!");
            }
        }
        else // Moving to a board point
        {
            BoardPoint endPoint = _gameState.Board[move.EndPointIndex - 1];

            // Check for Hit
            if (endPoint.Checkers.Count == 1 && endPoint.Checkers.First().PlayerId != playerId)
            {
                Checker hitChecker = endPoint.Checkers.First();
                endPoint.Checkers.RemoveAt(0); // Remove opponent checker
                _gameState.Bar[hitChecker.PlayerId].Add(hitChecker); // Add to opponent's bar
                Console.WriteLine($"Player {playerId} hit opponent's ({hitChecker.PlayerId}) checker {hitChecker.Id} on point {move.EndPointIndex}!");
            }

            // Add the moved checker to the endpoint
            endPoint.Checkers.Add(checkerToMove);
            Console.WriteLine($"Checker {checkerToMove.Id} moved to point {move.EndPointIndex}.");
        }
    }

    // --- Turn Management & Helper ---

    private void EndTurn()
    {
        if (_gameState.Phase == GamePhase.GameOver) return; // Don't switch turns if game ended

        _gameState.CurrentPlayerId = (_gameState.CurrentPlayerId == PlayerId.Player1) ? PlayerId.Player2 : PlayerId.Player1;
        _gameState.CurrentDiceRoll = null;
        _gameState.RemainingMoves = null;
        _gameState.Phase = GamePhase.PlayerTurn; // Ensure phase is correct
        Console.WriteLine($"Turn ended. It is now Player {_gameState.CurrentPlayerId}'s turn.");
    }

    // Checks if *any* valid move exists for the player with the given dice rolls
    private bool CanPlayerMove(PlayerId playerId, List<int> remainingMoves)
    {
        if (remainingMoves == null || !remainingMoves.Any()) return false;

        Player player = _gameState.Players[playerId];
        int barCount = _gameState.GetCheckersOnBarCount(playerId);

        // If on the bar, check if any entry move is possible
        if (barCount > 0)
        {
            foreach (int die in remainingMoves.Distinct()) // Check each unique die value
            {
                int targetPoint = GetEntryPoint(player.Color, die);
                if (targetPoint != -1) // Ensure valid point calculation
                {
                    var validationResult = ValidateMove(playerId, new MoveData { StartPointIndex = 0, EndPointIndex = targetPoint });
                    if (validationResult.IsValid) return true; // Found a valid entry move
                }
            }
            return false; // No valid entry moves possible
        }

        // If not on the bar, check all checkers on the board
        bool canBearOff = CanBearOff(playerId);
        for (int i = 1; i <= 24; i++)
        {
            BoardPoint startPoint = _gameState.Board[i - 1];
            if (startPoint.Checkers.Any() && startPoint.Checkers.First().PlayerId == playerId)
            {
                foreach (int die in remainingMoves.Distinct()) // Check each unique die value
                {
                    // Check normal move
                    int targetPointIndex = GetTargetPointIndex(player.Color, i, die);
                    if (targetPointIndex > 0 && targetPointIndex <= 24)
                    {
                        var validationResult = ValidateMove(playerId, new MoveData { StartPointIndex = i, EndPointIndex = targetPointIndex });
                        if (validationResult.IsValid) return true;
                    }

                    // Check bearing off move
                    if (canBearOff)
                    {
                        int bearOffEndPoint = player.Color == PlayerColor.White ? 25 : 0;
                        // Check if moving die spaces lands exactly on bear-off point or beyond
                        int requiredPoint = player.Color == PlayerColor.White ? 25 - die : die; // Point index needed to bear off with 'die'
                        int highestPoint = GetHighestOccupiedPointInHomeBoard(playerId);

                        if (i == requiredPoint) // Can bear off from this point with this die?
                        {
                            var validationResult = ValidateMove(playerId, new MoveData { StartPointIndex = i, EndPointIndex = bearOffEndPoint });
                            if (validationResult.IsValid) return true;
                        }
                        // Also check if we can bear off from the highest point using a larger die
                        else if (i == highestPoint && die > (player.Color == PlayerColor.White ? 25 - i : i))
                        {
                            var validationResult = ValidateMove(playerId, new MoveData { StartPointIndex = i, EndPointIndex = bearOffEndPoint });
                            if (validationResult.IsValid) return true;
                        }

                    }
                }
            }
        }

        return false; // No valid moves found anywhere
    }

    // Helper to calculate target point index based on color, start, and dice
    private int GetTargetPointIndex(PlayerColor color, int startPointIndex, int diceValue)
    {
        if (color == PlayerColor.White) // Moves 1 -> 24
        {
            return startPointIndex + diceValue;
        }
        else // Black moves 24 -> 1
        {
            return startPointIndex - diceValue;
        }
    }

    // Helper to calculate entry point based on color and dice roll
    private int GetEntryPoint(PlayerColor color, int diceValue)
    {
        if (color == PlayerColor.White) // Enters on 1-6
        {
            return diceValue; // Enters directly onto point matching die
        }
        else // Black enters on 19-24
        {
            return 25 - diceValue; // e.g., rolls 1 -> enters 24; rolls 6 -> enters 19
        }
    }

}