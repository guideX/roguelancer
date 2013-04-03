using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
namespace Rougelancer.Functionality {
    public class clsSettings {
        public string lAppPath = System.IO.Directory.GetCurrentDirectory();
        public string lShipTexture = "SHIPS\\PI_TRANSPORT\\PI_TRANSPORT";
        public string lFont = "LucidaFont";
        public Vector2 lResolution = new Vector2(800, 600);
    }
}
