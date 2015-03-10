using System;
using Microsoft.Xna.Framework;
namespace Roguelancer.Interfaces {
    interface INotificationSettings {
        void Setup(string name, string text, TimeSpan time, Vector3 location, bool showForever);
    }
}