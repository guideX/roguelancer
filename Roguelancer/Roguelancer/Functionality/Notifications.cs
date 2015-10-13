// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using Roguelancer.Interfaces;
namespace Roguelancer.Functionality {
    /// <summary>
    /// Notifications
    /// </summary>
    public class Notifications : IGame {
        #region "private variables"
        /// <summary>
        /// Location
        /// </summary>
        private Notification _location;
        /// <summary>
        /// Speed
        /// </summary>
        private Notification _speed;
        #endregion
        #region "public variables"
        /// <summary>
        /// Entry Point
        /// </summary>
        public Notifications() {
            try {
                _location = new Notification();
                _speed = new Notification();
            } catch (Exception ex) {
                throw ex;
            }
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {
            try {
                _location.Initialize(game);
                _speed.Initialize(game);
            } catch (Exception ex) {
                throw ex;
            }
        }
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {
            try {
                _location.LoadContent(game);
                _speed.LoadContent(game);
            } catch (Exception ex) {
                throw ex;
            }
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {
            try {
                _location.Update(game);
                _speed.Update(game);
            } catch (Exception ex) {
                throw ex;
            }
        }
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {
            try {
                _location.Draw(game);
                _speed.Draw(game);
            } catch (Exception ex) {
                throw ex;
            }
        }
        #endregion
    }
    public class Notification : IGame {
        #region "public functions"
        /// <summary>
        /// Entry Point
        /// </summary>
        public Notification() {}
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="game"></param>
        public void Initialize(RoguelancerGame game) {}
        /// <summary>
        /// Load Content
        /// </summary>
        /// <param name="game"></param>
        public void LoadContent(RoguelancerGame game) {}
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="game"></param>
        public void Update(RoguelancerGame game) {}
        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="game"></param>
        public void Draw(RoguelancerGame game) {}
        #endregion
    }
}