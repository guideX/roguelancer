﻿// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Rougelancer.Bloom {
    public class clsBloomHandler {
        private int lBloomSettings = 0;
        private clsBloomComponent lBloom;
        public clsBloomHandler(Microsoft.Xna.Framework.Game _Game) {
            lBloom = new clsBloomComponent(_Game);
            _Game.Components.Add(lBloom);
        }
        public void LoadContent() {
            // TODO
        }
        public void Update(bool _BloomVisible) {
            lBloom.lSettings = clsBloomSettings.PresetSettings[lBloomSettings];
            lBloom.Visible = _BloomVisible;
        }
        public void Draw() {
            lBloom.BeginDraw();
        }
    }
}
