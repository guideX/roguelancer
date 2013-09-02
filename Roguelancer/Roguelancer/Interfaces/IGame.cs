using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Functionality;
namespace Roguelancer.Interfaces {
    interface IGame {
        void Initialize(RoguelancerGame game);
        void LoadContent(RoguelancerGame game);
        void Update(RoguelancerGame game);
        void Draw(RoguelancerGame game);
    }
}