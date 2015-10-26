// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System.Linq;
using Roguelancer.Interfaces;
using System.Collections.Generic;
using Roguelancer.Settings;
namespace Roguelancer.Objects {
    public abstract class DockableObject {
        /// <summary>
        /// Docked Ships
        /// </summary>
        public List<ISensorObject> DockedShips { get; set; }
        /// <summary>
        /// Dock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void Dock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject) {
            try {
                var _ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                if (_ship == ship) {
                    game.GameState.CurrentGameState = Enum.GameStates.Docked;
                }
                ship.Docked = true;
                DockedShips.Add(ship);
                game.DebugText.SetText(game, "Docked at '" + worldObject.Description + "'.", true);
            } catch {
                throw;
            }
        }
        /// <summary>
        /// Undock
        /// </summary>
        /// <param name="game"></param>
        /// <param name="ship"></param>
        public virtual void UnDock(RoguelancerGame game, Ship ship, ModelWorldObjects worldObject) {
            try {
                var _ship = game.Objects.Ships.Ships.Where(s => s.PlayerShipControl.UseInput).LastOrDefault();
                if (_ship == ship) {
                    game.GameState.CurrentGameState = Enum.GameStates.Playing;
                }
                ship.Docked = false;
                DockedShips.Remove(ship);
                game.DebugText.SetText(game, "Undocked from '" + worldObject.Description + "'.", true);
            } catch {
                throw;
            }
        }
    }
}