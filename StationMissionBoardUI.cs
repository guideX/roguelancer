using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// Physical Bar terminal overlay. The station scene remains loaded while this
/// screen consumes input, just like the existing dealer overlays.
/// </summary>
public sealed class StationMissionBoardUI
{
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly MissionManager _missionManager;
    private readonly JobBoard _jobBoard;
    private readonly CargoHold _cargoHold;
    private readonly Action<string> _showMessage;

    private string _stationName = "Station";
    private Station _station;
    private bool _inputGate;
    private string _statusMessage = string.Empty;
    private float _statusRemaining;
    private IReadOnlyList<MarketOpportunity> _marketOpportunities = Array.Empty<MarketOpportunity>();

    public StationMissionBoardUI(
        SpriteFont font,
        Texture2D pixel,
        MissionManager missionManager,
        CargoHold cargoHold = null,
        Action<string> showMessage = null)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
        _missionManager = missionManager ?? throw new ArgumentNullException(nameof(missionManager));
        _jobBoard = new JobBoard(missionManager);
        _cargoHold = cargoHold;
        _showMessage = showMessage ?? (_ => { });
    }

    public bool IsOpen { get; private set; }
    public IReadOnlyList<Mission> AvailableMissions => _jobBoard.AvailableMissions;

    public void Open(string stationName, Station station)
    {
        _stationName = string.IsNullOrWhiteSpace(stationName) ? "Station" : stationName;
        _station = station;
        _jobBoard.RefreshMissions(6, station?.FactionId, station);
        _marketOpportunities = _missionManager.GetKnownMarketOpportunities(5);
        _statusMessage = string.Empty;
        _statusRemaining = 0f;
        _inputGate = true;
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        _inputGate = false;
        _statusMessage = string.Empty;
        _statusRemaining = 0f;
    }

    public void Update(float deltaSeconds)
    {
        if (_statusRemaining <= 0f) return;
        _statusRemaining = MathF.Max(0f, _statusRemaining - MathF.Max(0f, deltaSeconds));
        if (_statusRemaining <= 0f) _statusMessage = string.Empty;
    }

    public bool HandleInput(KeyboardState current, KeyboardState previous)
    {
        if (!IsOpen) return false;
        if (_inputGate)
        {
            if (current.IsKeyUp(Keys.E)) _inputGate = false;
            return true;
        }

        if (Pressed(current, previous, Keys.Escape))
        {
            Close();
            return true;
        }

        if (Pressed(current, previous, Keys.C) && _missionManager.ActiveMission?.Type == MissionType.ExportContract)
        {
            if (_missionManager.CancelMission(_missionManager.ActiveMission, out string cancelMessage))
            {
                SetStatus(cancelMessage, success: true);
                Close();
            }
            else
            {
                SetStatus(cancelMessage, success: false);
                _showMessage(cancelMessage);
            }
            return true;
        }

        if (Pressed(current, previous, Keys.Up) || Pressed(current, previous, Keys.W))
        {
            _jobBoard.MoveSelectionUp();
            return true;
        }

        if (Pressed(current, previous, Keys.Down) || Pressed(current, previous, Keys.S))
        {
            _jobBoard.MoveSelectionDown();
            return true;
        }

        if (Pressed(current, previous, Keys.Enter) || Pressed(current, previous, Keys.E))
        {
            ActivateSelection();
            return true;
        }

        return true;
    }

    private void ActivateSelection()
    {
        Mission completed = _missionManager.UnclaimedCompletedMission;
        if (completed != null)
        {
            if (_missionManager.TryClaimReward(completed, _station, out string rewardMessage))
            {
                SetStatus(rewardMessage, success: true);
                Close();
            }
            else
            {
                SetStatus(rewardMessage, success: false);
                _showMessage(rewardMessage);
            }
            return;
        }

        if (_missionManager.ActiveMission != null)
        {
            SetStatus("Finish the active mission before accepting another job.", success: false);
            _showMessage(_statusMessage);
            return;
        }

        if (!_jobBoard.AcceptSelectedMission())
        {
            SetStatus("Mission acceptance rejected.", success: false);
            return;
        }

        SetStatus("Mission accepted.", success: true);
    }

    public void Draw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice)
    {
        if (!IsOpen || spriteBatch == null || graphicsDevice == null) return;

        int width = graphicsDevice.Viewport.Width;
        int height = graphicsDevice.Viewport.Height;
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), Color.Black * 0.72f);

        int panelWidth = Math.Min(1180, width - 64);
        int panelHeight = Math.Min(700, height - 64);
        int panelX = (width - panelWidth) / 2;
        int panelY = (height - panelHeight) / 2;
        Rectangle panel = new(panelX, panelY, panelWidth, panelHeight);
        spriteBatch.Draw(_pixel, panel, new Color(9, 15, 25) * 0.98f);
        DrawBorder(spriteBatch, panel, Color.Gold, 3);

        spriteBatch.DrawString(_font, $"MISSION BOARD - {_stationName}", new Vector2(panel.X + 24, panel.Y + 18), Color.Gold);
        DrawMarketOpportunityStrip(spriteBatch, panel);

        int contentTop = panel.Y + 84;
        int contentBottom = panel.Bottom - 86;
        int dividerX = panel.X + (int)(panel.Width * 0.38f);
        Rectangle listPanel = new(panel.X + 18, contentTop, dividerX - panel.X - 28, contentBottom - contentTop);
        Rectangle detailPanel = new(dividerX + 10, contentTop, panel.Right - dividerX - 28, contentBottom - contentTop);
        DrawBorder(spriteBatch, listPanel, Color.DarkSlateGray, 2);
        DrawBorder(spriteBatch, detailPanel, Color.DarkSlateGray, 2);

        spriteBatch.DrawString(_font, "AVAILABLE JOBS", new Vector2(listPanel.X + 14, listPanel.Y + 12), Color.LightSkyBlue);
        IReadOnlyList<Mission> missions = _jobBoard.AvailableMissions;
        int listY = listPanel.Y + 52;
        for (int i = 0; i < missions.Count; i++)
        {
            Mission mission = missions[i];
            int rowHeight = 62;
            Rectangle row = new(listPanel.X + 8, listY + i * rowHeight, listPanel.Width - 16, rowHeight - 6);
            bool selected = i == _jobBoard.SelectedIndex;
            if (selected) spriteBatch.Draw(_pixel, row, Color.Orange * 0.26f);
            DrawBorder(spriteBatch, row, selected ? Color.Orange : Color.DarkSlateGray, selected ? 2 : 1);
            spriteBatch.DrawString(_font, $"{(selected ? "> " : "  ")}{Shorten(mission.Title, 27)}", new Vector2(row.X + 10, row.Y + 8), Color.White);
            spriteBatch.DrawString(_font, mission.GetTypeLabel(), new Vector2(row.X + 28, row.Y + 32), TypeColor(mission.Type));
            string reward = $"{mission.Reward:N0} CR";
            Vector2 rewardSize = _font.MeasureString(reward);
            spriteBatch.DrawString(_font, reward, new Vector2(row.Right - rewardSize.X - 10, row.Y + 20), Color.Yellow);
        }

        spriteBatch.DrawString(_font, "SELECTED JOB", new Vector2(detailPanel.X + 16, detailPanel.Y + 12), Color.LightSkyBlue);
        Mission selectedMission = missions.Count > 0 && _jobBoard.SelectedIndex < missions.Count
            ? missions[_jobBoard.SelectedIndex]
            : null;
        DrawSelectedMission(spriteBatch, detailPanel, selectedMission);

        int statusY = panel.Bottom - 78;
        Mission active = _missionManager.ActiveMission;
        Mission completed = _missionManager.UnclaimedCompletedMission;
        string activeLine = active != null
            ? active.Type == MissionType.FreightContract
                ? $"ACTIVE: {active.Title} - {active.GetStatusLabel()} - Reserved {_cargoHold?.GetMissionCargoQuantity(active.Id) ?? 0}/{active.RequiredQuantity} - {active.GetDestinationLabel()}"
            : active.Type == MissionType.ExportContract
                ? $"ACTIVE: {active.Title} - {active.GetStatusLabel()} - Loaded {_cargoHold?.GetMissionCargoQuantity(active.Id) ?? 0}/{active.RequiredQuantity} - {active.GetDestinationLabel()}"
                : $"ACTIVE: {active.Title} - {active.GetStatusLabel()} - {active.GetHudProgressLine()}"
            : completed != null
                ? $"MISSION COMPLETE: {completed.Title} - {completed.Reward:N0} CR"
                : "ACTIVE: None";
        spriteBatch.DrawString(_font, Shorten(activeLine, 94), new Vector2(panel.X + 24, statusY), completed != null ? Color.Lime : Color.Cyan);

        string footer = "UP/DOWN or W/S: Select    ENTER/E: Accept or Claim    ESC: Back";
        if (_missionManager.ActiveMission?.Type == MissionType.ExportContract)
            footer = "UP/DOWN or W/S: Select    C: Cancel export    ESC: Back";
        spriteBatch.DrawString(_font, footer, new Vector2(panel.X + 24, panel.Bottom - 42), Color.LightGray);
        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            Color statusColor = _statusMessage.StartsWith("Mission accepted", StringComparison.OrdinalIgnoreCase) ||
                _statusMessage.StartsWith("Mission reward", StringComparison.OrdinalIgnoreCase)
                ? Color.Lime
                : Color.OrangeRed;
            string status = Shorten(_statusMessage, 54);
            Vector2 statusSize = _font.MeasureString(status);
            spriteBatch.DrawString(_font, status, new Vector2(panel.Right - statusSize.X - 24, panel.Bottom - 42), statusColor);
        }
    }

    private void DrawSelectedMission(SpriteBatch spriteBatch, Rectangle detailPanel, Mission mission)
    {
        if (mission == null)
        {
            spriteBatch.DrawString(_font, "No jobs available.", new Vector2(detailPanel.X + 16, detailPanel.Y + 54), Color.Gray);
            return;
        }

        int x = detailPanel.X + 16;
        int y = detailPanel.Y + 52;
        spriteBatch.DrawString(_font, mission.Title, new Vector2(x, y), Color.White);
        y += 32;
        foreach (string line in Wrap(mission.Description, 54))
        {
            spriteBatch.DrawString(_font, line, new Vector2(x, y), Color.LightGray);
            y += 22;
        }
        y += 8;
        spriteBatch.DrawString(_font, $"Objective: {mission.GetObjectiveText()}", new Vector2(x, y), Color.LightSkyBlue);
        y += 30;
        if (mission.Type == MissionType.CourierDelivery)
        {
            spriteBatch.DrawString(_font, $"Origin: {mission.SourceStationName}", new Vector2(x, y), Color.LightGreen);
            y += 24;
            spriteBatch.DrawString(_font, $"Destination: {mission.GetDestinationLabel()}", new Vector2(x, y), Color.LightGreen);
            y += 24;
            spriteBatch.DrawString(_font, $"Cargo required: {mission.GetCargoLabel()} ({mission.PackageVolume} space)", new Vector2(x, y), Color.LightGreen);
            y += 24;
            string freeSpace = _cargoHold == null ? "unknown" : $"{_cargoHold.AvailableCapacity} space";
            spriteBatch.DrawString(_font, $"Free cargo space: {freeSpace}", new Vector2(x, y), Color.Cyan);
            y += 26;
        }
        else if (mission.Type == MissionType.FreightContract)
        {
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
            int reserved = _cargoHold?.GetMissionCargoQuantity(mission.Id) ?? 0;
            int owned = commodity == null ? 0 : _cargoHold?.GetCommodityQuantity(commodity.Name) ?? 0;
            int remaining = Math.Max(0, mission.RequiredQuantity - reserved);
            spriteBatch.DrawString(_font, $"Destination: {mission.GetDestinationLabel()}", new Vector2(x, y), Color.LightGreen);
            y += 24;
            spriteBatch.DrawString(_font, $"Commodity: {commodity?.Name ?? mission.CommodityId}", new Vector2(x, y), Color.LightGreen);
            y += 24;
            spriteBatch.DrawString(_font, $"Required: {mission.RequiredQuantity:N0}   Reserved: {reserved:N0}   Remaining: {remaining:N0}", new Vector2(x, y), Color.LightGreen);
            y += 24;
            spriteBatch.DrawString(_font, $"Owned total: {owned:N0}   Volume: {(commodity?.VolumePerUnit ?? 0)} / unit", new Vector2(x, y), Color.Cyan);
            y += 26;
        }
        else if (mission.Type == MissionType.ExportContract)
        {
            Commodity commodity = CommodityCatalog.GetByIdOrName(mission.CommodityId);
            int requiredVolume = (commodity?.VolumePerUnit ?? 0) * mission.RequiredQuantity;
            int freeSpace = _cargoHold?.AvailableCapacity ?? 0;
            spriteBatch.DrawString(_font, $"Origin: {mission.OriginStationName}", new Vector2(x, y), Color.LightGreen);
            y += 24;
            spriteBatch.DrawString(_font, $"Destination: {mission.GetDestinationLabel()}", new Vector2(x, y), Color.LightGreen);
            y += 24;
            spriteBatch.DrawString(_font, $"Cargo supplied on acceptance: {mission.GetTargetLabel()} ({requiredVolume} space)", new Vector2(x, y), Color.LightGreen);
            y += 24;
            spriteBatch.DrawString(_font, $"Free cargo space: {freeSpace} space", new Vector2(x, y),
                freeSpace >= requiredVolume ? Color.Cyan : Color.OrangeRed);
            y += 26;
        }
        spriteBatch.DrawString(_font, $"Reward: {mission.Reward:N0} CR", new Vector2(x, y), Color.Yellow);
        y += 30;
        string action = _missionManager.ActiveMission == null ? "[ENTER] ACCEPT" : "ACTIVE MISSION BLOCKS ACCEPT";
        if (_missionManager.UnclaimedCompletedMission != null) action = "[ENTER] CLAIM REWARD";
        spriteBatch.DrawString(_font, action, new Vector2(x, Math.Min(y + 40, detailPanel.Bottom - 34)), Color.Lime);
    }

    private void DrawMarketOpportunityStrip(SpriteBatch spriteBatch, Rectangle panel)
    {
        if (_marketOpportunities == null || _marketOpportunities.Count == 0)
        {
            spriteBatch.DrawString(_font, "MARKET OPPORTUNITIES: No known routes or signals", new Vector2(panel.X + 24, panel.Y + 44), Color.Gray);
            return;
        }

        string summary = string.Join("  |  ", _marketOpportunities.Take(3).Select(opportunity => opportunity.GetDisplayText()));
        spriteBatch.DrawString(_font, $"MARKET OPPORTUNITIES: {Shorten(summary, 122)}", new Vector2(panel.X + 24, panel.Y + 44), Color.LightGreen);
    }

    private void SetStatus(string message, bool success)
    {
        _statusMessage = string.IsNullOrWhiteSpace(message) ? "Transaction rejected." : message;
        _statusRemaining = 5f;
    }

    private static bool Pressed(KeyboardState current, KeyboardState previous, Keys key) =>
        current.IsKeyDown(key) && previous.IsKeyUp(key);

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
        return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private static IEnumerable<string> Wrap(string value, int maxLength)
    {
        string[] words = (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string line = string.Empty;
        foreach (string word in words)
        {
            if ((line + " " + word).Trim().Length > maxLength)
            {
                if (line.Length > 0) yield return line;
                line = word;
            }
            else line = (line + " " + word).Trim();
        }
        if (line.Length > 0) yield return line;
    }

    private static Color TypeColor(MissionType type) => type switch
    {
        MissionType.ReachLocation => Color.Cyan,
        MissionType.DestroyHostiles => Color.IndianRed,
        MissionType.CourierDelivery => Color.LimeGreen,
        MissionType.FreightContract => Color.LightSkyBlue,
        MissionType.ExportContract => Color.LightGreen,
        MissionType.Bounty => Color.Red,
        MissionType.Escort => Color.Yellow,
        _ => Color.White
    };

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
