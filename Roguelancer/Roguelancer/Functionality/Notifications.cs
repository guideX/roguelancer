//TODO - THIS AREA ISN'T COMPLETED YET, COMPLETE IT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Interfaces;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
namespace Roguelancer.Functionality {
    public enum NotificationType {
        Location = 1, 
        Speed = 2
    }
    public class Notifications : IGame {
        private Notification location;
        private Notification speed;
        public Notifications() {
            try {
                location = new Notification();
                speed = new Notification();
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void Initialize(RoguelancerGame game) {
            try {
                location.Initialize(game);
                speed.Initialize(game);
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void LoadContent(RoguelancerGame game) {
            try {
                location.LoadContent(game);
                speed.LoadContent(game);
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void Update(RoguelancerGame game) {
            try {
                location.Update(game);
                speed.Update(game);
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void Draw(RoguelancerGame game) {
            try {
                location.Draw(game);
                speed.Draw(game);
            } catch (Exception ex) {
                throw ex;
            }
        }
    }
    public class Notification : IGame, INotificationSettings {
        private bool _showForever { get; set; }
        private string _name { get; set; }
        private Vector3 _location { get; set; }
        private bool _visible { get; set; }
        private TimeSpan _time { get; set; }
        private DateTime _timeShown { get; set; }
        private SpriteBatch _spriteBatch;
        private String _text;
        private SpriteFont _font;
        private Vector2 _fontPosition;
        public Notification() {
            try {
                
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void Setup(string name, string text, TimeSpan time, Vector3 location, bool showForever) {
            try {
                _name = name;
                _text = text;
                _time = time;
                _location = location;
                _showForever = showForever;
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void Initialize(RoguelancerGame game) {
            try {
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void LoadContent(RoguelancerGame game) {
            try {
                _font = game.Content.Load<SpriteFont>("FONTS\\" + game.Settings.font);
                _fontPosition = new Vector2(game.Graphics.graphicsDeviceManager.GraphicsDevice.Viewport.Width / 2, game.Graphics.graphicsDeviceManager.GraphicsDevice.Viewport.Height / 2);
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void Update(RoguelancerGame game) {
            try {
            } catch (Exception ex) {
                throw ex;
            }
        }
        public void Draw(RoguelancerGame game) {
            try {
            } catch (Exception ex) {
                throw ex;
            }
        }
    }
}