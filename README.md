# Roguelancer - Freelancer-Style Space Flight Game

## Overview
A Freelancer-inspired space flight game built with MonoGame featuring:
- **Advanced Flight Mechanics** - Full 6-degrees of freedom with afterburner, cruise, and more
- **Chase Camera** - Smooth following camera that tracks your ship
- **Starfield Background** - 3000+ procedurally placed stars
- **Engine Glow Effects** - Dynamic thruster effects that respond to flight mode
- **HUD Display** - Visual throttle bar and status indicators

## Complete Keyboard Controls

### ?? Throttle & Speed Control
- **W** - Increase throttle / Accelerate forward
- **S** - Decrease throttle / Full stop / Reverse thrust
- **TAB** - Toggle Afterburner (150 speed - extra fast!)
- **Z** - Toggle Cruise mode (300 speed - autopilot speed)
- **X** - Kill engines (drift mode - momentum only)
- **R** - Match target speed (resets to 50% throttle)

### ?? Flight Control (Pitch & Yaw)
**Option 1 - Arrow Keys:**
- **? (Up Arrow)** - Pitch up (nose up)
- **? (Down Arrow)** - Pitch down (nose down)
- **? (Left Arrow)** - Yaw left (turn left)
- **? (Right Arrow)** - Yaw right (turn right)

**Option 2 - IJKL Keys:**
- **I** - Pitch up
- **K** - Pitch down
- **J** - Yaw left
- **L** - Yaw right

**Option 3 - A/D Keys (when not strafing):**
- **A** - Yaw left
- **D** - Yaw right

### ?? Roll Control
- **Q** - Roll left (manual barrel roll)
- **E** - Roll right (manual barrel roll)
- *Note: Ship auto-banks when turning unless you manually roll*

### ???? Strafe Movement
Hold **Shift** + direction to strafe:
- **Shift + A** - Strafe left
- **Shift + D** - Strafe right
- **Shift + W** - Strafe up
- **Shift + S** - Strafe down

### ?? System Controls
- **ESC** - Exit game

## Flight Modes Explained

### ?? Normal Flight
- Default mode with standard throttle control
- Max speed: **80 units/sec**
- Full maneuverability
- Throttle range: 0-100%

### ?? Afterburner Mode (TAB)
- High-speed pursuit mode
- Max speed: **150 units/sec**
- Reduced turn rate (60% of normal)
- Engine glow intensifies (red/bright)
- Press W to accelerate, S to cancel

### ?? Cruise Mode (Z)
- Long-distance travel
- Auto-speed: **300 units/sec**
- Automatically maintains speed
- Any turn or throttle input cancels cruise
- Perfect for exploring

### ? Engines Killed (X)
- Drifts with current momentum
- No thrust applied
- Slow velocity decay
- Useful for stealth or coasting
- Press X again to restart engines

### ?? Reverse Thrust (S held)
- Press S when stopped to reverse
- Max reverse speed: **40 units/sec**
- Slower acceleration than forward
- Useful for fine positioning

## Controls at a Glance

```
???????????????????????????????????????????????????
?  THROTTLE              MANEUVER                 ?
?  W - Forward           ?/I - Pitch Up           ?
?  S - Stop/Reverse      ?/K - Pitch Down         ?
?  TAB - Afterburner     ?/J/A - Yaw Left         ?
?  Z - Cruise            ?/L/D - Yaw Right        ?
?  X - Kill Engine       Q - Roll Left            ?
?  R - Match Speed       E - Roll Right           ?
?                                                  ?
?  STRAFE (Hold Shift)                            ?
?  A/D - Left/Right      W/S - Up/Down            ?
???????????????????????????????????????????????????
```

## HUD Elements

### Throttle Bar (Right Side)
- Vertical bar showing current throttle
- **Blue/Cyan** - Normal flight
- **Red** - Afterburner active
- **Yellow** - Cruise mode
- **Orange** - Reverse thrust
- **Gray** - Engines killed

### Status Box (Top Right)
- Colored indicator shows current flight mode
- Flashes during special modes

## Flight Mechanics

### Auto-Banking
The ship automatically banks (rolls) when turning left or right, just like in Freelancer. This creates a more realistic and cinematic flight feel.

### Strafe Movement
Hold Shift and use WASD to strafe without changing your ship's orientation. Great for:
- Dodging asteroids
- Fine positioning near stations
- Combat maneuvering

### Speed & Acceleration
- **Normal flight**: Smooth acceleration to max speed
- **Afterburner**: Rapid acceleration but harder to turn
- **Cruise**: Instant jump to cruise speed
- **Reverse**: Half the speed of forward thrust

## Project Structure

### Core Classes

#### Ship.cs
Contains all ship flight mechanics:
- Position and orientation tracking
- Comprehensive keyboard input handling
- Physics-based movement with acceleration
- Multiple flight modes (afterburner, cruise, etc.)
- Banking, turning, and strafing mechanics
- Model rendering with lighting

**Key Properties:**
- `MaxSpeed` - Normal maximum speed (default: 80)
- `AfterburnerSpeed` - Speed during afterburner (default: 150)
- `CruiseSpeed` - Autopilot cruise speed (default: 300)
- `MaxReverseSpeed` - Maximum reverse speed (default: 40)
- `Acceleration` - How fast the ship accelerates (default: 40)
- `TurnSpeed` - Rotation speed (default: 2.5)
- `BankAmount` - How much the ship banks during turns (default: 1.2)
- `StrafeSpeed` - Strafe movement speed (default: 30)

#### Camera.cs
Chase camera system that follows the player:
- Smooth camera movement with lerp
- Maintains distance behind ship
- Always looks ahead of the ship
- Configurable field of view and distance

#### Starfield.cs
Procedural starfield generator:
- Creates 3000 stars in a sphere around the player
- Stars have varying brightness and color
- Always centered on the camera (parallax effect)
- Rendered as additive blend for glow effect

#### EngineGlow.cs
Engine thruster visual effects:
- Billboard particles that face the camera
- Intensity based on throttle and flight mode
- Extra bright during afterburner
- Procedurally generated glow texture
- Additive blending for bright glow

## Ship Model

The game uses the **Scimitar** ship model from your content folder:
- Location: `Content/SHIPS/scimitar/Scimitar2.fbx`
- Includes multiple textures for different ship parts
- The model is automatically loaded and rendered with lighting

## Customization

### Adjusting Ship Performance
Edit `Ship.cs` properties:

```csharp
_playerShip.MaxSpeed = 100f;           // Normal max speed
_playerShip.AfterburnerSpeed = 200f;   // Afterburner speed
_playerShip.CruiseSpeed = 400f;        // Cruise speed
_playerShip.MaxReverseSpeed = 50f;     // Reverse max speed
_playerShip.Acceleration = 60f;        // Acceleration rate
_playerShip.TurnSpeed = 3.5f;          // Turn rate
_playerShip.BankAmount = 2.0f;         // Banking angle
_playerShip.StrafeSpeed = 40f;         // Strafe speed
```

### Adjusting Camera
Edit `Camera.cs` private fields:

```csharp
private float _distance = 15f;  // Camera distance behind ship
private float _height = 3f;     // Camera height above ship
```

### Adjusting Engine Glow Position
Edit `EngineGlow.cs` engine offset positions in `DrawEngineGlows()`:

```csharp
List<Vector3> engineOffsets = new List<Vector3>
{
    new Vector3(-1.5f, -0.3f, -3f),  // Left engine
    new Vector3(1.5f, -0.3f, -3f)    // Right engine
};
```

## Tips & Tricks

1. **Combat Maneuvering**: Use afterburner + strafe for aggressive dodging
2. **Precise Landing**: Use reverse thrust and strafe for fine control
3. **Long Travel**: Enable cruise mode for fast, hands-free travel
4. **Drift Turning**: Kill engines (X) and use momentum to glide through turns
5. **Quick Stop**: Press S repeatedly to decelerate quickly
6. **Barrel Rolls**: Use Q/E for defensive rolls while strafing

## Next Steps

Here are some features you might want to add:

1. **Mouse Flight** - Use mouse for more intuitive aiming
2. **Combat System** - Weapons, projectiles, enemies, targeting
3. **Enhanced Physics** - Full Newtonian flight model option
4. **HUD Enhancement** - Radar, targeting reticle, ship status, damage indicators
5. **Space Objects** - Asteroids, stations, planets, jump gates
6. **Sound Effects** - Engine sounds (varying by mode), weapons, ambient space
7. **Multiple Ships** - Ship selection with different stats and handling
8. **Missions System** - Objectives, waypoints, navigation, story missions
9. **Trading System** - Economy, cargo, buying/selling goods
10. **Multiplayer** - Network play with other pilots

## Technical Notes

- Built with **MonoGame 3.8** for .NET 9
- Uses **BasicEffect** for rendering (simple but effective)
- All rendering is done with built-in MonoGame classes
- No external dependencies beyond MonoGame
- Ready for expansion with additional game systems

## Running the Game

Simply build and run:
```bash
dotnet run
```

Or press **F5** in Visual Studio to debug.

**Enjoy your flight, pilot!** ??
