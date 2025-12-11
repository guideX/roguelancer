using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Roguelancer
{
    /// <summary>
    /// Manages network communication with the Roguelancer multiplayer server
    /// </summary>
    public class NetworkManager
    {
        private HubConnection? _connection;
        private bool _isConnected = false;
        private string _playerId = string.Empty;
        private string _serverUrl = "http://localhost:5000/gamehub";
        
        // Remote players
        private readonly Dictionary<string, RemotePlayer> _remotePlayers = new();
        
        // Events for game integration
        public event Action<RemotePlayer>? OnPlayerJoined;
        public event Action<string>? OnPlayerLeft;
        public event Action<RemotePlayer>? OnPlayerStateUpdated;
        public event Action<NetworkProjectile>? OnProjectileFired;
        public event Action<string, float>? OnPlayerHit;
        public event Action<string>? OnPlayerDestroyed;
        public event Action<string, string>? OnChatMessageReceived;
        public event Action<List<RemotePlayer>>? OnWorldInitialized;

        public bool IsConnected => _isConnected;
        public string PlayerId => _playerId;
        public IEnumerable<RemotePlayer> RemotePlayers => _remotePlayers.Values;

        /// <summary>
        /// Connect to the multiplayer server
        /// </summary>
        public async Task<bool> ConnectAsync(string playerName, string serverUrl = "http://localhost:5000/gamehub")
        {
            try
            {
                _serverUrl = serverUrl;
                _connection = new HubConnectionBuilder()
                    .WithUrl(_serverUrl)
                    .WithAutomaticReconnect()
                    .Build();

                SetupEventHandlers();

                await _connection.StartAsync();
                
                // Join the game
                _playerId = await _connection.InvokeAsync<string>("JoinGame", playerName);
                _isConnected = true;

                Console.WriteLine($"[NETWORK] Connected to server as {playerName} (ID: {_playerId})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Failed to connect: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnect from the server
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
                _connection = null;
            }
            _isConnected = false;
            _remotePlayers.Clear();
            Console.WriteLine("[NETWORK] Disconnected from server");
        }

        /// <summary>
        /// Send player state update to server
        /// </summary>
        public async Task SendPlayerStateAsync(Ship playerShip)
        {
            if (!_isConnected || _connection == null)
                return;

            try
            {
                var state = new
                {
                    PlayerId = _playerId,
                    PositionX = playerShip.Position.X,
                    PositionY = playerShip.Position.Y,
                    PositionZ = playerShip.Position.Z,
                    VelocityX = playerShip.Velocity.X,
                    VelocityY = playerShip.Velocity.Y,
                    VelocityZ = playerShip.Velocity.Z,
                    RotationX = playerShip.Orientation.M11,
                    RotationY = playerShip.Orientation.M12,
                    RotationZ = playerShip.Orientation.M13,
                    RotationW = playerShip.Orientation.M14,
                    Speed = playerShip.Speed,
                    HullIntegrity = playerShip.Hull.HullPercentage * 100f,
                    Energy = playerShip.Energy.EnergyPercentage * 100f,
                    IsAfterburnerActive = playerShip.IsAfterburnerActive,
                    IsCruiseActive = playerShip.IsCruiseActive
                };

                await _connection.InvokeAsync("UpdatePlayerState", state);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Failed to send player state: {ex.Message}");
            }
        }

        /// <summary>
        /// Send weapon fire to server
        /// </summary>
        public async Task SendWeaponFireAsync(Vector3 position, Vector3 velocity, string weaponType)
        {
            if (!_isConnected || _connection == null)
                return;

            try
            {
                var projectile = new
                {
                    ProjectileId = Guid.NewGuid().ToString(),
                    OwnerId = _playerId,
                    PositionX = position.X,
                    PositionY = position.Y,
                    PositionZ = position.Z,
                    VelocityX = velocity.X,
                    VelocityY = velocity.Y,
                    VelocityZ = velocity.Z,
                    WeaponType = weaponType
                };

                await _connection.InvokeAsync("FireWeapon", projectile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Failed to send weapon fire: {ex.Message}");
            }
        }

        /// <summary>
        /// Report a hit on another player
        /// </summary>
        public async Task ReportHitAsync(string targetPlayerId, string projectileId, float damage)
        {
            if (!_isConnected || _connection == null)
                return;

            try
            {
                await _connection.InvokeAsync("ReportHit", targetPlayerId, projectileId, damage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Failed to report hit: {ex.Message}");
            }
        }

        /// <summary>
        /// Send chat message
        /// </summary>
        public async Task SendChatMessageAsync(string message)
        {
            if (!_isConnected || _connection == null)
                return;

            try
            {
                await _connection.InvokeAsync("SendChatMessage", _playerId, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Failed to send chat: {ex.Message}");
            }
        }

        private void SetupEventHandlers()
        {
            if (_connection == null)
                return;

            // Initialize world with existing players
            _connection.On<List<object>>("InitializeWorld", (players) =>
            {
                Console.WriteLine($"[NETWORK] Initializing world with {players.Count} players");
                var remotePlayers = new List<RemotePlayer>();
                
                foreach (var playerData in players)
                {
                    var remotePlayer = ParsePlayerState(playerData);
                    if (remotePlayer != null && remotePlayer.PlayerId != _playerId)
                    {
                        _remotePlayers[remotePlayer.PlayerId] = remotePlayer;
                        remotePlayers.Add(remotePlayer);
                    }
                }
                
                OnWorldInitialized?.Invoke(remotePlayers);
            });

            // Player joined
            _connection.On<object>("PlayerJoined", (playerData) =>
            {
                var remotePlayer = ParsePlayerState(playerData);
                if (remotePlayer != null && remotePlayer.PlayerId != _playerId)
                {
                    _remotePlayers[remotePlayer.PlayerId] = remotePlayer;
                    Console.WriteLine($"[NETWORK] Player joined: {remotePlayer.PlayerName}");
                    OnPlayerJoined?.Invoke(remotePlayer);
                }
            });

            // Player state updated
            _connection.On<object>("PlayerStateUpdated", (playerData) =>
            {
                var remotePlayer = ParsePlayerState(playerData);
                if (remotePlayer != null && remotePlayer.PlayerId != _playerId)
                {
                    _remotePlayers[remotePlayer.PlayerId] = remotePlayer;
                    OnPlayerStateUpdated?.Invoke(remotePlayer);
                }
            });

            // Player left
            _connection.On<string>("PlayerLeft", (playerId) =>
            {
                _remotePlayers.Remove(playerId);
                Console.WriteLine($"[NETWORK] Player left: {playerId}");
                OnPlayerLeft?.Invoke(playerId);
            });

            // Projectile fired
            _connection.On<object>("ProjectileFired", (projectileData) =>
            {
                var projectile = ParseProjectile(projectileData);
                if (projectile != null && projectile.OwnerId != _playerId)
                {
                    OnProjectileFired?.Invoke(projectile);
                }
            });

            // Player hit
            _connection.On<string, string, float, float>("PlayerHit", (targetPlayerId, projectileId, damage, hullIntegrity) =>
            {
                if (targetPlayerId == _playerId)
                {
                    Console.WriteLine($"[NETWORK] You were hit for {damage} damage!");
                }
                OnPlayerHit?.Invoke(targetPlayerId, damage);
            });

            // Player destroyed
            _connection.On<string>("PlayerDestroyed", (playerId) =>
            {
                Console.WriteLine($"[NETWORK] Player destroyed: {playerId}");
                OnPlayerDestroyed?.Invoke(playerId);
            });

            // Chat message
            _connection.On<string, string>("ChatMessageReceived", (playerName, message) =>
            {
                Console.WriteLine($"[CHAT] {playerName}: {message}");
                OnChatMessageReceived?.Invoke(playerName, message);
            });
        }

        private RemotePlayer? ParsePlayerState(object playerData)
        {
            try
            {
                // Use reflection to parse the dynamic object
                var type = playerData.GetType();
                var playerId = type.GetProperty("PlayerId")?.GetValue(playerData)?.ToString() ?? "";
                var playerName = type.GetProperty("PlayerName")?.GetValue(playerData)?.ToString() ?? "Unknown";
                
                return new RemotePlayer
                {
                    PlayerId = playerId,
                    PlayerName = playerName,
                    Position = new Vector3(
                        Convert.ToSingle(type.GetProperty("PositionX")?.GetValue(playerData)),
                        Convert.ToSingle(type.GetProperty("PositionY")?.GetValue(playerData)),
                        Convert.ToSingle(type.GetProperty("PositionZ")?.GetValue(playerData))
                    ),
                    Velocity = new Vector3(
                        Convert.ToSingle(type.GetProperty("VelocityX")?.GetValue(playerData)),
                        Convert.ToSingle(type.GetProperty("VelocityY")?.GetValue(playerData)),
                        Convert.ToSingle(type.GetProperty("VelocityZ")?.GetValue(playerData))
                    ),
                    Speed = Convert.ToSingle(type.GetProperty("Speed")?.GetValue(playerData)),
                    HullIntegrity = Convert.ToSingle(type.GetProperty("HullIntegrity")?.GetValue(playerData)),
                    Energy = Convert.ToSingle(type.GetProperty("Energy")?.GetValue(playerData)),
                    IsAfterburnerActive = Convert.ToBoolean(type.GetProperty("IsAfterburnerActive")?.GetValue(playerData)),
                    IsCruiseActive = Convert.ToBoolean(type.GetProperty("IsCruiseActive")?.GetValue(playerData))
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Failed to parse player state: {ex.Message}");
                return null;
            }
        }

        private NetworkProjectile? ParseProjectile(object projectileData)
        {
            try
            {
                var type = projectileData.GetType();
                
                return new NetworkProjectile
                {
                    ProjectileId = type.GetProperty("ProjectileId")?.GetValue(projectileData)?.ToString() ?? "",
                    OwnerId = type.GetProperty("OwnerId")?.GetValue(projectileData)?.ToString() ?? "",
                    Position = new Vector3(
                        Convert.ToSingle(type.GetProperty("PositionX")?.GetValue(projectileData)),
                        Convert.ToSingle(type.GetProperty("PositionY")?.GetValue(projectileData)),
                        Convert.ToSingle(type.GetProperty("PositionZ")?.GetValue(projectileData))
                    ),
                    Velocity = new Vector3(
                        Convert.ToSingle(type.GetProperty("VelocityX")?.GetValue(projectileData)),
                        Convert.ToSingle(type.GetProperty("VelocityY")?.GetValue(projectileData)),
                        Convert.ToSingle(type.GetProperty("VelocityZ")?.GetValue(projectileData))
                    ),
                    WeaponType = type.GetProperty("WeaponType")?.GetValue(projectileData)?.ToString() ?? "Blaster"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NETWORK] Failed to parse projectile: {ex.Message}");
                return null;
            }
        }

        public RemotePlayer? GetRemotePlayer(string playerId)
        {
            _remotePlayers.TryGetValue(playerId, out var player);
            return player;
        }
    }

    /// <summary>
    /// Represents a remote player in the game
    /// </summary>
    public class RemotePlayer
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = "Unknown";
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float Speed { get; set; }
        public float HullIntegrity { get; set; } = 100f;
        public float Energy { get; set; } = 100f;
        public bool IsAfterburnerActive { get; set; }
        public bool IsCruiseActive { get; set; }
        public Model Model { get; set; }
    }

    /// <summary>
    /// Represents a network projectile
    /// </summary>
    public class NetworkProjectile
    {
        public string ProjectileId { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public string WeaponType { get; set; } = "Blaster";
    }
}
