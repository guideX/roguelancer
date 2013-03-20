// Rougelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Rougelancer.Functionality;
namespace Rougelancer.Objects {
    public class clsGround {
        public clsModel lModel;
        public void LoadContent(string _Path, ContentManager _Content) {
            lModel = new clsModel();
            lModel.Init(_Path, _Content);
        }
        //public void Update() {
            //lModel.Update();
        //}
        public void Draw(clsCamera _Camera)
        {
            lModel.Draw(_Camera);
        }
    }
}
