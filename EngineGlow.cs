using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Roguelancer
{
    /// <summary>
    /// Engine state for determining glow appearance
    /// </summary>
    public enum EngineState
    {
        Idle,           // Engines off or minimal power
        Accelerating,   // Normal thrust
        Afterburner,    // Afterburner engaged - intense glow
        Cruise,         // Cruise mode - sustained bright glow
        CruiseCharging  // Charging cruise - pulsing glow
    }

    /// <summary>
    /// Engine glow effect for ship thrusters - Freelancer-style engine trails
    /// </summary>
    public class EngineGlow
    {
        private GraphicsDevice _graphicsDevice;
        private BasicEffect _effect;
        private Random _random = new Random();
        private float _pulseTimer = 0f;
        
        // Glow parameters for different states (in world units, will be scaled by ship scale)
        // Original code used (5f + intensity * 10f) * 0.1f = 0.5 to 1.5 final size
        // So we need base sizes around 5-15 that get multiplied by 0.1 ship scale
        private const float IdleGlowSize = 5.0f;
        private const float AcceleratingGlowSize = 10.0f;
        private const float AfterburnerGlowSize = 18.0f;
        private const float CruiseGlowSize = 15.0f;
        
        // Trail length multipliers (in world units before ship scale)
        private const float IdleTrailLength = 3.0f;
        private const float AcceleratingTrailLength = 8.0f;
        private const float AfterburnerTrailLength = 20.0f;
        private const float CruiseTrailLength = 15.0f;

        // Default engine positions for player ship (in model space)
        private static readonly List<Vector3> DefaultEngineOffsets = new List<Vector3>
        {
            new Vector3(-2.5f, -1.0f, 8.5f),  // Left engine
            new Vector3(2.5f, -1.0f, 8.5f),   // Right engine
            new Vector3(0f, -1.0f, 9.0f)      // Center engine
        };

        // NPC ship engine positions (smaller ships, single engine)
        private static readonly List<Vector3> NpcEngineOffsets = new List<Vector3>
        {
            new Vector3(-1.5f, -0.5f, 6.0f),  // Left engine
            new Vector3(1.5f, -0.5f, 6.0f),   // Right engine
        };

        public EngineGlow(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            
            _effect = new BasicEffect(graphicsDevice)
            {
                TextureEnabled = false,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        /// <summary>
        /// Update internal timers for pulsing effects
        /// </summary>
        public void Update(float deltaTime)
        {
            _pulseTimer += deltaTime;
        }

        /// <summary>
        /// Draw engine glows for player ship with full state information
        /// </summary>
        public void DrawEngineGlows(GraphicsDevice graphicsDevice, Ship ship, Camera camera, float throttle)
        {
            if (ship.Model == null) return;
            
            // Determine engine state from ship properties
            EngineState state;
            
            if (ship.EnginesKilled)
            {
                state = EngineState.Idle;
            }
            else if (ship.IsCruiseCharging)
            {
                state = EngineState.CruiseCharging;
            }
            else if (ship.IsCruiseActive)
            {
                state = EngineState.Cruise;
            }
            else if (ship.IsAfterburnerActive)
            {
                state = EngineState.Afterburner;
            }
            else if (throttle > 0.05f || ship.Speed > 10f)
            {
                // Show accelerating glow when there's any throttle or the ship is moving
                state = EngineState.Accelerating;
            }
            else
            {
                state = EngineState.Idle;
            }
            
            DrawEngineGlowsInternal(ship.Model, ship.Position, ship.Orientation, ship.ModelRotationCorrection, 
                                    camera, throttle, state, DefaultEngineOffsets, 0.1f);
        }

        /// <summary>
        /// Draw engine glows for NPC ships
        /// </summary>
        public void DrawNpcEngineGlows(NpcShip npc, Camera camera)
        {
            if (npc.Model == null || npc.IsDestroyed) return;
            
            // NPCs are always in "accelerating" state when moving
            float speedRatio = npc.Speed / 100f; // Normalize speed
            EngineState state = speedRatio > 0.1f ? EngineState.Accelerating : EngineState.Idle;
            
            DrawEngineGlowsInternal(npc.Model, npc.Position, npc.Orientation, npc.ModelRotationCorrection,
                                    camera, speedRatio, state, NpcEngineOffsets, 0.08f);
        }

        /// <summary>
        /// Internal method to draw engine glows for any ship
        /// </summary>
        private void DrawEngineGlowsInternal(Model model, Vector3 position, Matrix orientation, 
            Matrix modelRotationCorrection, Camera camera, float throttle, EngineState state, 
            List<Vector3> engineOffsets, float shipScale)
        {
            if (model == null) return;
            
            // Save current render states
            var oldBlendState = _graphicsDevice.BlendState;
            var oldDepthStencilState = _graphicsDevice.DepthStencilState;
            var oldRasterizerState = _graphicsDevice.RasterizerState;
            
            // Apply the same model correction as in Ship.Draw
            Matrix modelCorrection = Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi);
            
            // Transform engine positions to world space
            Matrix shipTransform = modelCorrection * modelRotationCorrection * orientation * Matrix.CreateTranslation(position);
            
            // Set render states for glowing particles
            _graphicsDevice.BlendState = BlendState.Additive;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            
            _effect.View = camera.View;
            _effect.Projection = camera.Projection;
            
            // Get glow parameters based on state
            var (baseColor, coreColor, glowSize, trailLength, intensity) = GetGlowParameters(state, throttle);
            
            foreach (var offset in engineOffsets)
            {
                Vector3 engineWorldPos = Vector3.Transform(offset, shipTransform);
                
                // Get the backward direction of the ship (where the glow trail should extend)
                Vector3 shipBackward = Vector3.TransformNormal(Vector3.Backward, shipTransform);
                shipBackward.Normalize();
                
                // Draw the engine glow layers
                DrawEngineGlowLayers(engineWorldPos, shipBackward, camera.Position, 
                                     baseColor, coreColor, glowSize * shipScale, 
                                     trailLength * shipScale, intensity, state);
            }
            
            // Restore render states
            _graphicsDevice.BlendState = oldBlendState;
            _graphicsDevice.DepthStencilState = oldDepthStencilState;
            _graphicsDevice.RasterizerState = oldRasterizerState;
        }

        /// <summary>
        /// Get glow visual parameters based on engine state
        /// </summary>
        private (Color baseColor, Color coreColor, float glowSize, float trailLength, float intensity) 
            GetGlowParameters(EngineState state, float throttle)
        {
            Color baseColor, coreColor;
            float glowSize, trailLength, intensity;
            
            switch (state)
            {
                case EngineState.Idle:
                    // Dim blue/white idle glow
                    baseColor = new Color(100, 150, 255, 80);
                    coreColor = new Color(200, 220, 255, 120);
                    glowSize = IdleGlowSize;
                    trailLength = IdleTrailLength;
                    intensity = 0.3f;
                    break;
                    
                case EngineState.Accelerating:
                    // Blue-white glow that intensifies with throttle
                    float t = Math.Max(0.3f, throttle);
                    baseColor = new Color((int)(80 + t * 100), (int)(140 + t * 80), 255, (int)(150 + t * 100));
                    coreColor = new Color(255, (int)(240 + t * 15), 255, (int)(200 + t * 55));
                    glowSize = MathHelper.Lerp(IdleGlowSize, AcceleratingGlowSize, t);
                    trailLength = MathHelper.Lerp(IdleTrailLength, AcceleratingTrailLength, t);
                    intensity = MathHelper.Lerp(0.4f, 0.9f, t);
                    break;
                    
                case EngineState.Afterburner:
                    // Intense orange-yellow-white afterburner effect
                    float pulse = (float)Math.Sin(_pulseTimer * 15f) * 0.15f + 0.85f;
                    baseColor = new Color(255, (int)(180 * pulse), 50, (int)(220 * pulse));
                    coreColor = new Color(255, 255, (int)(200 * pulse), 255);
                    glowSize = AfterburnerGlowSize * pulse;
                    trailLength = AfterburnerTrailLength;
                    intensity = 1.0f * pulse;
                    break;
                    
                case EngineState.Cruise:
                    // Sustained bright cyan-blue cruise glow
                    float cruisePulse = (float)Math.Sin(_pulseTimer * 4f) * 0.1f + 0.9f;
                    baseColor = new Color(100, 200, 255, (int)(200 * cruisePulse));
                    coreColor = new Color(220, 255, 255, 255);
                    glowSize = CruiseGlowSize;
                    trailLength = CruiseTrailLength * cruisePulse;
                    intensity = 0.95f;
                    break;
                    
                case EngineState.CruiseCharging:
                    // Pulsing charging effect - builds up intensity
                    float chargePulse = (float)Math.Sin(_pulseTimer * 8f) * 0.3f + 0.7f;
                    float chargeFlicker = (float)(_random.NextDouble() * 0.2f);
                    baseColor = new Color((int)(150 + chargePulse * 100), (int)(180 + chargePulse * 75), 255, (int)(180 * chargePulse));
                    coreColor = new Color(255, 255, 255, (int)(220 * chargePulse));
                    glowSize = MathHelper.Lerp(AcceleratingGlowSize, CruiseGlowSize, chargePulse) + chargeFlicker;
                    trailLength = MathHelper.Lerp(AcceleratingTrailLength, CruiseTrailLength, chargePulse);
                    intensity = 0.7f + chargePulse * 0.3f;
                    break;
                    
                default:
                    baseColor = new Color(100, 150, 255, 100);
                    coreColor = Color.White;
                    glowSize = IdleGlowSize;
                    trailLength = IdleTrailLength;
                    intensity = 0.3f;
                    break;
            }
            
            return (baseColor, coreColor, glowSize, trailLength, intensity);
        }

        /// <summary>
        /// Draw multiple layers of glow for a single engine to create depth effect
        /// </summary>
        private void DrawEngineGlowLayers(Vector3 enginePos, Vector3 backward, Vector3 cameraPos,
            Color baseColor, Color coreColor, float glowSize, float trailLength, float intensity, EngineState state)
        {
            // Layer 1: Outer glow halo (largest, most transparent)
            Color outerColor = baseColor * 0.4f;
            outerColor.A = (byte)(baseColor.A * 0.3f);
            DrawBillboard(enginePos, glowSize * 2.5f, outerColor, cameraPos);
            
            // Layer 2: Main glow body
            DrawBillboard(enginePos, glowSize * 1.5f, baseColor, cameraPos);
            
            // Layer 3: Hot core (smallest, brightest)
            DrawBillboard(enginePos, glowSize * 0.8f, coreColor, cameraPos);
            
            // Layer 4: Engine trail (elongated in backward direction) - draw when accelerating or faster
            if (state != EngineState.Idle && intensity > 0.2f)
            {
                DrawEngineTrail(enginePos, backward, cameraPos, baseColor, glowSize, trailLength, intensity, state);
            }
            
            // Layer 5: Additional spark/flare for afterburner
            if (state == EngineState.Afterburner)
            {
                float sparkSize = glowSize * (0.5f + (float)_random.NextDouble() * 0.3f);
                Vector3 sparkOffset = backward * (trailLength * 0.3f + (float)_random.NextDouble() * trailLength * 0.2f);
                Color sparkColor = new Color(255, 200, 100, 180);
                DrawBillboard(enginePos + sparkOffset, sparkSize, sparkColor, cameraPos);
            }
        }

        /// <summary>
        /// Draw an elongated engine trail effect
        /// </summary>
        private void DrawEngineTrail(Vector3 startPos, Vector3 backward, Vector3 cameraPos,
            Color color, float width, float length, float intensity, EngineState state)
        {
            int segments = state == EngineState.Afterburner ? 8 : 5;
            
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float segmentAlpha = (1.0f - t) * intensity;
                float segmentSize = width * (1.0f - t * 0.6f);
                
                // Add some wavering for afterburner
                Vector3 offset = backward * (length * t);
                if (state == EngineState.Afterburner)
                {
                    float waver = (float)Math.Sin(_pulseTimer * 20f + i * 0.5f) * 0.05f * t;
                    offset += Vector3.Cross(backward, Vector3.Up) * waver;
                }
                
                Vector3 segmentPos = startPos + offset;
                
                Color segmentColor = color * segmentAlpha;
                segmentColor.A = (byte)(color.A * segmentAlpha);
                
                DrawBillboard(segmentPos, segmentSize, segmentColor, cameraPos);
            }
        }

        /// <summary>
        /// Draw a camera-facing billboard quad
        /// </summary>
        private void DrawBillboard(Vector3 position, float size, Color color, Vector3 cameraPosition)
        {
            // Create billboard that always faces camera
            Vector3 forward = cameraPosition - position;
            if (forward.LengthSquared() < 0.0001f)
                forward = Vector3.Forward;
            forward.Normalize();
            
            Vector3 right = Vector3.Cross(Vector3.Up, forward);
            if (right.LengthSquared() < 0.0001f)
                right = Vector3.Right;
            right.Normalize();
            
            Vector3 up = Vector3.Cross(forward, right);
            
            // Create quad vertices
            VertexPositionColor[] vertices = new VertexPositionColor[4];
            
            vertices[0] = new VertexPositionColor(position + (-right - up) * size, color);
            vertices[1] = new VertexPositionColor(position + (right - up) * size, color);
            vertices[2] = new VertexPositionColor(position + (-right + up) * size, color);
            vertices[3] = new VertexPositionColor(position + (right + up) * size, color);
            
            short[] indices = { 0, 1, 2, 2, 1, 3 };
            
            _effect.World = Matrix.Identity;
            
            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                
                _graphicsDevice.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    vertices,
                    0,
                    4,
                    indices,
                    0,
                    2
                );
            }
        }
    }
}
