using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
namespace Roguelancer.Functionality {
    public class clsSettings {
        public string lAppPath = System.IO.Directory.GetCurrentDirectory();
        public string lShipTexture = "SHIPS\\PI_TRANSPORT\\PI_TRANSPORT";
        //public string lEarth = "Earth";
        //public Vector3 lEarthStartPosition = new Vector3(0, 1000, 2000);
        public string lGroundTexture = "";
        public string lFont = "LucidaFont";
        public Vector2 lResolution = new Vector2(1024, 768);
        public clsSettingsShip lEarth = new clsSettingsShip("Earth", new Vector3(0, 1000, 2000));
    }
    public class clsSettingsShip {
        public string shipModel { get; set; }
        public Vector3 shipStartupPosition { get; set; }
        public clsSettingsShip(string _shipModel, Vector3 _shipStartupPosition) {
            shipModel = _shipModel;
            shipStartupPosition = _shipStartupPosition;
        }
    }
}