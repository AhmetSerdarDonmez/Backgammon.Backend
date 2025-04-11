using Microsoft.AspNetCore.SignalR;
using Backgammon.Backend.Services;
using Backgammon.Backend.Models;
using System.Threading.Tasks;

namespace Backgammon.Backend.Hubs;

public class GameHub : Hub
{
    private readonly GameService _gameService;

    // Inject the singleton GameService
    public GameHub(GameService gameService)
    {
        _gameService = gameService;
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"Client connected: {Context.ConnectionId}");
        var playerId = _gameService.AddPlayer(Context.ConnectionId);

        if (playerId.HasValue)
        {
            // Add user to a group corresponding to their PlayerId? Easier targeting.
            await Groups.AddToGroupAsync(Context.ConnectionId, playerId.Value.ToString());

            // Notify the connecting client of their role
            var player = _gameService.CurrentState.Players[playerId.Value];
            await Clients.Caller.SendAsync("AssignPlayerRole", player.Id, player.Color.ToString());

            if (_gameService.AreBothPlayersConnected())
            {
                Console.WriteLine("Second player connected. Starting game initialization.");
                _gameService.InitializeGameStart();

                Console.WriteLine($"DEBUG: InitializeGameStart done. Current State Phase: {_gameService.CurrentState.Phase}, Current Player: {_gameService.CurrentState.CurrentPlayerId}");
                Console.WriteLine("DEBUG: Attempting to broadcast UpdateGameState...");
                try
                {
                    await Clients.All.SendAsync("UpdateGameState", _gameService.CurrentState);
                    Console.WriteLine("DEBUG: UpdateGameState broadcast successful.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR Broadcasting UpdateGameState: {ex.Message}");
                    Console.WriteLine(ex.ToString()); // Log full exception details
                }

                Console.WriteLine("DEBUG: Attempting to broadcast GameStart...");
                try
                {
                    await Clients.All.SendAsync("GameStart", _gameService.CurrentState);
                    Console.WriteLine("DEBUG: GameStart broadcast successful.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR Broadcasting GameStart: {ex.Message}");
                    Console.WriteLine(ex.ToString());
                }

                Console.WriteLine("DEBUG: Attempting to broadcast NotifyTurn...");
                try
                {
                    await Clients.All.SendAsync("NotifyTurn", _gameService.CurrentState.CurrentPlayerId);
                    Console.WriteLine("DEBUG: NotifyTurn broadcast successful.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR Broadcasting NotifyTurn: {ex.Message}");
                    Console.WriteLine(ex.ToString());
                }
                Console.WriteLine("DEBUG: All game start broadcasts attempted.");


                // Send initial state to both players
                await Clients.All.SendAsync("UpdateGameState", _gameService.CurrentState);
                await Clients.All.SendAsync("GameStart", _gameService.CurrentState); // Explicit start signal
                await Clients.All.SendAsync("NotifyTurn", _gameService.CurrentState.CurrentPlayerId);

            }
            else
            {
                // Only one player connected, notify them to wait
                await Clients.Caller.SendAsync("WaitingForOpponent");
                // Send current state (mostly empty/waiting)
                await Clients.Caller.SendAsync("UpdateGameState", _gameService.CurrentState);
                Console.WriteLine("First player connected. Waiting for opponent.");
            }
        }
        else
        {
            // Game is full or error occurred
            await Clients.Caller.SendAsync("NotifyError", "Game is currently full or an error occurred.");
            Context.Abort(); // Disconnect the client
            Console.WriteLine($"Connection {Context.ConnectionId} rejected: Game full.");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
        var playerId = _gameService.RemovePlayer(Context.ConnectionId);

        if (playerId.HasValue)
        {
            // Notify the remaining player (if any)
            var opponent = _gameService.GetOpponent(playerId.Value);
            if (opponent != null)
            {
                await Clients.Client(opponent.ConnectionId).SendAsync("OpponentDisconnected");
                // Optionally send updated game state showing only one player
                await Clients.Client(opponent.ConnectionId).SendAsync("UpdateGameState", _gameService.CurrentState);
            }
            else // If the last player leaves
            {
                _gameService.ResetGame(); // Ensure clean state if everyone leaves
            }
        }

        if (exception != null)
        {
            Console.WriteLine($"Disconnect Error: {exception.Message}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    // --- Client-to-Server Methods ---

    [HubMethodName("RollDice")] // Explicit naming can be useful
    public async Task RollDice()
    {
        var player = _gameService.GetPlayerByConnectionId(Context.ConnectionId);
        if (player == null || _gameService.CurrentState.CurrentPlayerId != player.Id)
        {
            await Clients.Caller.SendAsync("NotifyError", "Not your turn or invalid player.");
            return;
        }
        if (_gameService.CurrentState.Phase != GamePhase.PlayerTurn)
        {
            await Clients.Caller.SendAsync("NotifyError", "Cannot roll dice at this time.");
            return;
        }
        // Check if dice already rolled this turn
        if (_gameService.CurrentState.RemainingMoves != null && _gameService.CurrentState.RemainingMoves.Any())
        {
            await Clients.Caller.SendAsync("NotifyError", "Dice already rolled for this turn.");
            return;
        }


        // TODO: Implement dice rolling logic in GameService
        _gameService.RollDice(player.Id); // This method should update GameState

        // Broadcast updated state after rolling
        await Clients.All.SendAsync("UpdateGameState", _gameService.CurrentState);
        // TODO: Maybe send specific dice roll info: SendAsync("DiceRolled", diceResult);
    }

    [HubMethodName("MakeMove")]
    public async Task MakeMove(MoveData move)
    {
        var player = _gameService.GetPlayerByConnectionId(Context.ConnectionId);
        if (player == null || _gameService.CurrentState.CurrentPlayerId != player.Id)
        {
            await Clients.Caller.SendAsync("NotifyError", "Not your turn or invalid player.");
            return;
        }
        if (_gameService.CurrentState.Phase != GamePhase.PlayerTurn)
        {
            await Clients.Caller.SendAsync("NotifyError", "Cannot move at this time.");
            return;
        }
        if (_gameService.CurrentState.RemainingMoves == null || !_gameService.CurrentState.RemainingMoves.Any())
        {
            await Clients.Caller.SendAsync("NotifyError", "You need to roll the dice first or have moves remaining.");
            return;
        }

        // TODO: Implement move logic in GameService
        bool moveSuccessful = _gameService.MakeMove(player.Id, move); // This method should update GameState

        if (moveSuccessful)
        {
            // Broadcast updated state after successful move
            await Clients.All.SendAsync("UpdateGameState", _gameService.CurrentState);

            // TODO: Check if turn ended in MakeMove and update CurrentPlayerId etc.
            // If turn ended, notify: await Clients.All.SendAsync("NotifyTurn", _gameService.CurrentState.CurrentPlayerId);
            // Check for game over: if(_gameService.CurrentState.Phase == GamePhase.GameOver) ... await Clients.All.SendAsync("GameOver", winnerId);
        }
        else
        {
            // Notify only the caller of the invalid move attempt
            await Clients.Caller.SendAsync("InvalidMove", "The requested move is not valid.");
            // Optionally send the current state again to ensure sync
            // await Clients.Caller.SendAsync("UpdateGameState", _gameService.CurrentState);
        }
    }
}