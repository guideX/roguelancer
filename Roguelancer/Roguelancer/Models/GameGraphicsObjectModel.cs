using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Roguelancer.Enum;
namespace Roguelancer.Models {
    /// <summary>
    /// Game Graphics Object Model
    /// </summary>
    public class GameGraphicsObjectModel {
        /// <summary>
        /// Graphics Device Manager
        /// </summary>
        public GraphicsDeviceManager GraphicsDeviceManager { get; set; }
        /// <summary>
        /// Sprite Batch
        /// </summary>
        public SpriteBatch SpriteBatch { get; set; }
        /// <summary>
        /// Projection
        /// </summary>
        public Matrix Projection { get; set; }
        /// <summary>
        /// View
        /// </summary>
        public Matrix View { get; set; }
        /// <summary>
        /// Mode
        /// </summary>
        public GameCameraModeEnum Mode { get; set; } = GameCameraModeEnum.StandardMode;
        /// <summary>
        /// Chase Position
        /// </summary>
        public Vector3 ChasePosition { get; set; }
        /// <summary>
        /// Chase Direction
        /// </summary>
        public Vector3 ChaseDirection { get; set; }
        /// <summary>
        /// Look At
        /// </summary>
        public Vector3 LookAt { get; set; }
        /// <summary>
        /// Position
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Up
        /// </summary>
        public Vector3 Up { get; set; } = Vector3.Up;
        /// <summary>
        /// Desired Position
        /// </summary>
        public Vector3 DesiredPosition { get; set; }
        /// <summary>
        /// Velocity
        /// </summary>
        public Vector3 Velocity { get; set; }
        /// <summary>
        /// Shake Offset
        /// </summary>
        public Vector3 ShakeOffset { get; set; }
        /// <summary>
        /// Shake Magnitude
        /// </summary>
        public float ShakeMagnitude { get; set; }
        /// <summary>
        /// Shake Duration
        /// </summary>
        public float ShakeDuration { get; set; }
        /// <summary>
        /// Shake Timer
        /// </summary>
        public float ShakeTimer { get; set; }
        /// <summary>
        /// Shaking
        /// </summary>
        public bool Shaking { get; set; }
        /// <summary>
        /// Shake Use Duration
        /// </summary>
        public bool ShakeUseDuration { get; set; }
        /// <summary>
        /// Shake Random
        /// </summary>
        public Random ShakeRandom { get; set; } = new Random();
    }
}