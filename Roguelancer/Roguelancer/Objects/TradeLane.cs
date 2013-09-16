using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roguelancer.Functionality;
using Roguelancer.Interfaces;
namespace Roguelancer.Objects {
    public class TradeLaneCollection : IGame {
        public TradeLaneCollection(RoguelancerGame game) {
        }
        public void LoadContent(RoguelancerGame game) {
        }
        public void Initialize(RoguelancerGame game) {
        }
        public void Update(RoguelancerGame game) {
        }
        public void Draw(RoguelancerGame game) {
        }
        public void Reset(RoguelancerGame game) {
        }
    }
    public class TradeLane : IGame {
        public List<TradeLaneRing> tradeLaneRings { get; set; }
        private const int tradeLaneRingCount = 8;
        public TradeLane(RoguelancerGame game) {
            tradeLaneRings = new List<TradeLaneRing>();
            for(int i = 0; i < tradeLaneRingCount - 1; i++) {
                //tradeLaneRings.Add(new TradeLaneRing() {

                //});
            }
        }
        public void LoadContent(RoguelancerGame game) {
        }
        public void Initialize(RoguelancerGame game) {
        }
        public void Update(RoguelancerGame game) {
        }
        public void Draw(RoguelancerGame game) {
        }
        public void Reset(RoguelancerGame game) {
        }
    }
    public class TradeLaneRing { // : IGame {
        GameModel model;
        public TradeLaneRing(RoguelancerGame game) {
            model = new GameModel(game);
        }
        public void LoadContent(RoguelancerGame game) {
        }
        public void Initialize(RoguelancerGame game) {
        }
        public void Update(RoguelancerGame game) {
        }
        public void Draw(RoguelancerGame game) {
        }
        public void Reset(RoguelancerGame game) {
        }
    }
}