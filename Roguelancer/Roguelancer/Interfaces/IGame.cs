using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Functionality;
using Roguelancer.Objects;
namespace Roguelancer.Interfaces {
    interface IDockable {
        void Dock(RoguelancerGame game, Ship ship);
        void UnDock(RoguelancerGame game, Ship ship);
    }
    interface IGame {
        void Initialize(RoguelancerGame game);
        void LoadContent(RoguelancerGame game);
        void Update(RoguelancerGame game);
        void Draw(RoguelancerGame game);
    }
}