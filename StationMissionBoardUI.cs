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
    private readonly ReputationManager _reputationManager;
    private readonly JobBoard _jobBoard;
    private readonly CargoHold _cargoHold;
    private readonly Action<string> _showMessage;
    private readonly TradePlanManager _tradePlanManager;
    private readonly Action<bool> _plotTradePlan;
    private readonly Action _clearTradePlanNavigation;

    private string _stationName = "Station";
    private Station _station;
    private bool _inputGate;
    private string _statusMessage = string.Empty;
    private float _statusRemaining;
    private IReadOnlyList<MarketOpportunity> _marketOpportunities = Array.Empty<MarketOpportunity>();
    private int _marketSelection;
    private bool _marketFocus;
    private bool _showReputationOverview;

    public StationMissionBoardUI(
        SpriteFont font,
        Texture2D pixel,
        MissionManager missionManager,
        CargoHold cargoHold = null,
        Action<string> showMessage = null,
        TradePlanManager tradePlanManager = null,
        Action<bool> plotTradePlan = null,
        Action clearTradePlanNavigation = null,
        ReputationManager reputationManager = null)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
        _missionManager = missionManager ?? throw new ArgumentNullException(nameof(missionManager));
        _reputationManager = reputationManager;
        _jobBoard = new JobBoard(missionManager);
        _cargoHold = cargoHold;
        _showMessage = showMessage ?? (_ => { });
        _tradePlanManager = tradePlanManager;
        _plotTradePlan = plotTradePlan;
        _clearTradePlanNavigation = clearTradePlanNavigation;
    }

    public bool IsOpen { get; private set; }
    public IReadOnlyList<Mission> AvailableMissions => _jobBoard.AvailableMissions;

    public void Open(string stationName, Station station)
    {
        _stationName = string.IsNullOrWhiteSpace(stationName) ? "Station" : stationName;
        _station = station;
        _jobBoard.RefreshMissions(6, station?.FactionId, station);
        _marketOpportunities = _missionManager.GetKnownMarketOpportunities(5);
        _marketSelection = Math.Clamp(_marketSelection, 0, Math.Max(0, _marketOpportunities.Count - 1));
        _marketFocus = false;
        _statusMessage = string.Empty;
        _statusRemaining = 0f;
        _showReputationOverview = false;
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

        if (Pressed(current, previous, Keys.P))
        {
            _showReputationOverview = !_showReputationOverview;
            return true;
        }

        if (Pressed(current, previous, Keys.M))
        {
            _marketFocus = !_marketFocus;
            return true;
        }

        if (Pressed(current, previous, Keys.C) && _tradePlanManager?.ActivePlan != null)
        {
            if (_tradePlanManager.CancelActivePlan(out string tradeCancelMessage))
            {
                _clearTradePlanNavigation?.Invoke();
                SetStatus(tradeCancelMessage, success: true);
                _showMessage(tradeCancelMessage);
            }
            return true;
        }

        if (Pressed(current, previous, Keys.R))
        {
            PlotSelectedTradePlan();
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

        if (_marketFocus && (Pressed(current, previous, Keys.Up) || Pressed(current, previous, Keys.W)))
        {
            MoveMarketSelection(-1);
            return true;
        }

        if (_marketFocus && (Pressed(current, previous, Keys.Down) || Pressed(current, previous, Keys.S)))
        {
            MoveMarketSelection(1);
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
            if (_marketFocus)
            {
                PlotSelectedTradePlan();
                return true;
            }
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
            string rejection = _missionManager.LastAcceptanceFailureReason;
            SetStatus(string.IsNullOrWhiteSpace(rejection) ? "Mission acceptance rejected." : rejection, success: false);
            _showMessage(_statusMessage);
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
        spriteBatch.DrawString(_font, ReputationPresentation.BuildStationFactionLine(_station, _reputationManager), new Vector2(panel.X + 360, panel.Y + 18), Color.LightSkyBlue);
        Color stationStandingColor = _reputationManager?.IsFactionCurrentlyHostile(_station?.FactionId) == true
            ? Color.IndianRed
            : _reputationManager?.GetBand(_station?.FactionId) switch
        {
            ReputationBand.Hostile => Color.IndianRed,
            ReputationBand.Unfriendly => Color.Orange,
            ReputationBand.Friendly => Color.LightGreen,
            ReputationBand.Allied => Color.LimeGreen,
            _ => Color.LightGray
        };
        spriteBatch.DrawString(_font, ReputationPresentation.BuildStationStandingLine(_station, _reputationManager), new Vector2(panel.X + 360, panel.Y + 44), stationStandingColor);
        if (_showReputationOverview)
        {
            DrawReputationOverview(spriteBatch, panel);
            return;
        }
        DrawMarketOpportunityStrip(spriteBatch, panel);

        int contentTop = panel.Y + 166;
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
            MissionEligibilityResult eligibility = _missionManager.GetMissionEligibility(mission);
            Color rowAccent = eligibility.IsEligible ? Color.Orange : Color.DarkOrange;
            if (selected) spriteBatch.Draw(_pixel, row, rowAccent * 0.26f);
            DrawBorder(spriteBatch, row, selected ? rowAccent : Color.DarkSlateGray, selected ? 2 : 1);
            string lockLabel = eligibility.IsEligible ? string.Empty : "[LOCKED] ";
            Color titleColor = eligibility.IsEligible ? Color.White : Color.Orange;
            spriteBatch.DrawString(_font, $"{(selected ? "> " : "  ")}{lockLabel}{Shorten(mission.Title, 23)}", new Vector2(row.X + 10, row.Y + 8), titleColor);
            spriteBatch.DrawString(_font, mission.GetTypeLabel(), new Vector2(row.X + 28, row.Y + 32), eligibility.IsEligible ? TypeColor(mission.Type) : Color.OrangeRed);
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

        string footer = "M: Market focus   P: Reputation   M + UP/DOWN: Select route   R/ENTER: Plot   C: Cancel plan   ESC: Back";
        if (!_marketFocus)
            footer = "UP/DOWN or W/S: Select job   ENTER/E: Accept or Claim   P: Reputation   M: Market focus   R: Plot route   ESC: Back";
        if (_tradePlanManager?.ActivePlan == null && _missionManager.ActiveMission?.Type == MissionType.ExportContract)
            footer = "UP/DOWN or W/S: Select job   ENTER/E: Accept   C: Cancel export   P: Reputation   M: Market focus   ESC: Back";
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
        spriteBatch.DrawString(_font, ReputationPresentation.BuildMissionEmployerLine(mission, _reputationManager), new Vector2(x, y), Color.LightSkyBlue);
        y += 22;
        spriteBatch.DrawString(_font, ReputationPresentation.BuildMissionStandingLine(mission, _reputationManager), new Vector2(x, y), Color.LightGray);
        y += 26;
        spriteBatch.DrawString(_font, ReputationPresentation.BuildMissionRequirementLine(mission), new Vector2(x, y), Color.LightGreen);
        y += 24;
        spriteBatch.DrawString(_font, ReputationPresentation.BuildMissionRewardLine(mission), new Vector2(x, y), Color.MediumPurple);
        y += 28;

        MissionEligibilityResult eligibility = _missionManager.GetMissionEligibility(mission);
        if (!eligibility.IsEligible)
        {
            spriteBatch.DrawString(_font, eligibility.Reason, new Vector2(x, y), Color.OrangeRed);
            y += 28;
            spriteBatch.DrawString(_font, "[LOCKED] Earn the required standing to accept this job.", new Vector2(x, y), Color.Orange);
            return;
        }

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

    private void DrawReputationOverview(SpriteBatch spriteBatch, Rectangle panel)
    {
        Rectangle overview = new(panel.X + 48, panel.Y + 90, panel.Width - 96, panel.Height - 180);
        spriteBatch.Draw(_pixel, overview, new Color(12, 20, 32) * 0.98f);
        DrawBorder(spriteBatch, overview, Color.Gold, 2);
        spriteBatch.DrawString(_font, "REPUTATION OVERVIEW", new Vector2(overview.X + 22, overview.Y + 18), Color.Gold);
        int y = overview.Y + 66;
        foreach (ReputationOverviewLine line in ReputationPresentation.BuildOverview(_reputationManager))
        {
            Color bandColor = line.BandLabel switch
            {
                "HOSTILE" => Color.IndianRed,
                "UNFRIENDLY" => Color.Orange,
                "FRIENDLY" => Color.LightGreen,
                "ALLIED" => Color.LimeGreen,
                _ => Color.LightGray
            };
            spriteBatch.DrawString(_font, line.DisplayName, new Vector2(overview.X + 28, y), Color.White);
            spriteBatch.DrawString(_font, $"{line.BandLabel} ({ReputationManager.FormatStanding(line.Value)})", new Vector2(overview.X + 390, y), bandColor);
            if (!string.IsNullOrWhiteSpace(line.TransientLabel))
                spriteBatch.DrawString(_font, line.TransientLabel, new Vector2(overview.X + 650, y), Color.OrangeRed);
            y += 34;
        }
        spriteBatch.DrawString(_font, "P: Return to mission board", new Vector2(overview.X + 28, overview.Bottom - 48), Color.LightGray);
    }

    private void DrawMarketOpportunityStrip(SpriteBatch spriteBatch, Rectangle panel)
    {
        if (_marketOpportunities == null || _marketOpportunities.Count == 0)
        {
            Rectangle emptyPanel = new(panel.X + 18, panel.Y + 42, panel.Width - 36, 112);
            DrawBorder(spriteBatch, emptyPanel, Color.DarkSlateGray, 2);
            spriteBatch.DrawString(_font, "MARKET OPPORTUNITIES", new Vector2(emptyPanel.X + 14, emptyPanel.Y + 10), Color.LightSkyBlue);
            spriteBatch.DrawString(_font, "NO KNOWN MARKET DATA", new Vector2(emptyPanel.X + 14, emptyPanel.Y + 48), Color.Gray);
            return;
        }

        _marketSelection = Math.Clamp(_marketSelection, 0, _marketOpportunities.Count - 1);
        MarketOpportunity selected = _marketOpportunities[_marketSelection];
        Rectangle opportunityPanel = new(panel.X + 18, panel.Y + 42, panel.Width - 36, 112);
        DrawBorder(spriteBatch, opportunityPanel, _marketFocus ? Color.Orange : Color.DarkSlateGray, 2);

        int dividerX = opportunityPanel.X + (int)(opportunityPanel.Width * 0.58f);
        Rectangle listPanel = new(opportunityPanel.X + 8, opportunityPanel.Y + 6, dividerX - opportunityPanel.X - 16, opportunityPanel.Height - 12);
        Rectangle detailPanel = new(dividerX + 8, opportunityPanel.Y + 6, opportunityPanel.Right - dividerX - 16, opportunityPanel.Height - 12);
        spriteBatch.DrawString(_font, $"MARKET OPPORTUNITIES [{_marketSelection + 1}/{_marketOpportunities.Count}]", new Vector2(listPanel.X + 4, listPanel.Y + 2), _marketFocus ? Color.Orange : Color.LightGreen);

        int visibleRows = Math.Min(3, _marketOpportunities.Count);
        int firstRow = Math.Clamp(_marketSelection - visibleRows / 2, 0, Math.Max(0, _marketOpportunities.Count - visibleRows));
        int rowTop = listPanel.Y + 28;
        for (int rowIndex = 0; rowIndex < visibleRows; rowIndex++)
        {
            int opportunityIndex = firstRow + rowIndex;
            MarketOpportunity opportunity = _marketOpportunities[opportunityIndex];
            bool isSelected = opportunityIndex == _marketSelection;
            Rectangle row = new(listPanel.X, rowTop + rowIndex * 23, listPanel.Width, 21);
            if (isSelected) spriteBatch.Draw(_pixel, row, (_marketFocus ? Color.Orange : Color.LightGreen) * 0.24f);
            string prefix = isSelected ? "> " : "  ";
            spriteBatch.DrawString(_font, prefix + FormatOpportunityRow(opportunity), new Vector2(row.X + 4, row.Y + 2), isSelected ? Color.White : Color.LightGray);
        }

        spriteBatch.DrawString(_font, "SELECTED OPPORTUNITY", new Vector2(detailPanel.X + 4, detailPanel.Y + 2), Color.LightSkyBlue);
        int detailY = detailPanel.Y + 27;
        foreach (string line in BuildMarketOpportunityDetailLines(selected).Take(4))
        {
            spriteBatch.DrawString(_font, Shorten(line, 43), new Vector2(detailPanel.X + 4, detailY), Color.White);
            detailY += 20;
        }
    }

    private string FormatOpportunityRow(MarketOpportunity opportunity)
    {
        if (opportunity == null) return "MARKET DATA UNKNOWN";
        string route = opportunity.RouteHops == 1 ? "1 jump" : $"{Math.Max(0, opportunity.RouteHops):N0} jumps";
        string spread = opportunity.Type == MarketOpportunityType.TradeRoute && opportunity.CurrentSpread > 0
            ? $" +{opportunity.CurrentSpread:N0} CR/unit"
            : string.Empty;
        return Shorten($"{opportunity.GetTypeLabel()} {opportunity.CommodityName}: {opportunity.OriginStationName} -> {opportunity.DestinationStationName}{spread} | {route}", 68);
    }

    private IReadOnlyList<string> BuildMarketOpportunityDetailLines(MarketOpportunity opportunity)
    {
        if (opportunity == null) return new[] { "MARKET DATA UNKNOWN" };
        if (opportunity.Type != MarketOpportunityType.TradeRoute)
        {
            return new[]
            {
                $"TYPE: {opportunity.GetTypeLabel()}",
                $"COMMODITY: {Shorten(opportunity.CommodityName, 28)}",
                $"STATION: {Shorten(opportunity.StationName, 28)}",
                $"{Shorten(opportunity.Reason, 38)} | {opportunity.Quantity:N0} units",
                "NO TRADE PLAN AVAILABLE"
            };
        }

        string sourcePrice = "PRICE UNKNOWN";
        string destinationPrice = "PRICE UNKNOWN";
        if (_tradePlanManager?.MarketIntelligence != null)
        {
            if (_tradePlanManager.MarketIntelligence.TryGetObservation(opportunity.OriginStationId, opportunity.CommodityId, out MarketObservation source))
                sourcePrice = source.BuyPrice > 0 ? $"BUY {source.BuyPrice:N0} CR" : "BUY PRICE UNKNOWN";
            if (_tradePlanManager.MarketIntelligence.TryGetObservation(opportunity.DestinationStationId, opportunity.CommodityId, out MarketObservation destination))
                destinationPrice = destination.SellPrice > 0 ? $"SELL {destination.SellPrice:N0} CR" : "SELL PRICE UNKNOWN";
        }

        string sourceAge = string.IsNullOrWhiteSpace(opportunity.SourceAgeBand) ? "UNKNOWN" : opportunity.SourceAgeBand;
        string destinationAge = string.IsNullOrWhiteSpace(opportunity.DestinationAgeBand) ? "UNKNOWN" : opportunity.DestinationAgeBand;
        string spread = opportunity.CurrentSpread > 0 ? $"+{opportunity.CurrentSpread:N0} CR/unit" : "UNKNOWN";
        string active = _tradePlanManager?.ActivePlan != null ? "ACTIVE ROUTE" : "R/ENTER: PLOT";
        return new[]
        {
            $"COMMODITY: {Shorten(opportunity.CommodityName, 28)}",
            $"ROUTE: {Shorten(opportunity.OriginStationName, 17)} -> {Shorten(opportunity.DestinationStationName, 17)}",
            $"PRICES: {sourcePrice} / {destinationPrice}",
            $"SPREAD: {spread} | {opportunity.RouteHops:N0} jumps",
            $"INTEL: {sourceAge} / {destinationAge} | SUGGESTED {opportunity.Quantity:N0}",
            active
        };
    }

    private void MoveMarketSelection(int direction)
    {
        if (_marketOpportunities == null || _marketOpportunities.Count == 0) return;
        _marketSelection = (_marketSelection + direction) % _marketOpportunities.Count;
        if (_marketSelection < 0) _marketSelection += _marketOpportunities.Count;
    }

    private void PlotSelectedTradePlan()
    {
        if (_tradePlanManager == null || _marketOpportunities == null || _marketOpportunities.Count == 0)
        {
            SetStatus("No exact market route is known.", success: false);
            return;
        }

        MarketOpportunity selected = _marketOpportunities[Math.Clamp(_marketSelection, 0, _marketOpportunities.Count - 1)];
        if (selected.Type != MarketOpportunityType.TradeRoute)
        {
            SetStatus("Select a trade route to plot.", success: false);
            return;
        }
        if (!_tradePlanManager.TryCreatePlan(selected, out string message))
        {
            SetStatus(message, success: false);
            _showMessage(message);
            return;
        }

        SetStatus(message, success: true);
        _showMessage(message);
        _plotTradePlan?.Invoke(true);
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
