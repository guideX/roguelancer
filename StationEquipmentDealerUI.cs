using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelancer;

/// <summary>
/// On-foot Equipment terminal overlay. The station scene remains active while
/// this UI is open; all actions operate on the current Ship.Loadout instance.
/// </summary>
public sealed class StationEquipmentDealerUI
{
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly EquipmentDealer _equipmentDealer;
    private readonly Action<string> _showMessage;

    private string _stationName = "Station";
    private PlayerCredits _credits;
    private Ship _playerShip;
    private int _selectedIndex;
    private bool _ownedMode;
    private bool _inputGate;
    private string _statusMessage = string.Empty;
    private float _statusRemaining;
    private bool _statusSuccess;

    public StationEquipmentDealerUI(SpriteFont font, Texture2D pixel, EquipmentDealer equipmentDealer, Action<string> showMessage = null)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
        _equipmentDealer = equipmentDealer ?? throw new ArgumentNullException(nameof(equipmentDealer));
        _showMessage = showMessage ?? (_ => { });
    }

    public bool IsOpen { get; private set; }

    public void Open(string stationName, PlayerCredits credits, Ship playerShip)
    {
        _stationName = string.IsNullOrWhiteSpace(stationName) ? "Station" : stationName;
        _credits = credits;
        _playerShip = playerShip;
        _ownedMode = false;
        _selectedIndex = 0;
        _statusMessage = string.Empty;
        _statusRemaining = 0f;
        _statusSuccess = false;
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

    /// <summary>
    /// Consumes all input while open. The opening E press is gated until E is
    /// released, and every navigation/action is edge-triggered.
    /// </summary>
    public bool HandleInput(KeyboardState keyboardState, KeyboardState previousKeyboardState)
    {
        if (!IsOpen) return false;

        if (_inputGate)
        {
            if (keyboardState.IsKeyUp(Keys.E)) _inputGate = false;
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.Escape))
        {
            Close();
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.Tab) ||
            Pressed(keyboardState, previousKeyboardState, Keys.Left) ||
            Pressed(keyboardState, previousKeyboardState, Keys.Right))
        {
            _ownedMode = !_ownedMode;
            _selectedIndex = 0;
            ClearStatus();
            return true;
        }

        IReadOnlyList<EquipmentDefinition> items = GetVisibleItems();
        if (Pressed(keyboardState, previousKeyboardState, Keys.Up) ||
            Pressed(keyboardState, previousKeyboardState, Keys.W))
        {
            MoveSelection(-1, items.Count);
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.Down) ||
            Pressed(keyboardState, previousKeyboardState, Keys.S))
        {
            // S is sell in owned mode, but remains navigation in the sale list.
            if (_ownedMode && Pressed(keyboardState, previousKeyboardState, Keys.S))
            {
                SellSelected(items);
            }
            else
            {
                MoveSelection(1, items.Count);
            }

            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.B))
        {
            if (_ownedMode) SwitchToSaleList();
            else BuySelected(items);
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.U))
        {
            UnequipSelected(items);
            return true;
        }

        if (Pressed(keyboardState, previousKeyboardState, Keys.Enter) ||
            Pressed(keyboardState, previousKeyboardState, Keys.E))
        {
            if (_ownedMode) EquipSelected(items);
            else BuySelected(items);
            return true;
        }

        return true;
    }

    public void Draw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice)
    {
        if (!IsOpen || spriteBatch == null || graphicsDevice == null) return;

        int width = graphicsDevice.Viewport.Width;
        int height = graphicsDevice.Viewport.Height;
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), Color.Black * 0.72f);

        int panelWidth = Math.Min(1220, width - 64);
        int panelHeight = Math.Min(760, height - 56);
        int panelX = (width - panelWidth) / 2;
        int panelY = (height - panelHeight) / 2;
        Rectangle panel = new(panelX, panelY, panelWidth, panelHeight);
        spriteBatch.Draw(_pixel, panel, new Color(9, 15, 25) * 0.98f);
        DrawBorder(spriteBatch, panel, Color.Gold, 3);

        string title = $"EQUIPMENT - {_stationName}";
        spriteBatch.DrawString(_font, title, new Vector2(panel.X + 24, panel.Y + 16), Color.Gold);
        string shipText = $"Ship: {_playerShip?.DisplayName ?? "Unavailable"}";
        spriteBatch.DrawString(_font, shipText, new Vector2(panel.X + 24, panel.Y + 42), Color.LightSkyBlue);
        string creditsText = $"Credits: {_credits?.GetFormattedCredits() ?? "0"} CR";
        Vector2 creditsSize = _font.MeasureString(creditsText);
        spriteBatch.DrawString(_font, creditsText, new Vector2(panel.Right - creditsSize.X - 24, panel.Y + 18), Color.Yellow);

        int contentTop = panel.Y + 76;
        int contentBottom = panel.Bottom - 64;
        int dividerX = panel.X + (int)(panel.Width * 0.43f);
        Rectangle listPanel = new(panel.X + 18, contentTop, dividerX - panel.X - 28, contentBottom - contentTop);
        Rectangle detailPanel = new(dividerX + 10, contentTop, panel.Right - dividerX - 28, contentBottom - contentTop);
        DrawBorder(spriteBatch, listPanel, Color.DarkSlateGray, 2);
        DrawBorder(spriteBatch, detailPanel, Color.DarkSlateGray, 2);

        IReadOnlyList<EquipmentDefinition> items = GetVisibleItems();
        _selectedIndex = items.Count == 0 ? 0 : Math.Clamp(_selectedIndex, 0, items.Count - 1);
        string listTitle = _ownedMode ? "OWNED / EQUIPPED" : "FOR SALE";
        string switchHint = _ownedMode ? "TAB: FOR SALE" : "TAB: OWNED";
        spriteBatch.DrawString(_font, listTitle, new Vector2(listPanel.X + 14, listPanel.Y + 12), Color.LightSkyBlue);
        Vector2 switchSize = _font.MeasureString(switchHint);
        spriteBatch.DrawString(_font, switchHint, new Vector2(listPanel.Right - switchSize.X - 14, listPanel.Y + 12), Color.Cyan);

        int listY = listPanel.Y + 46;
        int rowHeight = 48;
        if (items.Count == 0)
        {
            string empty = _ownedMode ? "No owned dealer equipment." : "No valid equipment inventory.";
            spriteBatch.DrawString(_font, empty, new Vector2(listPanel.X + 16, listY + 16), Color.Gray);
        }
        else
        {
            for (int i = 0; i < items.Count; i++)
            {
                int rowY = listY + i * rowHeight;
                if (rowY + rowHeight > listPanel.Bottom - 10) break;

                EquipmentDefinition equipment = items[i];
                bool selected = i == _selectedIndex;
                Rectangle row = new(listPanel.X + 8, rowY, listPanel.Width - 16, rowHeight - 5);
                if (selected) spriteBatch.Draw(_pixel, row, Color.Cyan * 0.24f);
                DrawBorder(spriteBatch, row, selected ? Color.Cyan : Color.DarkSlateGray, selected ? 2 : 1);

                string label = $"{(selected ? "> " : "  ")}{Shorten(equipment.Name, 25)}";
                spriteBatch.DrawString(_font, label, new Vector2(row.X + 10, row.Y + 7), Color.White);
                spriteBatch.DrawString(_font, equipment.EquipmentType.ToString(), new Vector2(row.X + 28, row.Y + 27), TypeColor(equipment.EquipmentType));

                if (_ownedMode)
                {
                    int owned = _playerShip?.Loadout?.GetOwnedCount(equipment.Id) ?? 0;
                    int mounted = _playerShip?.Loadout?.GetMountedCount(equipment.Id) ?? 0;
                    string ownedText = $"{owned} owned / {mounted} equipped";
                    Vector2 ownedSize = _font.MeasureString(ownedText);
                    spriteBatch.DrawString(_font, ownedText, new Vector2(row.Right - ownedSize.X - 10, row.Y + 15), Color.LightGreen);
                }
                else
                {
                    string price = $"{equipment.Price:N0} CR";
                    Vector2 priceSize = _font.MeasureString(price);
                    spriteBatch.DrawString(_font, price, new Vector2(row.Right - priceSize.X - 10, row.Y + 15), Color.Yellow);
                }
            }
        }

        EquipmentDefinition selectedEquipment = items.Count > 0 ? items[_selectedIndex] : null;
        DrawDetails(spriteBatch, detailPanel, selectedEquipment);

        string footer = "UP/DOWN or W/S: Select   TAB/LEFT/RIGHT: Switch   ENTER/E: Action   B: Buy   U: Unequip   S: Sell   ESC: Back";
        spriteBatch.DrawString(_font, footer, new Vector2(panel.X + 24, panel.Bottom - 43), Color.LightGray);
        if (!string.IsNullOrWhiteSpace(_statusMessage))
        {
            Vector2 statusSize = _font.MeasureString(_statusMessage);
            Color color = _statusSuccess ? Color.Lime : Color.OrangeRed;
            string status = Shorten(_statusMessage, 64);
            Vector2 shownStatusSize = _font.MeasureString(status);
            spriteBatch.DrawString(_font, status, new Vector2(panel.Right - shownStatusSize.X - 24, panel.Bottom - 43), color);
        }
    }

    private void DrawDetails(SpriteBatch spriteBatch, Rectangle detailPanel, EquipmentDefinition equipment)
    {
        spriteBatch.DrawString(_font, "SELECTED ITEM", new Vector2(detailPanel.X + 16, detailPanel.Y + 12), Color.LightSkyBlue);
        if (equipment == null)
        {
            spriteBatch.DrawString(_font, "Select an equipment item.", new Vector2(detailPanel.X + 16, detailPanel.Y + 54), Color.Gray);
            return;
        }

        int x = detailPanel.X + 16;
        int y = detailPanel.Y + 48;
        spriteBatch.DrawString(_font, Shorten(equipment.Name, 42), new Vector2(x, y), Color.White);
        y += 28;
        spriteBatch.DrawString(_font, $"Type: {equipment.EquipmentType}", new Vector2(x, y), TypeColor(equipment.EquipmentType));
        y += 25;
        spriteBatch.DrawString(_font, $"Price: {equipment.Price:N0} CR", new Vector2(x, y), Color.Yellow);
        y += 25;
        spriteBatch.DrawString(_font, $"Resale: {_equipmentDealer.GetResaleValue(equipment):N0} CR", new Vector2(x, y), Color.LightGreen);
        y += 30;

        foreach (string stat in GetStats(equipment))
        {
            spriteBatch.DrawString(_font, stat, new Vector2(x, y), Color.White);
            y += 21;
        }

        y += 6;
        string description = Shorten(equipment.Description, 56);
        spriteBatch.DrawString(_font, description, new Vector2(x, y), Color.LightGray);
        y += 30;

        ShipLoadout loadout = _playerShip?.Loadout;
        IReadOnlyList<ShipHardpoint> compatible = loadout == null
            ? Array.Empty<ShipHardpoint>()
            : loadout.GetCompatibleHardpoints(equipment).ToList();
        IReadOnlyList<ShipHardpoint> empty = compatible.Where(hardpoint => hardpoint.IsEmpty).ToList();
        string compatibility = compatible.Count == 0
            ? "Incompatible with current ship"
            : empty.Count == 0
                ? "Compatible: Yes (no empty hardpoint)"
                : $"Compatible: Yes ({empty[0].Id} available)";
        spriteBatch.DrawString(_font, Shorten(compatibility, 58), new Vector2(x, y), compatible.Count == 0 ? Color.OrangeRed : Color.Cyan);
        y += 24;

        int owned = loadout?.GetOwnedCount(equipment.Id) ?? 0;
        int mounted = loadout?.GetMountedCount(equipment.Id) ?? 0;
        spriteBatch.DrawString(_font, $"Owned: {owned}   Equipped: {mounted}", new Vector2(x, y), Color.LightGreen);
        y += 28;

        string action = _ownedMode
            ? mounted > 0 ? "[U] UNEQUIP   [S] SELL SPARE" : "[ENTER/E] EQUIP   [S] SELL"
            : "[ENTER/E or B] BUY";
        spriteBatch.DrawString(_font, action, new Vector2(x, Math.Min(y, detailPanel.Bottom - 30)), Color.Gold);
    }

    private IReadOnlyList<EquipmentDefinition> GetVisibleItems()
    {
        if (!_ownedMode) return _equipmentDealer.AvailableEquipment;

        ShipLoadout loadout = _playerShip?.Loadout;
        if (loadout == null) return Array.Empty<EquipmentDefinition>();

        return _equipmentDealer.AvailableEquipment
            .Where(equipment => loadout.GetOwnedCount(equipment.Id) > 0 || loadout.GetMountedCount(equipment.Id) > 0)
            .ToList();
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
        ClearStatus();
    }

    private void SwitchToSaleList()
    {
        _ownedMode = false;
        _selectedIndex = 0;
        ClearStatus();
    }

    private void BuySelected(IReadOnlyList<EquipmentDefinition> items)
    {
        EquipmentDefinition selected = GetSelected(items);
        if (selected == null)
        {
            SetStatus("No equipment selected.", false);
            return;
        }

        bool success = _equipmentDealer.TryBuyEquipment(selected, _credits, _playerShip, out string message);
        SetStatus(message, success);
        if (!success) _showMessage(message);
    }

    private void EquipSelected(IReadOnlyList<EquipmentDefinition> items)
    {
        EquipmentDefinition selected = GetSelected(items);
        if (selected == null)
        {
            SetStatus("No owned equipment selected.", false);
            return;
        }

        bool success = _equipmentDealer.TryMountEquipment(selected, _playerShip, out string message);
        SetStatus(message, success);
        if (!success) _showMessage(message);
    }

    private void UnequipSelected(IReadOnlyList<EquipmentDefinition> items)
    {
        EquipmentDefinition selected = GetSelected(items);
        if (selected == null)
        {
            SetStatus("No owned equipment selected.", false);
            return;
        }

        bool success = _equipmentDealer.TryUnmountEquipment(selected, _playerShip, out string message);
        SetStatus(message, success);
        if (!success) _showMessage(message);
    }

    private void SellSelected(IReadOnlyList<EquipmentDefinition> items)
    {
        EquipmentDefinition selected = GetSelected(items);
        if (selected == null)
        {
            SetStatus("No owned equipment selected.", false);
            return;
        }

        bool success = _equipmentDealer.TrySellUnequippedEquipment(selected, _credits, _playerShip, out string message);
        SetStatus(message, success);
        if (!success) _showMessage(message);
    }

    private EquipmentDefinition GetSelected(IReadOnlyList<EquipmentDefinition> items)
    {
        return items != null && _selectedIndex >= 0 && _selectedIndex < items.Count ? items[_selectedIndex] : null;
    }

    private void SetStatus(string message, bool success)
    {
        _statusMessage = string.IsNullOrWhiteSpace(message) ? "Transaction rejected." : message;
        _statusSuccess = success;
        _statusRemaining = 5f;
    }

    private void ClearStatus()
    {
        _statusMessage = string.Empty;
        _statusRemaining = 0f;
    }

    private static bool Pressed(KeyboardState current, KeyboardState previous, Keys key)
    {
        return current.IsKeyDown(key) && previous.IsKeyUp(key);
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
        return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
    }

    private static Color TypeColor(EquipmentType equipmentType)
    {
        return equipmentType switch
        {
            EquipmentType.Gun => Color.Orange,
            EquipmentType.MissileLauncher => Color.IndianRed,
            EquipmentType.MineDropper => Color.SandyBrown,
            EquipmentType.CountermeasureDropper => Color.MediumPurple,
            _ => Color.White
        };
    }

    private static IEnumerable<string> GetStats(EquipmentDefinition equipment)
    {
        if (equipment is WeaponEquipmentDefinition weapon)
        {
            yield return $"Damage: {weapon.Damage:0.#}";
            yield return $"Refire: {weapon.RefireRate:0.##} s";
            yield return $"Range: {weapon.Range:0.#}   Speed: {weapon.ProjectileSpeed:0.#}";
            yield return $"Energy usage: {weapon.EnergyCost:0.#}   Mount: Gun";
            yield break;
        }

        if (equipment.EquipmentType == EquipmentType.MissileLauncher)
        {
            yield return $"Damage: {equipment.MissileDamage:0.#}";
            yield return $"Speed: {equipment.MissileSpeed:0.#}   Turn: {equipment.MissileTurnRate:0.##}";
            yield return $"Lifetime: {equipment.MissileLifetime:0.#} s   Mount: MissileLauncher";
            yield break;
        }

        if (equipment.EquipmentType == EquipmentType.MineDropper)
        {
            yield return $"Damage: {equipment.MineDamage:0.#}   Blast: {equipment.MineBlastRadius:0.#}";
            yield return $"Trigger: {equipment.MineTriggerRadius:0.#}   Cooldown: {equipment.MineCooldown:0.#} s";
            yield return "Mount: MineDropper";
            yield break;
        }

        if (equipment.EquipmentType == EquipmentType.CountermeasureDropper)
        {
            yield return $"Life: {equipment.CountermeasureLife:0.#} s";
            yield return $"Attraction radius: {equipment.CountermeasureAttractionRadius:0.#}";
            yield return $"Strength: {equipment.CountermeasureStrength:0.#}   Cooldown: {equipment.CountermeasureCooldown:0.#} s";
            yield return "Mount: CountermeasureDropper";
            yield break;
        }

        yield return $"Mount: {equipment.EquipmentType}";
    }

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
