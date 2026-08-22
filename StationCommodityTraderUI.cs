using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace Roguelancer;

/// <summary>
/// Physical on-foot Commodity Trader terminal. The terminal is only a view and
/// input layer; CommodityDealer, MarketManager, CargoHold, and PlayerCredits
/// remain authoritative for all transactions.
/// </summary>
public sealed class StationCommodityTraderUI
{
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly CommodityDealer _commodityDealer;
    private readonly Action<string> _showMessage;
    private readonly TradePlanManager _tradePlanManager;

    private string _stationName = "Station";
    private Station _station;
    private PlayerCredits _credits;
    private Ship _playerShip;
    private int _selectedIndex;
    private int _quantity = 1;
    private bool _buying = true;
    private bool _inputGate;
    private string _statusMessage = string.Empty;
    private bool _statusSuccess;
    private float _statusRemaining;

    public StationCommodityTraderUI(
        SpriteFont font,
        Texture2D pixel,
        CommodityDealer commodityDealer,
        Action<string> showMessage = null,
        TradePlanManager tradePlanManager = null)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
        _commodityDealer = commodityDealer ?? throw new ArgumentNullException(nameof(commodityDealer));
        _showMessage = showMessage ?? (_ => { });
        _tradePlanManager = tradePlanManager;
    }

    public bool IsOpen { get; private set; }
    public Station CurrentStation => _station;
    public int SelectedIndex => _selectedIndex;
    public int Quantity => _quantity;
    public bool IsBuying => _buying;

    public void Open(string stationName, Station station, PlayerCredits credits, Ship playerShip)
    {
        _stationName = string.IsNullOrWhiteSpace(stationName) ? station?.Name ?? "Station" : stationName;
        _station = station;
        _credits = credits;
        _playerShip = playerShip;
        _commodityDealer.SetDockedStation(station);
        _commodityDealer.RefreshMarketIntelligence();
        _selectedIndex = 0;
        _quantity = 1;
        _buying = true;
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
        if (_statusRemaining <= 0f)
        {
            return;
        }

        _statusRemaining = MathF.Max(0f, _statusRemaining - MathF.Max(0f, deltaSeconds));
        if (_statusRemaining <= 0f)
        {
            _statusMessage = string.Empty;
        }
    }

    public bool HandleInput(KeyboardState current, KeyboardState previous)
    {
        if (!IsOpen)
        {
            return false;
        }

        if (_inputGate)
        {
            if (current.IsKeyUp(Keys.E))
            {
                _inputGate = false;
            }

            return true;
        }

        if (Pressed(current, previous, Keys.Escape))
        {
            Close();
            return true;
        }

        IReadOnlyList<StationMarketListing> listings = _commodityDealer.CurrentMarketListings;
        SyncSelection(listings);

        if (Pressed(current, previous, Keys.Up) || Pressed(current, previous, Keys.W))
        {
            MoveSelection(-1, listings.Count);
            return true;
        }

        if (Pressed(current, previous, Keys.Down) || Pressed(current, previous, Keys.D))
        {
            MoveSelection(1, listings.Count);
            return true;
        }

        if (Pressed(current, previous, Keys.B))
        {
            _buying = true;
            NormalizeQuantity(listings);
            return true;
        }

        if (Pressed(current, previous, Keys.S))
        {
            _buying = false;
            NormalizeQuantity(listings);
            return true;
        }

        if (Pressed(current, previous, Keys.OemPlus))
        {
            _quantity = Math.Min(999, _quantity + 1);
            NormalizeQuantity(listings, allowZeroMaximum: true);
            return true;
        }

        if (Pressed(current, previous, Keys.OemMinus))
        {
            _quantity = Math.Max(1, _quantity - 1);
            return true;
        }

        if (Pressed(current, previous, Keys.Enter) || Pressed(current, previous, Keys.E))
        {
            ExecuteSelected(listings);
            return true;
        }

        return true;
    }

    public void Draw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice)
    {
        if (!IsOpen || spriteBatch == null || graphicsDevice == null)
        {
            return;
        }

        int width = graphicsDevice.Viewport.Width;
        int height = graphicsDevice.Viewport.Height;
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), Color.Black * 0.74f);

        int panelWidth = Math.Min(1240, width - 64);
        int panelHeight = Math.Min(760, height - 64);
        int panelX = (width - panelWidth) / 2;
        int panelY = (height - panelHeight) / 2;
        Rectangle panel = new(panelX, panelY, panelWidth, panelHeight);
        spriteBatch.Draw(_pixel, panel, new Color(8, 14, 24) * 0.98f);
        DrawBorder(spriteBatch, panel, Color.Gold, 3);

        string title = $"COMMODITY TRADER - {_stationName}";
        spriteBatch.DrawString(_font, title, new Vector2(panel.X + 24, panel.Y + 16), Color.Gold);
        string creditsText = $"Credits: {_credits?.GetFormattedCredits() ?? "0"} CR";
        Vector2 creditsSize = _font.MeasureString(creditsText);
        spriteBatch.DrawString(_font, creditsText, new Vector2(panel.Right - creditsSize.X - 24, panel.Y + 18), Color.Yellow);

        int freeCargo = _playerShip?.CargoHold?.AvailableCapacity ?? 0;
        string cargoText = $"Free Cargo: {freeCargo}   Used: {_playerShip?.CargoHold?.UsedCapacity ?? 0}/{_playerShip?.CargoHold?.MaxCapacity ?? 0}";
        spriteBatch.DrawString(_font, cargoText, new Vector2(panel.X + 24, panel.Y + 44), Color.LightSkyBlue);
        DrawTradePlanContext(spriteBatch, panel);

        Rectangle content = new(panel.X + 18, panel.Y + 78, panel.Width - 36, panel.Height - 142);
        int dividerX = content.X + (int)(content.Width * 0.43f);
        Rectangle listPanel = new(content.X, content.Y, dividerX - content.X - 10, content.Height);
        Rectangle detailPanel = new(dividerX + 10, content.Y, content.Right - dividerX - 10, content.Height);
        DrawBorder(spriteBatch, listPanel, Color.DarkSlateGray, 2);
        DrawBorder(spriteBatch, detailPanel, Color.DarkSlateGray, 2);

        IReadOnlyList<StationMarketListing> listings = _commodityDealer.CurrentMarketListings;
        SyncSelection(listings);
        spriteBatch.DrawString(_font, "MARKET", new Vector2(listPanel.X + 14, listPanel.Y + 12), Color.LightSkyBlue);
        string mode = _buying ? "[B] BUY" : "[S] SELL";
        Vector2 modeSize = _font.MeasureString(mode);
        spriteBatch.DrawString(_font, mode, new Vector2(listPanel.Right - modeSize.X - 14, listPanel.Y + 12), _buying ? Color.Lime : Color.Orange);

        int rowTop = listPanel.Y + 48;
        int rowHeight = 40;
        int maxRows = Math.Max(1, (listPanel.Height - 58) / rowHeight);
        int firstRow = Math.Clamp(_selectedIndex - maxRows / 2, 0, Math.Max(0, listings.Count - maxRows));
        for (int i = firstRow; i < Math.Min(listings.Count, firstRow + maxRows); i++)
        {
            StationMarketListing listing = listings[i];
            Commodity commodity = listing?.Commodity;
            if (commodity == null) continue;

            int rowY = rowTop + (i - firstRow) * rowHeight;
            Rectangle row = new(listPanel.X + 8, rowY, listPanel.Width - 16, rowHeight - 4);
            bool selected = i == _selectedIndex;
            if (selected) spriteBatch.Draw(_pixel, row, commodity.DisplayColor * 0.22f);
            bool plannedCommodity = IsPlannedTradeCommodity(commodity);
            DrawBorder(spriteBatch, row, selected ? commodity.DisplayColor : plannedCommodity ? Color.LimeGreen : Color.DarkSlateGray, selected ? 2 : 1);

            string label = $"{(selected ? "> " : "  ")}{Shorten(commodity.Name, plannedCommodity ? 13 : 24)}";
            if (plannedCommodity) label += " [TRADE ROUTE]";
            Color labelColor = listing.IsAvailable ? Color.White : Color.Gray;
            spriteBatch.DrawString(_font, label, new Vector2(row.X + 10, row.Y + 7), labelColor);
            string price = listing.IsAvailable
                ? $"B {listing.BuyPrice:N0} / S {listing.SellPrice:N0}  {FormatMovement(listing.BuyPriceMovementPercent)}"
                : "UNAVAILABLE";
            Vector2 priceSize = _font.MeasureString(price);
            spriteBatch.DrawString(_font, price, new Vector2(row.Right - priceSize.X - 10, row.Y + 7), listing.IsAvailable ? Color.Yellow : Color.Gray);
            int sellable = _playerShip?.CargoHold?.GetSellableCommodityQuantity(commodity.Name) ?? 0;
            spriteBatch.DrawString(_font, $"Owned {sellable} sellable  |  {commodity.VolumePerUnit}/unit", new Vector2(row.X + 10, row.Y + 23), Color.LightGray);
        }

        DrawSelectedDetails(spriteBatch, detailPanel, listings);

        string footer = "UP/DOWN or W/D: Select   B/S: Buy/Sell   +/-: Quantity   ENTER/E: Confirm   ESC: Back";
        spriteBatch.DrawString(_font, Shorten(footer, 130), new Vector2(panel.X + 24, panel.Bottom - 42), Color.LightGray);
        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            string status = Shorten(_statusMessage, 76);
            Vector2 statusSize = _font.MeasureString(status);
            spriteBatch.DrawString(_font, status, new Vector2(panel.Right - statusSize.X - 24, panel.Bottom - 42), _statusSuccess ? Color.Lime : Color.OrangeRed);
        }
    }

    private void DrawSelectedDetails(SpriteBatch spriteBatch, Rectangle detailPanel, IReadOnlyList<StationMarketListing> listings)
    {
        spriteBatch.DrawString(_font, "SELECTED", new Vector2(detailPanel.X + 16, detailPanel.Y + 12), Color.LightSkyBlue);
        if (listings == null || _selectedIndex < 0 || _selectedIndex >= listings.Count || listings[_selectedIndex]?.Commodity == null)
        {
            spriteBatch.DrawString(_font, "No market listing selected.", new Vector2(detailPanel.X + 16, detailPanel.Y + 56), Color.Gray);
            return;
        }

        StationMarketListing listing = listings[_selectedIndex];
        Commodity commodity = listing.Commodity;
        CargoHold cargo = _playerShip?.CargoHold;
        int owned = cargo?.GetCommodityQuantity(commodity.Name) ?? 0;
        int protectedQuantity = cargo?.GetMissionReservedQuantity(commodity.Name) ?? 0;
        int sellable = cargo?.GetSellableCommodityQuantity(commodity.Name) ?? 0;
        int maximum = GetMaximumQuantity(listing);

        int x = detailPanel.X + 16;
        int y = detailPanel.Y + 50;
        spriteBatch.DrawString(_font, Shorten(commodity.Name, 40), new Vector2(x, y), commodity.DisplayColor);
        y += 30;
        spriteBatch.DrawString(_font, Shorten(commodity.Description, 58), new Vector2(x, y), Color.LightGray);
        y += 42;
        spriteBatch.DrawString(_font, $"Buy: {(listing.BuyPrice > 0 ? $"{listing.BuyPrice:N0} CR" : "N/A")}", new Vector2(x, y), Color.Yellow);
        y += 24;
        spriteBatch.DrawString(_font, $"Sell: {(listing.SellPrice > 0 ? $"{listing.SellPrice:N0} CR" : "N/A")}", new Vector2(x, y), Color.LightGreen);
        y += 24;
        spriteBatch.DrawString(_font, $"Cargo: {commodity.VolumePerUnit} space / unit", new Vector2(x, y), Color.LightSkyBlue);
        y += 24;
        spriteBatch.DrawString(_font, $"Owned: {owned}   Sellable: {sellable}", new Vector2(x, y), Color.White);
        y += 24;
        if (protectedQuantity > 0)
        {
            spriteBatch.DrawString(_font, $"Mission-protected: {protectedQuantity} (cannot sell)", new Vector2(x, y), Color.OrangeRed);
            y += 24;
        }

        string stock = listing.IsAvailable
            ? $"Station stock: {listing.Stock:N0}/{listing.MaximumStock:N0}   {listing.MarketCondition}"
            : "UNAVAILABLE HERE";
        spriteBatch.DrawString(_font, stock, new Vector2(x, y), listing.IsAvailable ? Color.Cyan : Color.Gray);
        y += 24;
        if (listing.IsAvailable)
        {
            string movement = $"Normal: B {listing.BaseBuyPrice:N0} / S {listing.BaseSellPrice:N0}   Current: {FormatMovement(listing.BuyPriceMovementPercent)}";
            spriteBatch.DrawString(_font, Shorten(movement, 58), new Vector2(x, y), Color.LightGray);
        }
        y += 32;
        spriteBatch.DrawString(_font, $"Quantity: {_quantity}   Maximum now: {maximum}", new Vector2(x, y), Color.White);
        y += 28;
        string total = _buying
            ? $"Total cost: {SafeTotal(listing.BuyPrice, _quantity):N0} CR   Space: {(long)commodity.VolumePerUnit * _quantity}"
            : $"Total value: {SafeTotal(listing.SellPrice, _quantity):N0} CR   Space freed: {(long)commodity.VolumePerUnit * _quantity}";
        spriteBatch.DrawString(_font, total, new Vector2(x, y), Color.Cyan);
        y += 34;

        string action = _buying
            ? listing.IsAvailable && listing.BuyPrice > 0 && listing.Stock > listing.MinimumStock ? "[ENTER] BUY" : "BUY UNAVAILABLE"
            : protectedQuantity > 0 && sellable == 0 ? "MISSION CARGO CANNOT BE SOLD" : sellable > 0 && listing.SellPrice > 0 ? "[ENTER] SELL" : "NOTHING SELLABLE";
        spriteBatch.DrawString(_font, Shorten(action, 58), new Vector2(x, Math.Min(y, detailPanel.Bottom - 42)), _buying ? Color.Lime : Color.Orange);
    }

    private void DrawTradePlanContext(SpriteBatch spriteBatch, Rectangle panel)
    {
        TradePlan plan = _tradePlanManager?.ActivePlan;
        if (plan == null || _station == null) return;

        string stationId = _commodityDealer.MarketManager.GetStationId(_station);
        bool atSource = string.Equals(stationId, plan.SourceStationId, StringComparison.OrdinalIgnoreCase);
        bool atDestination = string.Equals(stationId, plan.DestinationStationId, StringComparison.OrdinalIgnoreCase);
        if (!atSource && !atDestination) return;

        string context = $"{(atSource ? "TRADE ROUTE SOURCE" : "TRADE ROUTE DESTINATION")} | Planned commodity: {plan.CommodityName}";
        spriteBatch.DrawString(_font, Shorten(context, 110), new Microsoft.Xna.Framework.Vector2(panel.X + 24, panel.Y + 62), atSource ? Microsoft.Xna.Framework.Color.LimeGreen : Microsoft.Xna.Framework.Color.LightSkyBlue);
    }

    private bool IsPlannedTradeCommodity(Commodity commodity)
    {
        return commodity != null && _tradePlanManager?.ActivePlan != null &&
            string.Equals(commodity.Id, _tradePlanManager.ActivePlan.CommodityId, StringComparison.OrdinalIgnoreCase);
    }

    private void ExecuteSelected(IReadOnlyList<StationMarketListing> listings)
    {
        if (listings == null || _selectedIndex < 0 || _selectedIndex >= listings.Count)
        {
            SetStatus("No commodity selected.", false);
            return;
        }

        Commodity commodity = listings[_selectedIndex]?.Commodity;
        if (commodity == null || _credits == null || _playerShip?.CargoHold == null)
        {
            SetStatus("Trading terminal is unavailable.", false);
            _showMessage(_statusMessage);
            return;
        }

        string message;
        bool success;
        if (_buying)
        {
            success = _commodityDealer.TryBuyCommodity(commodity, _quantity, _credits, _playerShip.CargoHold, out message);
        }
        else
        {
            success = _commodityDealer.TrySellCommodity(commodity, _quantity, _credits, _playerShip.CargoHold, out message);
        }

        SetStatus(message, success);
        _showMessage(message);
        NormalizeQuantity(_commodityDealer.CurrentMarketListings);
    }

    private int GetMaximumQuantity(StationMarketListing listing)
    {
        if (listing?.Commodity == null || !listing.IsAvailable)
        {
            return 0;
        }

        if (!_buying)
        {
            return Math.Min(999, _playerShip?.CargoHold?.GetSellableCommodityQuantity(listing.Commodity.Name) ?? 0);
        }

        int max = Math.Min(999, Math.Max(0, listing.Stock - listing.MinimumStock));
        if (listing.BuyPrice <= 0 || _credits == null)
        {
            return 0;
        }

        max = Math.Min(max, _credits.Credits / listing.BuyPrice);
        if (_playerShip?.CargoHold != null && listing.Commodity.VolumePerUnit > 0)
        {
            max = Math.Min(max, _playerShip.CargoHold.AvailableCapacity / listing.Commodity.VolumePerUnit);
        }

        return Math.Max(0, max);
    }

    private void SyncSelection(IReadOnlyList<StationMarketListing> listings)
    {
        int count = listings?.Count ?? 0;
        _selectedIndex = count == 0 ? 0 : Math.Clamp(_selectedIndex, 0, count - 1);
        NormalizeQuantity(listings);
    }

    private void NormalizeQuantity(IReadOnlyList<StationMarketListing> listings, bool allowZeroMaximum = false)
    {
        if (listings == null || _selectedIndex < 0 || _selectedIndex >= listings.Count)
        {
            _quantity = Math.Max(1, _quantity);
            return;
        }

        int maximum = GetMaximumQuantity(listings[_selectedIndex]);
        if (maximum > 0)
        {
            _quantity = Math.Clamp(_quantity, 1, maximum);
        }
        else if (!allowZeroMaximum)
        {
            _quantity = Math.Max(1, _quantity);
        }
    }

    private void MoveSelection(int direction, int count)
    {
        if (count <= 0)
        {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = (_selectedIndex + direction) % count;
        if (_selectedIndex < 0) _selectedIndex += count;
        _quantity = 1;
    }

    private void SetStatus(string message, bool success)
    {
        _statusMessage = string.IsNullOrWhiteSpace(message) ? "Transaction rejected." : message;
        _statusSuccess = success;
        _statusRemaining = 5f;
    }

    private static bool Pressed(KeyboardState current, KeyboardState previous, Keys key)
    {
        return current.IsKeyDown(key) && previous.IsKeyUp(key);
    }

    private static int SafeTotal(int unitPrice, int quantity)
    {
        long total = (long)Math.Max(0, unitPrice) * Math.Max(0, quantity);
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    private static string FormatMovement(int percent)
    {
        return percent == 0 ? "NORMAL" : percent > 0 ? $"+{percent}%" : $"{percent}%";
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
        return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
