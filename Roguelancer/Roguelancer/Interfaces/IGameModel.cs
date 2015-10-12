using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Roguelancer.Interfaces {
    interface IGameModel : IGame {
        void UpdatePosition();
    }
}
