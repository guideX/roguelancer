using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoguelancerServer;

// Player state
public class PlayerState
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = "Unknown Pilot";
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float VelocityZ { get; set; }
    public float RotationX { get; set; }
    public float RotationY { get; set; }
    public float RotationZ { get; set; }
    public float RotationW { get; set; }
    public float Speed { get; set; }
    public float HullIntegrity { get; set; } = 100f;
    public float Energy { get; set; } = 100f;
    public bool IsAfterburnerActive { get; set; }
    public bool IsCruiseActive { get; set; }
    public long LastUpdateTick { get; set; }
}

// Projectile state
public class ProjectileState
{
    public string ProjectileId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float VelocityZ { get; set; }
    public string WeaponType { get; set; } = "Blaster";
    public long SpawnTick { get; set; }
}

// SignalR Hub
public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, PlayerState> _players = new();
    private static readonly ConcurrentDictionary<string, ProjectileState> _projectiles = new();
    private static readonly ConcurrentDictionary<string, string> _connectionToPlayer = new();

    public async Task<string> JoinGame(string playerName)
    {
        string playerId = Guid.NewGuid().ToString();
        string connectionId = Context.ConnectionId;

        var playerState = new PlayerState
        {
            PlayerId = playerId,
            PlayerName = playerName,
            PositionX = 3000f,
            PositionY = 1500f,
            PositionZ = 2000f,
            LastUpdateTick = DateTime.UtcNow.Ticks
        };

        _players[playerId] = playerState;
        _connectionToPlayer[connectionId] = playerId;

        Console.WriteLine($"[JOIN] Player '{playerName}' joined as {playerId}");

        await Clients.Caller.SendAsync("InitializeWorld", _players.Values);
        await Clients.Others.SendAsync("PlayerJoined", playerState);

        return playerId;
    }

    public async Task UpdatePlayerState(PlayerState state)
    {
        if (string.IsNullOrEmpty(state.PlayerId))
            return;

        state.LastUpdateTick = DateTime.UtcNow.Ticks;
        _players[state.PlayerId] = state;

        await Clients.Others.SendAsync("PlayerStateUpdated", state);
    }

    public async Task FireWeapon(ProjectileState projectile)
    {
        if (string.IsNullOrEmpty(projectile.ProjectileId))
            projectile.ProjectileId = Guid.NewGuid().ToString();

        projectile.SpawnTick = DateTime.UtcNow.Ticks;
        _projectiles[projectile.ProjectileId] = projectile;

        Console.WriteLine($"[WEAPON] Player {projectile.OwnerId} fired {projectile.WeaponType}");

        await Clients.All.SendAsync("ProjectileFired", projectile);
    }

    public async Task ReportHit(string targetPlayerId, string projectileId, float damage)
    {
        if (_players.TryGetValue(targetPlayerId, out var target))
        {
            target.HullIntegrity = Math.Max(0, target.HullIntegrity - damage);
            
            Console.WriteLine($"[HIT] Player {targetPlayerId} hit for {damage} damage (Hull: {target.HullIntegrity}%)");

            await Clients.All.SendAsync("PlayerHit", targetPlayerId, projectileId, damage, target.HullIntegrity);

            if (target.HullIntegrity <= 0)
            {
                Console.WriteLine($"[DESTROYED] Player {targetPlayerId} destroyed!");
                await Clients.All.SendAsync("PlayerDestroyed", targetPlayerId);
            }
        }

        _projectiles.TryRemove(projectileId, out _);
    }

    public async Task RespawnPlayer(string playerId)
    {
        if (_players.TryGetValue(playerId, out var player))
        {
            player.HullIntegrity = 100f;
            player.Energy = 100f;
            player.PositionX = 3000f;
            player.PositionY = 1500f;
            player.PositionZ = 2000f;
            player.VelocityX = 0f;
            player.VelocityY = 0f;
            player.VelocityZ = 0f;

            Console.WriteLine($"[RESPAWN] Player {playerId} respawned");

            await Clients.All.SendAsync("PlayerRespawned", player);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connectionId = Context.ConnectionId;

        if (_connectionToPlayer.TryRemove(connectionId, out var playerId))
        {
            _players.TryRemove(playerId, out var player);

            Console.WriteLine($"[LEAVE] Player {player?.PlayerName} ({playerId}) left");

            await Clients.Others.SendAsync("PlayerLeft", playerId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendChatMessage(string playerId, string message)
    {
        if (_players.TryGetValue(playerId, out var player))
        {
            Console.WriteLine($"[CHAT] {player.PlayerName}: {message}");
            await Clients.All.SendAsync("ChatMessageReceived", player.PlayerName, message);
        }
    }

    public List<PlayerState> GetAllPlayers()
    {
        return _players.Values.ToList();
    }
}

// Main program
public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSignalR();
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        app.UseCors();
        app.MapHub<GameHub>("/gamehub");
        app.MapGet("/", () => "Roguelancer Server is running!");
        app.MapGet("/status", () => new
        {
            ServerName = "Roguelancer Server",
            Status = "Online",
            Version = "1.0.0",
            Timestamp = DateTime.UtcNow
        });

        Console.WriteLine("========================================");
        Console.WriteLine("   ROGUELANCER MULTIPLAYER SERVER");
        Console.WriteLine("========================================");
        Console.WriteLine($"Server starting on http://localhost:5000");
        Console.WriteLine("SignalR Hub: /gamehub");
        Console.WriteLine("========================================");

        app.Run("http://localhost:5000");
    }
}
