using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Functionality;
namespace Roguelancer.Interfaces {
    interface IGame {
        void Initialize(clsGame _Game);
        void LoadContent(clsGame _Game);
        void Update(clsGame _Game);
        void Draw(clsGame _Game);
    }
}