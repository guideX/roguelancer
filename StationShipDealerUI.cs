using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace Roguelancer;

/// <summary>
/// On-foot station service screen for the physical Ship Dealer NPC. It is an
/// overlay over the active station scene, so opening it never creates a second
/// station or replaces the authoritative player state.
/// </summary>
public sealed class StationShipDealerUI {
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly ShipDealer _shipDealer;
    private readonly Action<string> _showMessage;

    private string _stationName = "Station";
    private PlayerCredits _credits;
    private Ship _playerShip;
    private int _selectedIndex;
    private bool _inputGate;
    private string _statusMessage = string.Empty;
    private float _statusRemaining;

    public StationShipDealerUI(SpriteFont font, Texture2D pixel, ShipDealer shipDealer, Action<string> showMessage = null) {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
        _shipDealer = shipDealer ?? throw new ArgumentNullException(nameof(shipDealer));
        _showMessage = showMessage ?? (_ => { });
    }

    public bool IsOpen { get; private set; }

    public event Action<ShipDefinition> OnShipPurchased;

    public void Open(string stationName, PlayerCredits credits, Ship playerShip) {
        _stationName = string.IsNullOrWhiteSpace(stationName) ? "Station" : stationName;
        _credits = credits;
        _playerShip = playerShip;
        _statusMessage = string.Empty;
        _statusRemaining = 0f;
        _selectedIndex = FindFirstReplacementIndex();
        _inputGate = true;
        IsOpen = true;
    }

    public void Close() {
        IsOpen = false;
        _inputGate = false;
        _statusMessage = string.Empty;
        _statusRemaining = 0f;
    }

    public void Update(float deltaSeconds) {
        if (_statusRemaining <= 0f) return;
        _statusRemaining = MathF.Max(0f, _statusRemaining - MathF.Max(0f, deltaSeconds));
        if (_statusRemaining <= 0f) _statusMessage = string.Empty;
    }

    /// <summary>
    /// Consume all station input while open. Every action is edge-triggered and
    /// the opening E press is gated until the key has been released.
    /// </summary>
    public bool HandleInput(KeyboardState keyboardState, KeyboardState previousKeyboardState) {
        if (!IsOpen) return false;

        if (_inputGate) {
            if (keyboardState.IsKeyUp(Keys.E)) _inputGate = false;
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.Escape)) {
            Close();
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.Up) || Pressed(keyboardState, previousKeyboardState, Keys.W)) {
            MoveSelection(-1);
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.Down) || Pressed(keyboardState, previousKeyboardState, Keys.S)) {
            MoveSelection(1);
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.Enter) || Pressed(keyboardState, previousKeyboardState, Keys.E)) {
            PurchaseSelectedShip();
            return true;
        }

        return true;
    }

    public void Draw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice) {
        if (!IsOpen || spriteBatch == null || graphicsDevice == null) return;

        int width = graphicsDevice.Viewport.Width;
        int height = graphicsDevice.Viewport.Height;
        Rectangle overlay = new(0, 0, width, height);
        spriteBatch.Draw(_pixel, overlay, Color.Black * 0.72f);

        int panelWidth = Math.Min(1160, width - 80);
        int panelHeight = Math.Min(700, height - 90);
        int panelX = (width - panelWidth) / 2;
        int panelY = (height - panelHeight) / 2;
        Rectangle panel = new(panelX, panelY, panelWidth, panelHeight);
        spriteBatch.Draw(_pixel, panel, new Color(9, 15, 25) * 0.97f);
        DrawBorder(spriteBatch, panel, Color.Gold, 3);

        string title = $"SHIP DEALER — {_stationName}";
        spriteBatch.DrawString(_font, title, new Vector2(panel.X + 24, panel.Y + 18), Color.Gold);
        string creditsText = $"Credits: {_credits?.GetFormattedCredits() ?? "0"} CR";
        Vector2 creditsSize = _font.MeasureString(creditsText);
        spriteBatch.DrawString(_font, creditsText, new Vector2(panel.Right - creditsSize.X - 24, panel.Y + 18), Color.Yellow);

        int contentTop = panel.Y + 68;
        int footerHeight = 58;
        int contentBottom = panel.Bottom - footerHeight;
        int dividerX = panel.X + (int)(panel.Width * 0.42f);
        Rectangle listPanel = new(panel.X + 18, contentTop, dividerX - panel.X - 28, contentBottom - contentTop);
        Rectangle detailPanel = new(dividerX + 10, contentTop, panel.Right - dividerX - 28, contentBottom - contentTop);
        DrawBorder(spriteBatch, listPanel, Color.DarkSlateGray, 2);
        DrawBorder(spriteBatch, detailPanel, Color.DarkSlateGray, 2);

        spriteBatch.DrawString(_font, "AVAILABLE SHIPS", new Vector2(listPanel.X + 14, listPanel.Y + 12), Color.LightSkyBlue);
        IReadOnlyList<ShipDefinition> ships = _shipDealer.AvailableShips;
        int listY = listPanel.Y + 48;
        int rowHeight = 44;
        for (int i = 0; i < ships.Count; i++) {
            int rowY = listY + i * rowHeight;
            if (rowY + rowHeight > listPanel.Bottom - 10) break;

            ShipDefinition ship = ships[i];
            bool selected = i == _selectedIndex;
            bool current = IsCurrentShip(ship);
            Rectangle row = new(listPanel.X + 8, rowY, listPanel.Width - 16, rowHeight - 4);
            if (selected) spriteBatch.Draw(_pixel, row, Color.Orange * 0.28f);
            DrawBorder(spriteBatch, row, selected ? Color.Orange : Color.DarkSlateGray, selected ? 2 : 1);

            string label = $"{(selected ? "> " : "  ")}{ship.Name}";
            spriteBatch.DrawString(_font, label, new Vector2(row.X + 10, row.Y + 8), current ? Color.Cyan : Color.White);
            if (current) {
                string currentLabel = "CURRENT";
                Vector2 currentSize = _font.MeasureString(currentLabel);
                spriteBatch.DrawString(_font, currentLabel, new Vector2(row.Right - currentSize.X - 10, row.Y + 8), Color.Cyan);
            }
        }

        ShipDefinition selectedShip = GetSelectedShip();
        spriteBatch.DrawString(_font, "SELECTED SHIP", new Vector2(detailPanel.X + 16, detailPanel.Y + 12), Color.LightSkyBlue);
        if (selectedShip == null) {
            spriteBatch.DrawString(_font, "No valid ship inventory.", new Vector2(detailPanel.X + 16, detailPanel.Y + 54), Color.OrangeRed);
        } else {
            DrawSelectedShip(spriteBatch, detailPanel, selectedShip);
        }

        string footer = "UP/DOWN or W/S: Select    ENTER/E: Purchase    ESC: Back";
        spriteBatch.DrawString(_font, footer, new Vector2(panel.X + 24, panel.Bottom - 42), Color.LightGray);
        if (!string.IsNullOrWhiteSpace(_statusMessage)) {
            Vector2 statusSize = _font.MeasureString(_statusMessage);
            Color statusColor = _statusMessage.StartsWith("Purchased", StringComparison.OrdinalIgnoreCase)
                ? Color.Lime
                : Color.OrangeRed;
            spriteBatch.DrawString(_font, _statusMessage, new Vector2(panel.Right - statusSize.X - 24, panel.Bottom - 42), statusColor);
        }
    }

    private void DrawSelectedShip(SpriteBatch spriteBatch, Rectangle detailPanel, ShipDefinition ship) {
        int x = detailPanel.X + 16;
        int y = detailPanel.Y + 48;
        bool current = IsCurrentShip(ship);
        bool canPurchase = _shipDealer.CanPurchaseShip(ship, _credits, _playerShip, out string validationMessage);

        spriteBatch.DrawString(_font, ship.Name, new Vector2(x, y), Color.White);
        y += 30;
        string description = Shorten(ship.Description, 58);
        spriteBatch.DrawString(_font, description, new Vector2(x, y), Color.LightGray);
        y += 34;

        int purchaseCost = _shipDealer.GetTotalCost(ship);
        string price = current ? "CURRENT SHIP" : $"Price: {ship.Price:N0} CR   Due: {purchaseCost:N0} CR";
        spriteBatch.DrawString(_font, price, new Vector2(x, y), current ? Color.Cyan : Color.Yellow);
        y += 34;

        spriteBatch.DrawString(_font, "HULL", new Vector2(x, y), Color.LightSkyBlue);
        spriteBatch.DrawString(_font, $"{ship.MaxHull:0}", new Vector2(x + 126, y), Color.White);
        y += 24;
        spriteBatch.DrawString(_font, "SHIELDS", new Vector2(x, y), Color.LightSkyBlue);
        spriteBatch.DrawString(_font, $"{ship.MaxShields:0}", new Vector2(x + 126, y), Color.White);
        y += 24;
        spriteBatch.DrawString(_font, "MAX SPEED", new Vector2(x, y), Color.LightSkyBlue);
        spriteBatch.DrawString(_font, $"{ship.MaxSpeed:0}", new Vector2(x + 126, y), Color.White);
        y += 24;
        spriteBatch.DrawString(_font, "TURN", new Vector2(x, y), Color.LightSkyBlue);
        spriteBatch.DrawString(_font, $"{ship.TurnSpeed:0.0}", new Vector2(x + 126, y), Color.White);
        y += 24;
        spriteBatch.DrawString(_font, "CARGO", new Vector2(x, y), Color.LightSkyBlue);
        spriteBatch.DrawString(_font, $"{ship.CargoCapacity}", new Vector2(x + 126, y), Color.White);
        y += 34;

        string action = current ? "CURRENT SHIP" : canPurchase ? "[ENTER] PURCHASE" : validationMessage;
        Color actionColor = current ? Color.Cyan : canPurchase ? Color.Lime : Color.OrangeRed;
        spriteBatch.DrawString(_font, Shorten(action, 58), new Vector2(x, Math.Min(y, detailPanel.Bottom - 38)), actionColor);
    }

    private void PurchaseSelectedShip() {
        ShipDefinition selectedShip = GetSelectedShip();
        if (selectedShip == null) {
            SetStatus("No ship selected.");
            return;
        }

        if (_shipDealer.TryPurchaseShip(selectedShip, _credits, _playerShip, out string message)) {
            OnShipPurchased?.Invoke(selectedShip);
            Close();
            return;
        }

        SetStatus(message);
        _showMessage(message);
    }

    private int FindFirstReplacementIndex() {
        for (int i = 0; i < _shipDealer.AvailableShips.Count; i++) {
            if (!IsCurrentShip(_shipDealer.AvailableShips[i])) return i;
        }
        return 0;
    }

    private void MoveSelection(int direction) {
        int count = _shipDealer.AvailableShips.Count;
        if (count <= 0) {
            _selectedIndex = 0;
            return;
        }

        _selectedIndex = (_selectedIndex + direction) % count;
        if (_selectedIndex < 0) _selectedIndex += count;
        _statusMessage = string.Empty;
        _statusRemaining = 0f;
    }

    private ShipDefinition GetSelectedShip() {
        return _shipDealer.GetShipByIndex(_selectedIndex);
    }

    private bool IsCurrentShip(ShipDefinition ship) {
        return ship != null && _shipDealer.CurrentPlayerShip != null &&
            string.Equals(ship.Name, _shipDealer.CurrentPlayerShip.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void SetStatus(string message) {
        _statusMessage = string.IsNullOrWhiteSpace(message) ? "Transaction rejected." : message;
        _statusRemaining = 5f;
    }

    private static bool Pressed(KeyboardState current, KeyboardState previous, Keys key) {
        return current.IsKeyDown(key) && previous.IsKeyUp(key);
    }

    private static string Shorten(string value, int maxLength) {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
        return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness) {
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}