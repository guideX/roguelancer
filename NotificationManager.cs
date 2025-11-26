using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Manages and displays on-screen notifications.
    /// </summary>
    public class NotificationManager
    {
        private class NotificationMessage
        {
            public string Text;
            public float Duration;
            public float Timer;
            public float FadeInTime;
            public float FadeOutTime;
            public float Alpha;
        }

        private readonly Queue<NotificationMessage> _messageQueue = new();
        private NotificationMessage _currentMessage;
        private readonly SpriteFont _font;
        private readonly Viewport _viewport;

        public NotificationManager(SpriteFont font, Viewport viewport)
        {
            _font = font;
            _viewport = viewport;
        }

        /// <summary>
        /// Adds a new message to the notification queue.
        /// </summary>
        /// <param name="text">The text to display.</param>
        /// <param name="duration">How long the message should stay on screen in seconds.</param>
        public void ShowMessage(string text, float duration = 3f)
        {
            var message = new NotificationMessage
            {
                Text = text,
                Duration = duration,
                Timer = 0f,
                FadeInTime = 0.5f, // 0.5 second fade in
                FadeOutTime = 0.5f, // 0.5 second fade out
                Alpha = 0f
            };
            _messageQueue.Enqueue(message);
        }

        public Viewport GetViewport()
        {
            return _viewport;
        }

        public void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_currentMessage == null && _messageQueue.Count > 0)
            {
                _currentMessage = _messageQueue.Dequeue();
            }

            if (_currentMessage != null)
            {
                _currentMessage.Timer += deltaTime;

                // Handle state transitions
                if (_currentMessage.Timer < _currentMessage.FadeInTime)
                {
                    // Fading in
                    _currentMessage.Alpha = MathHelper.Lerp(0f, 1f, _currentMessage.Timer / _currentMessage.FadeInTime);
                }
                else if (_currentMessage.Timer < _currentMessage.Duration - _currentMessage.FadeOutTime)
                {
                    // Fully visible
                    _currentMessage.Alpha = 1f;
                }
                else if (_currentMessage.Timer < _currentMessage.Duration)
                {
                    // Fading out
                    float fadeOutProgress = (_currentMessage.Timer - (_currentMessage.Duration - _currentMessage.FadeOutTime)) / _currentMessage.FadeOutTime;
                    _currentMessage.Alpha = MathHelper.Lerp(1f, 0f, fadeOutProgress);
                }
                else
                {
                    // Message expired
                    _currentMessage = null;
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_currentMessage == null || _font == null)
            {
                return;
            }

            // Measure text to center it
            Vector2 textSize = _font.MeasureString(_currentMessage.Text);
            
            // Position text in the center of the screen, but lower down
            Vector2 position = new Vector2(
                _viewport.Width / 2f - textSize.X / 2f,
                _viewport.Height * 0.65f - textSize.Y / 2f // 65% down the screen
            );

            // Draw shadow first
            spriteBatch.DrawString(_font, _currentMessage.Text, position + Vector2.One * 2, Color.Black * _currentMessage.Alpha * 0.8f);
            
            // Draw main text
            spriteBatch.DrawString(_font, _currentMessage.Text, position, Color.White * _currentMessage.Alpha);
        }
    }
}
