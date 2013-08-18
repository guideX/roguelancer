using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace Rougelancer.Functionality {
    class clsCameraShake {
        private static readonly Random random = new Random();
        private bool shaking;
        private float shakeMagnitude;
        private float shakeDuration;
        private float shakeTimer;
        private Vector3 shakeOffset;
        private float NextFloat() {
            return (float)random.NextDouble() * 2f - 1f;
        }
        public void Shake(float magnitude, float duration) {
            // We're now shaking
            shaking = true;

            // Store our magnitude and duration
            shakeMagnitude = magnitude;
            shakeDuration = duration;

            // Reset our timer
            shakeTimer = 0f;
        }
        public void Update() {
            if(shaking) {
                shakeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if(shakeTimer >= shakeDuration) {
                    shaking = false;
                    shakeTimer = shakeDuration;
                }
                float progress = shakeTimer / shakeDuration;
                float magnitude = shakeMagnitude * (1f - (progress * progress));
                shakeOffset = new Vector3(NextFloat(), NextFloat(), NextFloat()) * magnitude;
                cameraPosition += shakeOffset;
                cameraTarget += shakeOffset;
            }
            Matrix.CreateLookAt(ref cameraPosition, ref cameraTarget, ref cameraUpVector, out view);
        }
    }
}
