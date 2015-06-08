namespace Roguelancer.Functionality {
    public class GameState {
        public GameStates currentGameState { get; set; }
        public GameStates lastGameState { get; set; }
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
    }
}