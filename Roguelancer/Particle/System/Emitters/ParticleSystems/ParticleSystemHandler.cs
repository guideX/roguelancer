/*
// Roguelancer 0.1 Pre Alpha by Leon Aiossa
// http://www.team-nexgen.org
using Roguelancer.Functionality;
using Roguelancer.Models;
namespace Roguelancer.Particle.System.ParticleSystems {
public class ParticleSystemHandler {
public ParticleSystemSettingsModel Settings { get; set; }
private ParticleSystem _particleSystem;
public ParticleSystemHandler(RoguelancerGame game, ParticleSystemSettingsModel particleSystemSettings) {
    Settings = particleSystemSettings;
    _particleSystem = new ParticleSystem(game);
    _particleSystem.Settings = Settings;
}
public void Initialize(RoguelancerGame game) {
    _particleSystem.Initialize(game);
}
public void LoadContent(RoguelancerGame game) {
    _particleSystem.LoadContent(game);
}
public void Update(RoguelancerGame game) {
    _particleSystem.Update(game, Settings);
}
public void Draw(RoguelancerGame game, GameModel model) {
    _particleSystem.Draw(game);
}
}
}    */
