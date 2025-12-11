using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Roguelancer
{
    /// <summary>
    /// Manages the docked space station UI and navigation between areas
    /// </summary>
    public class StationDockUI
    {
        private Station _dockedStation;
        private StationArea _currentArea;
        private bool _isDocked;
        private SpriteFont _font;
        private Texture2D _pixel;
        
        // Ship dealer system
        private ShipDealer _shipDealer;
        private int _selectedShipIndex = 0;
        
        // Commodity dealer system
        private CommodityDealer _commodityDealer;
        private int _selectedCommodityIndex = 0;
        private int _purchaseQuantity = 1;
        private bool _buyingMode = true; // true = buying, false = selling
        
        public bool IsDocked => _isDocked;
        public StationArea CurrentArea => _currentArea;
        public Station DockedStation => _dockedStation;

        public event Action? OnUndock;
        public event Action<ShipDefinition>? OnShipPurchased;

        public StationDockUI(SpriteFont font, Texture2D pixel, ShipDealer shipDealer, CommodityDealer commodityDealer)
        {
            _font = font;
            _pixel = pixel;
            _shipDealer = shipDealer;
            _commodityDealer = commodityDealer;
            _isDocked = false;
            _currentArea = StationArea.Hangar;
        }

        /// <summary>
        /// Dock at a space station
        /// </summary>
        public void DockAtStation(Station station)
        {
            if (station == null) return;
            
            _dockedStation = station;
            _isDocked = true;
            _currentArea = StationArea.Hangar;
            
            Console.WriteLine($"[DOCK] Docked at {station.Name}");
        }

        /// <summary>
        /// Undock from the current station
        /// </summary>
        public void Undock()
        {
            if (!_isDocked) return;
            
            Console.WriteLine($"[DOCK] Undocking from {_dockedStation?.Name}");
            _isDocked = false;
            _dockedStation = null;
            OnUndock?.Invoke();
        }

        /// <summary>
        /// Navigate to a different area of the station
        /// </summary>
        public void NavigateToArea(StationArea area)
        {
            if (!_isDocked) return;
            
            _currentArea = area;
            Console.WriteLine($"[DOCK] Navigated to {area}");
        }

        /// <summary>
        /// Draw the docked station UI
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, PlayerCredits credits, Ship playerShip)
        {
            if (!_isDocked) return;

            int screenWidth = graphicsDevice.Viewport.Width;
            int screenHeight = graphicsDevice.Viewport.Height;

            // Dark overlay
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), Color.Black * 0.9f);

            // Station name header
            DrawHeader(spriteBatch, screenWidth);

            // Credits display
            DrawCreditsDisplay(spriteBatch, screenWidth, credits);

            // Cargo hold display
            DrawCargoDisplay(spriteBatch, screenWidth, playerShip.CargoHold);

            // Area-specific content
            switch (_currentArea)
            {
                case StationArea.Hangar:
                    DrawHangar(spriteBatch, screenWidth, screenHeight);
                    break;
                case StationArea.Bar:
                    DrawBar(spriteBatch, screenWidth, screenHeight);
                    break;
                case StationArea.Dealer:
                    DrawDealer(spriteBatch, screenWidth, screenHeight);
                    break;
                case StationArea.ShipDealer:
                    DrawShipDealer(spriteBatch, screenWidth, screenHeight);
                    break;
            }

            // Navigation menu
            DrawNavigationMenu(spriteBatch, screenWidth, screenHeight);
        }

        /// <summary>
        /// Handle input for ship dealer (arrow keys and enter)
        /// </summary>
        public bool HandleShipDealerInput(Microsoft.Xna.Framework.Input.KeyboardState keyboardState, 
                                          Microsoft.Xna.Framework.Input.KeyboardState prevKeyboardState,
                                          PlayerCredits credits, Ship playerShip)
        {
            if (_currentArea != StationArea.ShipDealer) return false;

            bool purchasedShip = false;

            // Navigate up/down through ships
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up) && 
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Up))
            {
                _selectedShipIndex--;
                if (_selectedShipIndex < 0) _selectedShipIndex = _shipDealer.AvailableShips.Count - 1;
            }
            else if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down) && 
                     prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Down))
            {
                _selectedShipIndex++;
                if (_selectedShipIndex >= _shipDealer.AvailableShips.Count) _selectedShipIndex = 0;
            }

            // Purchase ship with Enter
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter) && 
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Enter))
            {
                var selectedShip = _shipDealer.AvailableShips[_selectedShipIndex];
                if (selectedShip != null)
                {
                    bool success = _shipDealer.PurchaseShip(selectedShip, credits, playerShip, _commodityDealer);
                    if (success)
                    {
                        OnShipPurchased?.Invoke(selectedShip);
                        purchasedShip = true;
                    }
                }
            }

            return purchasedShip;
        }

        /// <summary>
        /// Handle input for commodity dealer (arrow keys, +/-, B/S, Enter)
        /// </summary>
        public bool HandleCommodityDealerInput(Microsoft.Xna.Framework.Input.KeyboardState keyboardState, 
                                                Microsoft.Xna.Framework.Input.KeyboardState prevKeyboardState,
                                                PlayerCredits credits, Ship playerShip)
        {
            if (_currentArea != StationArea.Dealer) return false;

            bool transactionMade = false;

            // Navigate up/down through commodities
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up) && 
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Up))
            {
                _selectedCommodityIndex--;
                if (_selectedCommodityIndex < 0) _selectedCommodityIndex = _commodityDealer.AvailableCommodities.Count - 1;
                _purchaseQuantity = 1; // Reset quantity when changing selection
            }
            else if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down) && 
                     prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Down))
            {
                _selectedCommodityIndex++;
                if (_selectedCommodityIndex >= _commodityDealer.AvailableCommodities.Count) _selectedCommodityIndex = 0;
                _purchaseQuantity = 1; // Reset quantity when changing selection
            }

            // Increase/decrease purchase quantity
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemPlus) && 
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.OemPlus))
            {
                _purchaseQuantity++;
            }
            else if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemMinus) && 
                     prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.OemMinus))
            {
                _purchaseQuantity--;
                if (_purchaseQuantity < 1) _purchaseQuantity = 1;
            }

            // Toggle buy/sell mode
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B) && 
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.B))
            {
                _buyingMode = true;
            }
            else if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.S) && 
                     prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.S))
            {
                _buyingMode = false;
            }

            // Execute transaction with Enter
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter) && 
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Enter))
            {
                var selectedCommodity = _commodityDealer.AvailableCommodities[_selectedCommodityIndex];
                if (selectedCommodity != null)
                {
                    if (_buyingMode)
                    {
                        bool success = _commodityDealer.BuyCommodity(selectedCommodity, _purchaseQuantity, credits, playerShip.CargoHold);
                        transactionMade = success;
                        if (success)
                        {
                            Console.WriteLine($"[COMMODITY] Bought {_purchaseQuantity}x {selectedCommodity.Name}");
                        }
                    }
                    else
                    {
                        bool success = _commodityDealer.SellCommodity(selectedCommodity, _purchaseQuantity, credits, playerShip.CargoHold);
                        transactionMade = success;
                        if (success)
                        {
                            Console.WriteLine($"[COMMODITY] Sold {_purchaseQuantity}x {selectedCommodity.Name}");
                        }
                    }
                }
            }

            return transactionMade;
        }

        private void DrawHeader(SpriteBatch spriteBatch, int screenWidth)
        {
            string stationName = _dockedStation?.Name ?? "Unknown Station";
            Vector2 titleSize = _font.MeasureString(stationName);
            Vector2 titlePos = new Vector2((screenWidth - titleSize.X) / 2, 30);
            
            // Background panel
            Rectangle headerPanel = new Rectangle(
                (int)titlePos.X - 20,
                (int)titlePos.Y - 10,
                (int)titleSize.X + 40,
                (int)titleSize.Y + 20
            );
            spriteBatch.Draw(_pixel, headerPanel, Color.DarkBlue * 0.8f);
            spriteBatch.Draw(_pixel, new Rectangle(headerPanel.X, headerPanel.Y, headerPanel.Width, 3), Color.Cyan);
            spriteBatch.Draw(_pixel, new Rectangle(headerPanel.X, headerPanel.Bottom - 3, headerPanel.Width, 3), Color.Cyan);
            
            spriteBatch.DrawString(_font, stationName, titlePos, Color.Cyan);
        }

        private void DrawCreditsDisplay(SpriteBatch spriteBatch, int screenWidth, PlayerCredits credits)
        {
            string creditsText = $"Credits: {credits.GetFormattedCredits()}";
            Vector2 creditsSize = _font.MeasureString(creditsText);
            Vector2 creditsPos = new Vector2(screenWidth - creditsSize.X - 30, 30);
            
            Rectangle creditsPanel = new Rectangle(
                (int)creditsPos.X - 10,
                (int)creditsPos.Y - 5,
                (int)creditsSize.X + 20,
                (int)creditsSize.Y + 10
            );
            spriteBatch.Draw(_pixel, creditsPanel, Color.Black * 0.7f);
            spriteBatch.Draw(_pixel, new Rectangle(creditsPanel.X, creditsPanel.Y, creditsPanel.Width, 2), Color.Yellow);
            
            spriteBatch.DrawString(_font, creditsText, creditsPos, Color.Yellow);
        }

        private void DrawCargoDisplay(SpriteBatch spriteBatch, int screenWidth, CargoHold cargoHold)
        {
            string cargoText = $"Cargo: {cargoHold.UsedCapacity}/{cargoHold.MaxCapacity}";
            Vector2 cargoSize = _font.MeasureString(cargoText);
            Vector2 cargoPos = new Vector2(screenWidth - cargoSize.X - 30, 70);
            
            Rectangle cargoPanel = new Rectangle(
                (int)cargoPos.X - 10,
                (int)cargoPos.Y - 5,
                (int)cargoSize.X + 20,
                (int)cargoSize.Y + 10
            );
            spriteBatch.Draw(_pixel, cargoPanel, Color.Black * 0.7f);
            spriteBatch.Draw(_pixel, new Rectangle(cargoPanel.X, cargoPanel.Y, cargoPanel.Width, 2), Color.Cyan);
            
            spriteBatch.DrawString(_font, cargoText, cargoPos, Color.Cyan);
        }

        private void DrawHangar(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            int centerX = screenWidth / 2;
            int centerY = screenHeight / 2;

            // Title
            string title = "== HANGAR ==";
            Vector2 titleSize = _font.MeasureString(title);
            spriteBatch.DrawString(_font, title, new Vector2(centerX - titleSize.X / 2, centerY - 200), Color.Cyan);

            // Ship display area (placeholder)
            Rectangle shipArea = new Rectangle(centerX - 300, centerY - 100, 600, 200);
            spriteBatch.Draw(_pixel, shipArea, Color.DarkGray * 0.3f);
            spriteBatch.Draw(_pixel, new Rectangle(shipArea.X, shipArea.Y, shipArea.Width, 2), Color.Gray);
            spriteBatch.Draw(_pixel, new Rectangle(shipArea.X, shipArea.Bottom - 2, shipArea.Width, 2), Color.Gray);
            
            // Current ship info
            var currentShip = _shipDealer.CurrentPlayerShip;
            string shipText = $"Your Ship: {currentShip.Name}";
            Vector2 shipTextSize = _font.MeasureString(shipText);
            spriteBatch.DrawString(_font, shipText, 
                new Vector2(centerX - shipTextSize.X / 2, centerY - 60), Color.White);
            
            // Ship description
            Vector2 descSize = _font.MeasureString(currentShip.Description);
            spriteBatch.DrawString(_font, currentShip.Description, 
                new Vector2(centerX - descSize.X / 2, centerY - 30), Color.LightGray);
            
            // Ship stats
            string statsText = currentShip.GetStatsString();
            Vector2 statsSize = _font.MeasureString(statsText);
            spriteBatch.DrawString(_font, statsText, 
                new Vector2(centerX - statsSize.X / 2, centerY), Color.Yellow);
            
            // Trade-in value
            string valueText = $"Trade-in Value: {currentShip.TradeInValue:N0} CR";
            Vector2 valueSize = _font.MeasureString(valueText);
            spriteBatch.DrawString(_font, valueText, 
                new Vector2(centerX - valueSize.X / 2, centerY + 30), Color.Cyan);

            // Undock button
            string undockText = "[Press U] UNDOCK";
            Vector2 undockSize = _font.MeasureString(undockText);
            Vector2 undockPos = new Vector2(centerX - undockSize.X / 2, centerY + 140);
            
            Rectangle undockButton = new Rectangle(
                (int)undockPos.X - 15,
                (int)undockPos.Y - 10,
                (int)undockSize.X + 30,
                (int)undockSize.Y + 20
            );
            spriteBatch.Draw(_pixel, undockButton, Color.Green * 0.4f);
            spriteBatch.Draw(_pixel, new Rectangle(undockButton.X, undockButton.Y, undockButton.Width, 3), Color.Green);
            spriteBatch.Draw(_pixel, new Rectangle(undockButton.X, undockButton.Bottom - 3, undockButton.Width, 3), Color.Green);
            
            spriteBatch.DrawString(_font, undockText, undockPos, Color.Green);
        }

        private void DrawBar(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            int centerX = screenWidth / 2;
            int centerY = screenHeight / 2;

            // Title
            string title = "== BAR ==";
            Vector2 titleSize = _font.MeasureString(title);
            spriteBatch.DrawString(_font, title, new Vector2(centerX - titleSize.X / 2, centerY - 200), Color.Orange);

            // Placeholder content
            string[] barText = {
                "The bartender nods at you.",
                "",
                "A few pilots are chatting in the corner.",
                "",
                "[PLACEHOLDER - Future conversation system]"
            };

            int yOffset = centerY - 80;
            foreach (string line in barText)
            {
                Vector2 lineSize = _font.MeasureString(line);
                spriteBatch.DrawString(_font, line, 
                    new Vector2(centerX - lineSize.X / 2, yOffset), Color.LightGray);
                yOffset += 30;
            }
        }

        private void DrawDealer(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            int centerX = screenWidth / 2;
            int centerY = screenHeight / 2;

            // Title
            string title = "== COMMODITIES DEALER ==";
            Vector2 titleSize = _font.MeasureString(title);
            spriteBatch.DrawString(_font, title, new Vector2(centerX - titleSize.X / 2, centerY - 300), Color.Yellow);

            // Mode selector
            string modeText = _buyingMode ? "[B] BUY MODE" : "[S] SELL MODE";
            Color modeColor = _buyingMode ? Color.Lime : Color.Orange;
            Vector2 modeSize = _font.MeasureString(modeText);
            spriteBatch.DrawString(_font, modeText, new Vector2(centerX - modeSize.X / 2, centerY - 260), modeColor);

            // Cargo status (will be provided by playerShip later)
            // For now, show placeholder

            // Draw commodity list
            int yOffset = centerY - 200;
            var commodities = _commodityDealer.AvailableCommodities;

            for (int i = 0; i < commodities.Count; i++)
            {
                var commodity = commodities[i];
                bool isSelected = (i == _selectedCommodityIndex);

                // Commodity panel background
                Rectangle commodityPanel = new Rectangle(centerX - 400, yOffset - 10, 800, 140);
                Color panelColor = isSelected ? commodity.DisplayColor * 0.3f : Color.DarkGray * 0.3f;
                spriteBatch.Draw(_pixel, commodityPanel, panelColor);

                // Border
                Color borderColor = isSelected ? commodity.DisplayColor : Color.Gray;
                spriteBatch.Draw(_pixel, new Rectangle(commodityPanel.X, commodityPanel.Y, commodityPanel.Width, 3), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(commodityPanel.X, commodityPanel.Bottom - 3, commodityPanel.Width, 3), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(commodityPanel.X, commodityPanel.Y, 3, commodityPanel.Height), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(commodityPanel.Right - 3, commodityPanel.Y, 3, commodityPanel.Height), borderColor);

                // Commodity name
                string nameText = $"{commodity.Name}";
                spriteBatch.DrawString(_font, nameText, new Vector2(commodityPanel.X + 15, yOffset), commodity.DisplayColor);

                // Description (short version)
                string descText = commodity.Description.Length > 80 ? commodity.Description.Substring(0, 77) + "..." : commodity.Description;
                spriteBatch.DrawString(_font, descText, 
                    new Vector2(commodityPanel.X + 15, yOffset + 25), Color.LightGray);

                // Price and cargo info
                string priceText = $"Price: {commodity.BasePrice:N0} CR/unit  |  Cargo Space: {commodity.VolumePerUnit}/unit";
                spriteBatch.DrawString(_font, priceText, 
                    new Vector2(commodityPanel.X + 15, yOffset + 70), Color.Yellow);

                // Transaction controls (only show for selected)
                if (isSelected)
                {
                    string quantityText = $"Quantity: {_purchaseQuantity} units  [+/-] to adjust";
                    spriteBatch.DrawString(_font, quantityText, 
                        new Vector2(commodityPanel.X + 15, yOffset + 95), Color.White);

                    int totalCost = commodity.BasePrice * _purchaseQuantity;
                    int totalSpace = commodity.VolumePerUnit * _purchaseQuantity;
                    string totalText = _buyingMode 
                        ? $"Total Cost: {totalCost:N0} CR  |  Space Required: {totalSpace}"
                        : $"Total Value: {totalCost:N0} CR  |  Space Freed: {totalSpace}";
                    spriteBatch.DrawString(_font, totalText, 
                        new Vector2(commodityPanel.X + 15, yOffset + 115), Color.Cyan);
                }

                yOffset += 150;
            }

            // Instructions at bottom
            string instructions = "UP/DOWN: Select | +/-: Quantity | B/S: Buy/Sell Mode | ENTER: Confirm";
            Vector2 instructSize = _font.MeasureString(instructions);
            spriteBatch.DrawString(_font, instructions, 
                new Vector2(centerX - instructSize.X / 2, screenHeight - 150), Color.White);
        }

        private void DrawShipDealer(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            int centerX = screenWidth / 2;
            int centerY = screenHeight / 2;

            // Title
            string title = "== SHIP DEALER ==";
            Vector2 titleSize = _font.MeasureString(title);
            spriteBatch.DrawString(_font, title, new Vector2(centerX - titleSize.X / 2, centerY - 250), Color.Lime);

            // Current ship display
            string currentShipText = $"Current Ship: {_shipDealer.CurrentPlayerShip.Name}";
            Vector2 currentShipSize = _font.MeasureString(currentShipText);
            spriteBatch.DrawString(_font, currentShipText, 
                new Vector2(centerX - currentShipSize.X / 2, centerY - 210), Color.Cyan);

            // Available ships list
            int yOffset = centerY - 150;
            var ships = _shipDealer.AvailableShips;

            for (int i = 0; i < ships.Count; i++)
            {
                var ship = ships[i];
                bool isSelected = (i == _selectedShipIndex);
                bool isCurrentShip = (ship.Name == _shipDealer.CurrentPlayerShip.Name);
                
                // Ship panel background
                Rectangle shipPanel = new Rectangle(centerX - 350, yOffset - 10, 700, 120);
                Color panelColor = isSelected ? Color.Orange * 0.3f : Color.DarkGray * 0.3f;
                spriteBatch.Draw(_pixel, shipPanel, panelColor);
                
                // Border
                Color borderColor = isSelected ? Color.Orange : Color.Gray;
                spriteBatch.Draw(_pixel, new Rectangle(shipPanel.X, shipPanel.Y, shipPanel.Width, 3), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(shipPanel.X, shipPanel.Bottom - 3, shipPanel.Width, 3), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(shipPanel.X, shipPanel.Y, 3, shipPanel.Height), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(shipPanel.Right - 3, shipPanel.Y, 3, shipPanel.Height), borderColor);

                // Ship name and description
                string nameText = $"[{i + 1}] {ship.Name}";
                if (isCurrentShip) nameText += " (OWNED)";
                
                Color nameColor = isCurrentShip ? Color.Cyan : Color.White;
                spriteBatch.DrawString(_font, nameText, new Vector2(shipPanel.X + 15, yOffset), nameColor);
                
                // Description
                spriteBatch.DrawString(_font, ship.Description, 
                    new Vector2(shipPanel.X + 15, yOffset + 25), Color.LightGray);

                // Stats
                string statsText = ship.GetStatsString();
                spriteBatch.DrawString(_font, statsText, 
                    new Vector2(shipPanel.X + 15, yOffset + 50), Color.Yellow);

                // Price info
                int totalCost = _shipDealer.GetTotalCost(ship);
                string priceText;
                Color priceColor;
                
                if (isCurrentShip)
                {
                    priceText = "Already Owned";
                    priceColor = Color.Cyan;
                }
                else
                {
                    if (_shipDealer.CurrentPlayerShip.TradeInValue > 0)
                    {
                        priceText = $"Price: {ship.Price:N0} CR - Trade-in: {_shipDealer.CurrentPlayerShip.TradeInValue:N0} CR = {totalCost:N0} CR";
                    }
                    else
                    {
                        priceText = $"Price: {totalCost:N0} CR";
                    }
                    priceColor = Color.Yellow;
                }
                
                spriteBatch.DrawString(_font, priceText, 
                    new Vector2(shipPanel.X + 15, yOffset + 75), priceColor);

                // Selection indicator
                if (isSelected && !isCurrentShip)
                {
                    string buyText = "[ENTER] Purchase";
                    Vector2 buySize = _font.MeasureString(buyText);
                    spriteBatch.DrawString(_font, buyText, 
                        new Vector2(shipPanel.Right - buySize.X - 15, yOffset + 75), Color.Lime);
                }

                yOffset += 130;
            }

            // Instructions at bottom
            string instructions = "UP/DOWN: Select Ship | ENTER: Purchase | ESC: Cancel";
            Vector2 instructSize = _font.MeasureString(instructions);
            spriteBatch.DrawString(_font, instructions, 
                new Vector2(centerX - instructSize.X / 2, screenHeight - 150), Color.White);
        }

        private void DrawNavigationMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            // Bottom navigation menu
            int menuY = screenHeight - 100;
            int menuWidth = 900;
            int menuX = (screenWidth - menuWidth) / 2;

            Rectangle menuPanel = new Rectangle(menuX, menuY, menuWidth, 80);
            spriteBatch.Draw(_pixel, menuPanel, Color.Black * 0.8f);
            spriteBatch.Draw(_pixel, new Rectangle(menuPanel.X, menuPanel.Y, menuPanel.Width, 2), Color.Cyan);

            // Menu buttons
            string[] menuItems = { "[1] Hangar", "[2] Bar", "[3] Equipment", "[4] Ships" };
            StationArea[] areas = { StationArea.Hangar, StationArea.Bar, StationArea.Dealer, StationArea.ShipDealer };

            int buttonWidth = menuWidth / menuItems.Length;
            for (int i = 0; i < menuItems.Length; i++)
            {
                bool isActive = areas[i] == _currentArea;
                Color buttonColor = isActive ? Color.Cyan : Color.Gray;
                
                Vector2 textSize = _font.MeasureString(menuItems[i]);
                Vector2 textPos = new Vector2(
                    menuX + i * buttonWidth + (buttonWidth - textSize.X) / 2,
                    menuY + (80 - textSize.Y) / 2
                );

                if (isActive)
                {
                    Rectangle highlight = new Rectangle(
                        menuX + i * buttonWidth + 10,
                        menuY + 10,
                        buttonWidth - 20,
                        60
                    );
                    spriteBatch.Draw(_pixel, highlight, Color.Cyan * 0.2f);
                }

                spriteBatch.DrawString(_font, menuItems[i], textPos, buttonColor);
            }
        }
    }
}
