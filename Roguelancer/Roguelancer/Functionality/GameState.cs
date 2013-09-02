using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    public class GameState : IGame {
        public GameStates currentGameState { get; set; }
        public GameStates lastGameState { get; set; }
        public bool gameComponentsLoaded { get; set; }
        public enum GameStates {
            uninitialized = 0,
            loading = 1,
            menu = 2,
            playing = 3
        }
        public GameState() {
            currentGameState = new GameStates();
            currentGameState = GameStates.menu;
        }
        public void Initialize(RoguelancerGame _Game) {
        }
        public void LoadContent(RoguelancerGame _Game) {
        }
        public void Update(RoguelancerGame _Game) {
        }
        public void Draw(RoguelancerGame _Game) {
        }
    }
}