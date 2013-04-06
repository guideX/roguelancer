using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rougelancer.Functionality;

namespace Rougelancer.Interfaces {
    interface intGame {
        void Initialize(clsGame _Game);
        void LoadContent(clsGame _Game);
        void Update(clsGame _Game);
        void Draw(clsGame _Game);
    }
}
