# Flight Model Configuration Guide

This guide shows you how to tune the ship's flight characteristics to your preference.

## Ship Speed Configuration

Edit these values in `Ship.cs` constructor or after ship creation:

```csharp
// In Ship.cs or Game1.cs after creating _playerShip

// SPEED SETTINGS
MaxSpeed = 80f;              // Normal maximum speed
MaxReverseSpeed = 40f;       // Reverse thrust max speed
CruiseSpeed = 300f;          // Cruise mode autopilot speed
AfterburnerSpeed = 150f;     // Afterburner boost speed
StrafeSpeed = 30f;           // Lateral strafe movement speed

// ACCELERATION
Acceleration = 40f;          // How quickly ship reaches target speed
                            // Higher = more responsive, Lower = more drift

// TURNING & MANEUVERABILITY  
TurnSpeed = 2.5f;           // How fast the ship rotates
                            // Higher = more agile, Lower = sluggish

BankAmount = 1.2f;          // Auto-banking angle during turns
                            // Higher = more dramatic rolls, Lower = subtle
```

## Pre-made Configurations

### ??? Racing Configuration (Fast & Agile)
```csharp
MaxSpeed = 120f;
AfterburnerSpeed = 250f;
CruiseSpeed = 500f;
Acceleration = 80f;
TurnSpeed = 4.0f;
BankAmount = 2.0f;
```

### ?? Freighter Configuration (Heavy & Slow)
```csharp
MaxSpeed = 50f;
AfterburnerSpeed = 90f;
CruiseSpeed = 200f;
Acceleration = 20f;
TurnSpeed = 1.5f;
BankAmount = 0.8f;
```

### ?? Fighter Configuration (Balanced Combat)
```csharp
MaxSpeed = 90f;
AfterburnerSpeed = 160f;
CruiseSpeed = 350f;
Acceleration = 50f;
TurnSpeed = 3.0f;
BankAmount = 1.5f;
StrafeSpeed = 45f;
```

### ?? UFO Configuration (Super Agile)
```csharp
MaxSpeed = 100f;
AfterburnerSpeed = 180f;
CruiseSpeed = 400f;
Acceleration = 100f;
TurnSpeed = 5.0f;
BankAmount = 0.3f;  // Minimal banking
StrafeSpeed = 60f;
```

### ?? Freelancer Classic Configuration
```csharp
MaxSpeed = 80f;
AfterburnerSpeed = 150f;
CruiseSpeed = 300f;
MaxReverseSpeed = 40f;
Acceleration = 40f;
TurnSpeed = 2.5f;
BankAmount = 1.2f;
StrafeSpeed = 30f;
```

## Camera Configuration

Edit these in `Camera.cs`:

```csharp
// CAMERA DISTANCE
private float _distance = 15f;   // Distance behind ship
                                 // Larger = further away, Smaller = closer

private float _height = 3f;      // Height above ship
                                 // Positive = above, Negative = below

// CAMERA SMOOTHING
// In Follow() method, change the smoothing parameter:
public void Follow(..., float smoothing = 0.15f)
// Lower (0.05f) = smooth but laggy
// Higher (0.3f) = responsive but jerky
// Default (0.15f) = balanced
```

## Engine Glow Configuration

Edit these in `EngineGlow.cs` in the `DrawEngineGlows()` method:

```csharp
// ENGINE POSITIONS (adjust for your ship model)
List<Vector3> engineOffsets = new List<Vector3>
{
    new Vector3(-1.5f, -0.3f, -3f),  // Left engine (X, Y, Z)
    new Vector3(1.5f, -0.3f, -3f),   // Right engine
    // Add more engines:
    // new Vector3(0f, 0.5f, -3f),   // Center top engine
};

// GLOW SIZE
float size = 1.5f + intensity * 1.0f;  // Base size + throttle scaling

// GLOW COLOR
Color glowColor = new Color(
    0.3f + intensity * 0.7f,  // Red component
    0.5f + intensity * 0.5f,  // Green component
    1.0f,                      // Blue component (full)
    intensity                  // Alpha (transparency)
);
```

## Starfield Configuration

Edit these in `Game1.cs` LoadContent():

```csharp
// Star count (more = denser, slower performance)
_starfield = new Starfield(GraphicsDevice, 3000);  // Change 3000 to desired count

// In Starfield.cs constructor, adjust:
float radius = 500f + (float)(random.NextDouble() * 2000f);  // Distance range
float brightness = 0.5f + (float)(random.NextDouble() * 0.5f);  // Star brightness
```

## Performance Tuning

### High Performance (Lower Quality)
```csharp
// Game1.cs Initialize()
_graphics.PreferredBackBufferWidth = 1280;
_graphics.PreferredBackBufferHeight = 720;

// Starfield.cs
new Starfield(GraphicsDevice, 1000);  // Fewer stars

// Camera.cs
float farPlane = 5000f;  // Shorter draw distance
```

### Ultra Quality (Lower Performance)
```csharp
// Game1.cs Initialize()
_graphics.PreferredBackBufferWidth = 2560;
_graphics.PreferredBackBufferHeight = 1440;
_graphics.IsFullScreen = true;

// Starfield.cs
new Starfield(GraphicsDevice, 5000);  // More stars

// Camera.cs
float farPlane = 20000f;  // Longer draw distance
```

## Testing Your Changes

After modifying values:

1. Save all files
2. Build: `dotnet build`
3. Run: `dotnet run`
4. Test flight mechanics
5. Adjust and repeat

## Recommended Testing Procedure

1. **Test Speeds**: Press TAB for afterburner, Z for cruise - do they feel right?
2. **Test Turning**: Make sharp turns - is it too sluggish or too twitchy?
3. **Test Banking**: Watch the ship roll during turns - too much or too little?
4. **Test Strafe**: Hold Shift+WASD - is strafe speed balanced with forward speed?
5. **Test Acceleration**: Press W - does it take too long to reach max speed?

## Common Issues & Fixes

**Ship turns too slowly:**
- Increase `TurnSpeed` to 3.5f or higher

**Ship accelerates too fast:**
- Decrease `Acceleration` to 20f or lower

**Banking looks weird:**
- Adjust `BankAmount` (1.0f = natural, 0.5f = subtle, 2.0f = dramatic)

**Camera too close/far:**
- Adjust `_distance` in Camera.cs (10f-25f range)

**Afterburner too fast/slow:**
- Balance `AfterburnerSpeed` with `MaxSpeed` (usually 1.5x to 2x)

**Engine glows in wrong position:**
- Load game, look at ship engines, adjust `engineOffsets` X,Y,Z values
- X = left(-) / right(+)
- Y = down(-) / up(+)  
- Z = forward(+) / backward(-)

---

Happy tuning! Experiment and find what feels best for your game! ??
