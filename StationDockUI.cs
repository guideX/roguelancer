using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

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

        // Bar NPC system
        private List<BarNpc> _barNpcs = new();
        private int _selectedNpcIndex = 0;
        private bool _isTalkingToNpc = false;
        private BarNpc _currentTalkNpc = null;
        private Mission _offeredMission = null;
        private string _dialogueLine = "";
        private int _dialogueState = 0; // 0=greeting, 1=offer, 2=accepted/declined

        // Job board system
        private JobBoard _jobBoard;
        private MissionManager _missionManager;
        private ReputationManager _reputationManager;
        public string LastDockingDeniedReason { get; private set; } = string.Empty;

        public bool IsDocked => _isDocked;
        public StationArea CurrentArea => _currentArea;
        public Station DockedStation => _dockedStation;

        public event Action? OnUndock;
        public event Action<ShipDefinition>? OnShipPurchased;

        public StationDockUI(SpriteFont font, Texture2D pixel, ShipDealer shipDealer, CommodityDealer commodityDealer, MissionManager missionManager, ReputationManager reputationManager = null)
        {
            _font = font;
            _pixel = pixel;
            _shipDealer = shipDealer;
            _commodityDealer = commodityDealer;
            _missionManager = missionManager;
            _reputationManager = reputationManager;
            _jobBoard = new JobBoard(missionManager);
            _isDocked = false;
            _currentArea = StationArea.Hangar;
        }

        /// <summary>
        /// Dock at a space station
        /// </summary>
        public bool DockAtStation(Station station)
        {
            LastDockingDeniedReason = string.Empty;

            if (station == null)
            {
                LastDockingDeniedReason = "Docking denied: no station selected.";
                return false;
            }

            string stationFactionId = FactionManager.NormalizeFactionId(station.FactionId);
            if (_reputationManager != null && _reputationManager.IsHostile(stationFactionId))
            {
                LastDockingDeniedReason = $"Docking denied: {station.Name} is hostile ({stationFactionId}).";
                Console.WriteLine($"[DOCK] Docking denied at {station.Name} because faction '{stationFactionId}' is hostile.");
                return false;
            }
            
            _dockedStation = station;
            _isDocked = true;
            _currentArea = StationArea.Hangar;
            _selectedCommodityIndex = 0;
            _purchaseQuantity = 1;
            _buyingMode = true;
            _commodityDealer?.SetDockedStation(station);

            // Spawn bar NPCs and refresh job board
            _barNpcs = BarNpc.GenerateBarNpcs();
            _selectedNpcIndex = 0;
            _isTalkingToNpc = false;
            _currentTalkNpc = null;
            _offeredMission = null;
            _dialogueState = 0;

            // Generate a random mission for each NPC
            if (_missionManager != null)
            {
                foreach (var npc in _barNpcs)
                {
                    npc.CurrentMission = _missionManager.GenerateRandomMission(npc.FactionId);
                    npc.CurrentMission.OfferedBy = npc.Name;
                }
            }
            else
            {
                foreach (var npc in _barNpcs)
                {
                    npc.CurrentMission = null;
                }
            }

            _jobBoard?.RefreshMissions(6, _dockedStation?.FactionId);

            // Notify mission manager we docked (for delivery missions)
            _missionManager?.NotifyArrivedAtStation(station.Name);

            Console.WriteLine($"[DOCK] Docked at {station.Name}");
            return true;
        }

        /// <summary>
        /// Undock from the current station
        /// </summary>
        public void Undock()
        {
            if (!_isDocked) return;
            
            Console.WriteLine($"[DOCK] Undocking from {_dockedStation?.Name}");
            _isDocked = false;
            _commodityDealer?.ClearDockedStation();
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
                    DrawDealer(spriteBatch, screenWidth, screenHeight, credits, playerShip);
                    break;
                case StationArea.ShipDealer:
                    DrawShipDealer(spriteBatch, screenWidth, screenHeight);
                    break;
                case StationArea.JobBoard:
                    DrawJobBoard(spriteBatch, screenWidth, screenHeight);
                    break;
            }

            // Active missions panel (always visible at top-right when docked)
            DrawActiveMissions(spriteBatch, screenWidth);

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
            var listings = _commodityDealer.CurrentMarketListings;
            if (listings.Count == 0)
            {
                return false;
            }

            // Navigate up/down through commodities
            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up) && 
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Up))
            {
                _selectedCommodityIndex--;
                if (_selectedCommodityIndex < 0) _selectedCommodityIndex = listings.Count - 1;
                _purchaseQuantity = 1; // Reset quantity when changing selection
            }
            else if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down) && 
                     prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Down))
            {
                _selectedCommodityIndex++;
                if (_selectedCommodityIndex >= listings.Count) _selectedCommodityIndex = 0;
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
                var selectedListing = listings[_selectedCommodityIndex];
                var selectedCommodity = selectedListing.Commodity;
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
            spriteBatch.DrawString(_font, title, new Vector2(centerX - titleSize.X / 2, 100), Color.Orange);

            if (_isTalkingToNpc && _currentTalkNpc != null)
            {
                // NPC Dialogue mode
                DrawNpcDialogue(spriteBatch, centerX, centerY);
            }
            else
            {
                // NPC List mode
                spriteBatch.DrawString(_font, "The bar is busy tonight. Several people look approachable.",
                    new Vector2(centerX - 280, 140), Color.LightGray);

                int yOffset = 190;
                for (int i = 0; i < _barNpcs.Count; i++)
                {
                    var npc = _barNpcs[i];
                    bool isSelected = (i == _selectedNpcIndex);

                    Rectangle npcPanel = new Rectangle(centerX - 350, yOffset - 5, 700, 55);
                    Color panelColor = isSelected ? Color.Orange * 0.25f : Color.DarkGray * 0.2f;
                    spriteBatch.Draw(_pixel, npcPanel, panelColor);

                    Color borderColor = isSelected ? Color.Orange : Color.Gray * 0.5f;
                    spriteBatch.Draw(_pixel, new Rectangle(npcPanel.X, npcPanel.Y, npcPanel.Width, 2), borderColor);
                    spriteBatch.Draw(_pixel, new Rectangle(npcPanel.X, npcPanel.Bottom - 2, npcPanel.Width, 2), borderColor);

                    string npcLabel = $"{npc.Name} - {npc.Title}";
                    spriteBatch.DrawString(_font, npcLabel, new Vector2(npcPanel.X + 15, yOffset), isSelected ? Color.Orange : Color.White);

                    if (npc.CurrentMission != null)
                    {
                        string missionHint = $"  Has a job: {npc.CurrentMission.Type} ({npc.CurrentMission.Difficulty})";
                        spriteBatch.DrawString(_font, missionHint, new Vector2(npcPanel.X + 15, yOffset + 25), Color.Yellow * 0.7f);
                    }

                    yOffset += 65;
                }

                string instructions = "UP/DOWN: Select NPC | ENTER: Talk | TAB: Job Board";
                Vector2 instrSize = _font.MeasureString(instructions);
                spriteBatch.DrawString(_font, instructions, new Vector2(centerX - instrSize.X / 2, screenHeight - 160), Color.White);
            }
        }

        private void DrawNpcDialogue(SpriteBatch spriteBatch, int centerX, int centerY)
        {
            // NPC name and title
            string npcHeader = $"{_currentTalkNpc.Name} - {_currentTalkNpc.Title}";
            Vector2 headerSize = _font.MeasureString(npcHeader);
            spriteBatch.DrawString(_font, npcHeader, new Vector2(centerX - headerSize.X / 2, 160), Color.Orange);

            // Dialogue panel
            Rectangle dialogPanel = new Rectangle(centerX - 400, 200, 800, 300);
            spriteBatch.Draw(_pixel, dialogPanel, Color.Black * 0.6f);
            spriteBatch.Draw(_pixel, new Rectangle(dialogPanel.X, dialogPanel.Y, dialogPanel.Width, 2), Color.Orange);
            spriteBatch.Draw(_pixel, new Rectangle(dialogPanel.X, dialogPanel.Bottom - 2, dialogPanel.Width, 2), Color.Orange);
            spriteBatch.Draw(_pixel, new Rectangle(dialogPanel.X, dialogPanel.Y, 2, dialogPanel.Height), Color.Orange);
            spriteBatch.Draw(_pixel, new Rectangle(dialogPanel.Right - 2, dialogPanel.Y, 2, dialogPanel.Height), Color.Orange);

            // Dialogue line
            spriteBatch.DrawString(_font, $"\"{_dialogueLine}\"", new Vector2(dialogPanel.X + 20, 220), Color.White);

            if (_dialogueState == 1 && _offeredMission != null)
            {
                // Show mission details
                int yOff = 270;
                spriteBatch.DrawString(_font, "--- MISSION OFFER ---", new Vector2(dialogPanel.X + 20, yOff), Color.Yellow);
                yOff += 30;
                spriteBatch.DrawString(_font, $"Type: {_offeredMission.Type}", new Vector2(dialogPanel.X + 20, yOff), Color.Cyan);
                yOff += 25;
                spriteBatch.DrawString(_font, $"Description: {_offeredMission.Description}", new Vector2(dialogPanel.X + 20, yOff), Color.White);
                yOff += 25;
                spriteBatch.DrawString(_font, $"Difficulty: {_offeredMission.Difficulty}", new Vector2(dialogPanel.X + 20, yOff), Color.White);
                yOff += 25;
                spriteBatch.DrawString(_font, $"Reward: {_offeredMission.Reward:N0} CR", new Vector2(dialogPanel.X + 20, yOff), Color.Yellow);
                yOff += 25;
                if (_offeredMission.TimeLimit > 0)
                    spriteBatch.DrawString(_font, $"Time Limit: {_offeredMission.TimeLimit:F0}s", new Vector2(dialogPanel.X + 20, yOff), Color.Red);

                string acceptInstr = "[ENTER] Accept Mission | [ESC] Decline";
                Vector2 aSize = _font.MeasureString(acceptInstr);
                spriteBatch.DrawString(_font, acceptInstr, new Vector2(centerX - aSize.X / 2, dialogPanel.Bottom + 20), Color.Lime);
            }
            else if (_dialogueState == 2)
            {
                string backInstr = "[ESC] Back to bar";
                Vector2 bSize = _font.MeasureString(backInstr);
                spriteBatch.DrawString(_font, backInstr, new Vector2(centerX - bSize.X / 2, dialogPanel.Bottom + 20), Color.White);
            }
            else
            {
                string contInstr = "[ENTER] Continue | [ESC] Leave";
                Vector2 cSize = _font.MeasureString(contInstr);
                spriteBatch.DrawString(_font, contInstr, new Vector2(centerX - cSize.X / 2, dialogPanel.Bottom + 20), Color.White);
            }
        }

        private void DrawDealer(SpriteBatch spriteBatch, int screenWidth, int screenHeight, PlayerCredits credits, Ship playerShip)
        {
            int centerX = screenWidth / 2;
            int centerY = screenHeight / 2;
            var cargoHold = playerShip?.CargoHold;
            var listings = _commodityDealer.CurrentMarketListings;

            // Title
            string title = _dockedStation != null
                ? $"== {_dockedStation.Name.ToUpperInvariant()} MARKET =="
                : "== COMMODITIES DEALER ==";
            Vector2 titleSize = _font.MeasureString(title);
            spriteBatch.DrawString(_font, title, new Vector2(centerX - titleSize.X / 2, centerY - 300), Color.Yellow);

            // Mode selector
            string modeText = _buyingMode ? "[B] BUY MODE" : "[S] SELL MODE";
            Color modeColor = _buyingMode ? Color.Lime : Color.Orange;
            Vector2 modeSize = _font.MeasureString(modeText);
            spriteBatch.DrawString(_font, modeText, new Vector2(centerX - modeSize.X / 2, centerY - 260), modeColor);

            string marketHint = _commodityDealer.CurrentStation != null
                ? $"Station faction: {_dockedStation?.FactionId ?? "unknown"} | Credits: {credits.GetFormattedCredits()}"
                : "Legacy fallback market";
            spriteBatch.DrawString(_font, marketHint, new Vector2(centerX - _font.MeasureString(marketHint).X / 2, centerY - 230), Color.LightGray);

            // Draw commodity list
            int yOffset = centerY - 200;
            if (listings.Count == 0)
            {
                string emptyText = "No station market data available.";
                Vector2 emptySize = _font.MeasureString(emptyText);
                spriteBatch.DrawString(_font, emptyText, new Vector2(centerX - emptySize.X / 2, centerY - 100), Color.Gray);
                return;
            }

            for (int i = 0; i < listings.Count; i++)
            {
                var listing = listings[i];
                var commodity = listing.Commodity;
                bool isSelected = (i == _selectedCommodityIndex);
                int playerQty = cargoHold?.GetCommodityQuantity(commodity.Name) ?? 0;
                bool canBuy = listing.IsAvailable && listing.BuyPrice > 0 && listing.Stock > 0;
                bool canSell = listing.SellPrice > 0 && playerQty > 0;

                // Commodity panel background
                Rectangle commodityPanel = new Rectangle(centerX - 420, yOffset - 10, 840, 160);
                Color panelColor = isSelected ? commodity.DisplayColor * 0.3f : Color.DarkGray * 0.3f;
                if (!listing.IsAvailable && !canSell)
                {
                    panelColor = Color.Black * 0.35f;
                }
                spriteBatch.Draw(_pixel, commodityPanel, panelColor);

                // Border
                Color borderColor = isSelected ? commodity.DisplayColor : (listing.IsAvailable ? Color.Gray : Color.DimGray);
                spriteBatch.Draw(_pixel, new Rectangle(commodityPanel.X, commodityPanel.Y, commodityPanel.Width, 3), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(commodityPanel.X, commodityPanel.Bottom - 3, commodityPanel.Width, 3), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(commodityPanel.X, commodityPanel.Y, 3, commodityPanel.Height), borderColor);
                spriteBatch.Draw(_pixel, new Rectangle(commodityPanel.Right - 3, commodityPanel.Y, 3, commodityPanel.Height), borderColor);

                // Commodity name
                string nameText = $"{commodity.Name}";
                if (commodity.IsContraband)
                {
                    nameText += " [CONTRABAND]";
                }
                spriteBatch.DrawString(_font, nameText, new Vector2(commodityPanel.X + 15, yOffset), commodity.DisplayColor);

                string categoryText = $"{commodity.Category} | Demand {listing.DemandLevel}";
                spriteBatch.DrawString(_font, categoryText, new Vector2(commodityPanel.X + 15, yOffset + 20), Color.LightGray);

                // Description (short version)
                string descText = commodity.Description.Length > 84 ? commodity.Description.Substring(0, 81) + "..." : commodity.Description;
                spriteBatch.DrawString(_font, descText, 
                    new Vector2(commodityPanel.X + 15, yOffset + 40), Color.LightGray);

                // Price and cargo info
                string availabilityText = listing.IsAvailable ? "Available" : "Unavailable";
                string priceText = $"Buy: {(listing.BuyPrice > 0 ? $"{listing.BuyPrice:N0}" : "N/A")} CR  |  Sell: {(listing.SellPrice > 0 ? $"{listing.SellPrice:N0}" : "N/A")} CR  |  Stock: {listing.Stock:N0}  |  Cargo: {commodity.VolumePerUnit}/unit  |  {availabilityText}";
                spriteBatch.DrawString(_font, priceText, 
                    new Vector2(commodityPanel.X + 15, yOffset + 68), Color.Yellow);

                // Transaction controls (only show for selected)
                if (isSelected)
                {
                    string quantityText = $"Quantity: {_purchaseQuantity} units  [+/-] to adjust  |  You have: {playerQty}";
                    spriteBatch.DrawString(_font, quantityText, 
                        new Vector2(commodityPanel.X + 15, yOffset + 92), Color.White);

                    int totalCost = listing.BuyPrice * _purchaseQuantity;
                    int totalValue = listing.SellPrice * _purchaseQuantity;
                    int totalSpace = commodity.VolumePerUnit * _purchaseQuantity;
                    int spread = listing.SellPrice - listing.BuyPrice;
                    string spreadText = spread >= 0
                        ? $"Local spread: +{spread:N0} CR"
                        : $"Local spread: {spread:N0} CR";

                    string totalText = _buyingMode
                        ? $"Total Cost: {totalCost:N0} CR  |  Space Required: {totalSpace}"
                        : $"Total Value: {totalValue:N0} CR  |  Space Freed: {totalSpace}";
                    spriteBatch.DrawString(_font, totalText, 
                        new Vector2(commodityPanel.X + 15, yOffset + 114), Color.Cyan);
                    spriteBatch.DrawString(_font, spreadText,
                        new Vector2(commodityPanel.X + 15, yOffset + 136), spread >= 0 ? Color.Lime : Color.OrangeRed);

                    string actionText;
                    Color actionColor;
                    if (_buyingMode)
                    {
                        actionText = canBuy ? "[ENTER] Buy" : "[ENTER] Cannot buy here";
                        actionColor = canBuy ? Color.Lime : Color.Gray;
                    }
                    else
                    {
                        actionText = canSell ? "[ENTER] Sell" : "[ENTER] No buyer here";
                        actionColor = canSell ? Color.Orange : Color.Gray;
                    }

                    Vector2 actionSize = _font.MeasureString(actionText);
                    spriteBatch.DrawString(_font, actionText,
                        new Vector2(commodityPanel.Right - actionSize.X - 15, yOffset + 114), actionColor);
                }

                yOffset += 170;
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

        private void DrawJobBoard(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            int centerX = screenWidth / 2;

            // Title
            string title = "== JOB BOARD ==";
            Vector2 titleSize = _font.MeasureString(title);
            spriteBatch.DrawString(_font, title, new Vector2(centerX - titleSize.X / 2, 100), Color.Lime);

            var missions = _jobBoard.AvailableMissions;
            if (missions.Count == 0)
            {
                string emptyMsg = "No jobs available right now. Check back later.";
                Vector2 emptySize = _font.MeasureString(emptyMsg);
                spriteBatch.DrawString(_font, emptyMsg, new Vector2(centerX - emptySize.X / 2, 300), Color.Gray);
            }
            else
            {
                int yOffset = 150;
                for (int i = 0; i < missions.Count; i++)
                {
                    var mission = missions[i];
                    bool isSelected = (i == _jobBoard.SelectedIndex);

                    Rectangle mPanel = new Rectangle(centerX - 420, yOffset - 5, 840, 70);
                    Color pColor = isSelected ? Color.Lime * 0.2f : Color.DarkGray * 0.2f;
                    spriteBatch.Draw(_pixel, mPanel, pColor);

                    Color bColor = isSelected ? Color.Lime : Color.Gray * 0.5f;
                    spriteBatch.Draw(_pixel, new Rectangle(mPanel.X, mPanel.Y, mPanel.Width, 2), bColor);
                    spriteBatch.Draw(_pixel, new Rectangle(mPanel.X, mPanel.Bottom - 2, mPanel.Width, 2), bColor);

                    Color typeColor = mission.Type switch
                    {
                        MissionType.Delivery => Color.Cyan,
                        MissionType.Bounty => Color.Red,
                        MissionType.Escort => Color.Yellow,
                        _ => Color.White
                    };

                    string typeTag = $"[{mission.Type.ToString().ToUpper()}]";
                    spriteBatch.DrawString(_font, typeTag, new Vector2(mPanel.X + 10, yOffset), typeColor);
                    spriteBatch.DrawString(_font, mission.Description, new Vector2(mPanel.X + 120, yOffset), isSelected ? Color.White : Color.LightGray);

                    string diffStr = mission.Difficulty.ToString();
                    string rewardStr = $"{mission.Reward:N0} CR";
                    string timeStr = mission.TimeLimit > 0 ? $"  Time: {mission.TimeLimit:F0}s" : "";
                    spriteBatch.DrawString(_font, $"{diffStr} | {rewardStr}{timeStr}",
                        new Vector2(mPanel.X + 120, yOffset + 25), Color.Yellow * 0.8f);

                    if (isSelected)
                    {
                        string acceptText = "[ENTER] Accept";
                        Vector2 accSize = _font.MeasureString(acceptText);
                        spriteBatch.DrawString(_font, acceptText, new Vector2(mPanel.Right - accSize.X - 10, yOffset + 20), Color.Lime);
                    }

                    yOffset += 80;
                }
            }

            string instructions = "UP/DOWN: Select | ENTER: Accept Mission | R: Refresh Board";
            Vector2 instrSize = _font.MeasureString(instructions);
            spriteBatch.DrawString(_font, instructions, new Vector2(centerX - instrSize.X / 2, screenHeight - 160), Color.White);
        }

        private void DrawActiveMissions(SpriteBatch spriteBatch, int screenWidth)
        {
            if (_missionManager == null)
            {
                return;
            }

            var activeMissions = _missionManager.ActiveMissions;
            if (activeMissions.Count == 0) return;

            int panelX = 20;
            int panelY = 110;
            int panelWidth = 350;
            int lineHeight = 22;
            int panelHeight = 30 + activeMissions.Count * (lineHeight * 2 + 10);

            Rectangle panel = new Rectangle(panelX, panelY, panelWidth, panelHeight);
            spriteBatch.Draw(_pixel, panel, Color.Black * 0.7f);
            spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 2), Color.Cyan);

            spriteBatch.DrawString(_font, "ACTIVE MISSIONS", new Vector2(panelX + 10, panelY + 5), Color.Cyan);

            int yOff = panelY + 30;
            foreach (var m in activeMissions)
            {
                Color typeColor = m.Type switch
                {
                    MissionType.Delivery => Color.Cyan,
                    MissionType.Bounty => Color.Red,
                    MissionType.Escort => Color.Yellow,
                    _ => Color.White
                };

                spriteBatch.DrawString(_font, $"[{m.Type}] {m.Description}", new Vector2(panelX + 10, yOff), typeColor);
                yOff += lineHeight;

                string detailStr = $"  Reward: {m.Reward:N0} CR";
                if (m.TimeLimit > 0)
                    detailStr += $" | Time: {m.TimeRemaining:F0}s";
                spriteBatch.DrawString(_font, detailStr, new Vector2(panelX + 10, yOff), Color.LightGray * 0.8f);
                yOff += lineHeight + 10;
            }
        }

        /// <summary>
        /// Handle input for bar NPC interactions
        /// </summary>
        public bool HandleBarInput(Microsoft.Xna.Framework.Input.KeyboardState keyboardState,
                                    Microsoft.Xna.Framework.Input.KeyboardState prevKeyboardState)
        {
            if (_currentArea != StationArea.Bar) return false;

            if (_isTalkingToNpc)
            {
                // In dialogue mode
                if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) &&
                    prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Escape))
                {
                    if (_dialogueState == 1 && _offeredMission != null)
                    {
                        // Decline mission
                        _dialogueLine = _currentTalkNpc.GetDeclineLine();
                        _dialogueState = 2;
                    }
                    else
                    {
                        // Leave conversation
                        _isTalkingToNpc = false;
                        _currentTalkNpc = null;
                        _offeredMission = null;
                        _dialogueState = 0;
                    }
                    return true;
                }

                if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter) &&
                    prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    if (_dialogueState == 0)
                    {
                        // Advance to mission offer
                        if (_currentTalkNpc.CurrentMission != null)
                        {
                            _dialogueLine = _currentTalkNpc.GetMissionOfferLine();
                            _offeredMission = _currentTalkNpc.CurrentMission;
                            _dialogueState = 1;
                        }
                        else
                        {
                            _dialogueLine = "I don't have any work right now. Check back later.";
                            _dialogueState = 2;
                        }
                    }
                    else if (_dialogueState == 1 && _offeredMission != null)
                    {
                        // Accept mission
                        _offeredMission.OfferedBy = _currentTalkNpc.Name;
                        if (_missionManager != null && _missionManager.AcceptMission(_offeredMission))
                        {
                            _currentTalkNpc.CurrentMission = null;
                            _dialogueLine = "Good luck out there, pilot.";
                        }
                        else
                        {
                            _dialogueLine = _missionManager == null
                                ? "The mission board is offline right now."
                                : "Looks like you already have that mission.";
                        }
                        _dialogueState = 2;
                    }
                    return true;
                }
            }
            else
            {
                // NPC list selection mode
                if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up) &&
                    prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Up))
                {
                    _selectedNpcIndex--;
                    if (_selectedNpcIndex < 0) _selectedNpcIndex = _barNpcs.Count - 1;
                    return true;
                }

                if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down) &&
                    prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Down))
                {
                    _selectedNpcIndex++;
                    if (_selectedNpcIndex >= _barNpcs.Count) _selectedNpcIndex = 0;
                    return true;
                }

                if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter) &&
                    prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Enter))
                {
                    if (_barNpcs.Count > 0)
                    {
                        _currentTalkNpc = _barNpcs[_selectedNpcIndex];
                        _isTalkingToNpc = true;
                        _dialogueLine = _currentTalkNpc.GetGreeting();
                        _dialogueState = 0;
                        Console.WriteLine($"[BAR] Talking to {_currentTalkNpc.Name}");
                    }
                    return true;
                }

                // TAB to switch to job board
                if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Tab) &&
                    prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Tab))
                {
                    NavigateToArea(StationArea.JobBoard);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Handle input for job board
        /// </summary>
        public bool HandleJobBoardInput(Microsoft.Xna.Framework.Input.KeyboardState keyboardState,
                                         Microsoft.Xna.Framework.Input.KeyboardState prevKeyboardState)
        {
            if (_currentArea != StationArea.JobBoard) return false;

            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Up) &&
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Up))
            {
                _jobBoard.MoveSelectionUp();
                return true;
            }

            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Down) &&
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Down))
            {
                _jobBoard.MoveSelectionDown();
                return true;
            }

            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Enter) &&
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.Enter))
            {
                _jobBoard.AcceptSelectedMission();
                return true;
            }

            if (keyboardState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.R) &&
                prevKeyboardState.IsKeyUp(Microsoft.Xna.Framework.Input.Keys.R))
            {
                _jobBoard.RefreshMissions(6);
                return true;
            }

            return false;
        }

        private void DrawNavigationMenu(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            // Bottom navigation menu
            int menuY = screenHeight - 100;
            int menuWidth = 1000;
            int menuX = (screenWidth - menuWidth) / 2;

            Rectangle menuPanel = new Rectangle(menuX, menuY, menuWidth, 80);
            spriteBatch.Draw(_pixel, menuPanel, Color.Black * 0.8f);
            spriteBatch.Draw(_pixel, new Rectangle(menuPanel.X, menuPanel.Y, menuPanel.Width, 2), Color.Cyan);

            // Menu buttons
            string[] menuItems = { "[1] Hangar", "[2] Bar", "[3] Equipment", "[4] Ships", "[5] Jobs" };
            StationArea[] areas = { StationArea.Hangar, StationArea.Bar, StationArea.Dealer, StationArea.ShipDealer, StationArea.JobBoard };

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
