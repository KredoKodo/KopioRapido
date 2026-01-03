# Adaptive Window Sizing Implementation

## Overview

KopioRapido now features intelligent, adaptive window sizing that:
- **Automatically sizes** based on screen resolution using golden ratio (1.618:1) proportions
- **Persists size and position** across sessions with validation
- **Allows user resizing** with minimum constraints (750×550)
- **Handles edge cases** gracefully (small screens, multi-monitor, disconnected displays)

## Golden Ratio Proportions

The window uses the golden ratio (φ = 1.618) for aesthetically pleasing proportions:
- Width:Height = 1.618:1
- Example: 1000px wide → 618px tall
- Example: 800px tall → 1294px wide

## Sizing Strategy

### Conservative Adaptive Approach
- Calculates 70% of screen work area (excludes taskbar/menubar)
- Applies golden ratio to maintain elegant proportions
- Clamps to minimum (750×550) and maximum (1600×1200) sizes
- Ensures window never exceeds screen boundaries

### Size Examples by Screen Resolution

| Screen Resolution | Work Area (70%) | Golden Ratio Applied | Final Window Size |
|-------------------|----------------|----------------------|-------------------|
| 800×600 (minimum) | 560×420        | Clamped to minimum   | 750×550 (min)     |
| 1280×720          | 896×504        | 896×553              | 896×553           |
| 1920×1080         | 1344×756       | 1344×830             | 1344×830          |
| 2560×1440         | 1792×1008      | Clamped to maximum   | 1600×988          |
| 3840×2160 (4K)    | 2688×1512      | Clamped to maximum   | 1600×1200 (max)   |

## Components

### WindowSizer.cs (`Helpers/WindowSizer.cs`)

Utility class for window size calculations:

```csharp
// Calculate default size for screen
(int width, int height) = WindowSizer.CalculateDefaultSize(screenWidth, screenHeight);

// Validate saved size against screen
(int width, int height) = WindowSizer.ValidateSavedSize(savedW, savedH, screenW, screenH);

// Validate saved position (ensures window is on-screen)
(int x, int y) = WindowSizer.ValidateSavedPosition(savedX, savedY, winW, winH, screenW, screenH);

// Center window on screen
(int x, int y) = WindowSizer.CenterWindow(windowWidth, windowHeight, screenWidth, screenHeight);
```

**Key Constants:**
- `GoldenRatio = 1.618` - Width-to-height ratio
- `MinWidth = 750`, `MinHeight = 550` - Minimum window constraints
- `MaxWidth = 1600`, `MaxHeight = 1200` - Maximum window constraints
- `WorkAreaPercentage = 0.70` - 70% of screen work area

### WindowPreferences.cs (`Helpers/WindowPreferences.cs`)

Manages window state persistence using .NET MAUI Preferences API:

```csharp
// Save size
WindowPreferences.SaveSize(width, height);

// Save position
WindowPreferences.SavePosition(x, y);

// Retrieve saved size (returns null if not saved)
var (width, height) = WindowPreferences.GetSavedSize();

// Retrieve saved position (returns null if not saved)
var (x, y) = WindowPreferences.GetSavedPosition();
```

**Storage Location:**
- Windows: `%LocalAppData%\KopioRapido\Preferences`
- macOS: `~/Library/Preferences/com.kopiorapido.preferences.plist`

### App.xaml.cs Updates

Modified `CreateWindow()` method to implement adaptive sizing:

1. **Calculate/Restore Size:**
   - Try to restore saved size from preferences
   - If no saved size, calculate default using golden ratio
   - Validate against screen dimensions and constraints

2. **Calculate/Restore Position:**
   - Try to restore saved position from preferences
   - If no saved position (or invalid), center window
   - Validate window is fully visible on screen

3. **Configure Window Properties:**
   - Enable resizing, maximizing, minimizing
   - Set borderless design (title bar hidden)
   - Attach event handlers for size/position changes

4. **Persist Changes (Windows):**
   - `appWindow.Changed` event saves size/position on change
   - Automatic real-time persistence

5. **Persist Changes (macOS):**
   - Saved in `OnSleep()` method (app backgrounding)
   - Native event handlers would require additional platform code

## Platform Differences

### Windows
- **Screen Detection:** Uses `DisplayArea.WorkArea` API for accurate dimensions
- **Persistence:** Real-time via `appWindow.Changed` event
- **Resizing:** Fully supported with minimum constraints
- **Multi-Monitor:** Automatically handles display changes

### macOS
- **Screen Detection:** Uses default assumptions (1920×1080) as `DisplayArea` API unavailable
- **Persistence:** Saves on app sleep/background
- **Resizing:** Fully supported with `MinimumWidth/Height` constraints
- **Multi-Monitor:** Position validation prevents off-screen windows

## Edge Cases Handled

### Small Screens (800×600 minimum)
- If calculated size < minimum, uses minimum (750×550)
- Golden ratio may be slightly violated to fit screen
- Window still centered and fully visible

### Large Screens (4K and above)
- If calculated size > maximum, uses maximum (1600×1200)
- Prevents excessively large windows on high-res displays
- Golden ratio maintained after clamping

### Disconnected Monitor
- If saved position is off-screen, window is re-centered
- Validates position against current screen dimensions
- Falls back to calculated default if size is invalid

### First Launch
- No saved preferences exist
- Calculates default size using golden ratio (70% of work area)
- Centers window on primary display
- Saves initial size/position for next launch

### Window Maximized
- User can maximize window (currently supported but not persisted)
- Future enhancement: Save maximized state in preferences

## User Experience

### Resizing Behavior
- User can resize window freely (drag edges/corners)
- Minimum size enforced: 750×550 (prevents UI from breaking)
- No maximum enforced during manual resize (only for calculated defaults)
- Size automatically saved on resize

### Position Behavior
- User can drag window anywhere on screen
- Position automatically saved on move
- On restart, window appears at last position
- If position invalid (monitor disconnected), re-centered automatically

### Multi-Monitor Support
- Window respects work area of primary display
- Position validation prevents off-screen windows
- If moved to secondary monitor, position saved correctly
- On monitor disconnect, falls back to primary display centered

## Testing Checklist

- [x] ✅ Build succeeds with no errors
- [ ] Window opens at correct size on first launch (golden ratio)
- [ ] Window size persists across restarts
- [ ] Window position persists across restarts
- [ ] Minimum size constraints enforced (750×550)
- [ ] Window can be resized by user
- [ ] Window can be moved by user
- [ ] 800×600 screen support (minimum viable)
- [ ] 1920×1080 screen (most common)
- [ ] 4K screen (3840×2160)
- [ ] Multi-monitor setup
- [ ] Disconnected monitor recovery (position reset to primary)
- [ ] Window centered on first launch
- [ ] Saved position validated (not off-screen)
- [ ] Saved size validated (not too small)

## Future Enhancements

1. **Maximized State Persistence**
   - Save `IsMaximized` preference
   - Restore maximized state on launch

2. **Per-Monitor DPI Awareness**
   - Handle DPI scaling more intelligently
   - Adjust sizes for high-DPI displays

3. **Multi-Monitor Position Memory**
   - Remember which monitor window was on
   - Restore to same monitor if available

4. **macOS Screen Detection**
   - Implement native code to get actual screen dimensions
   - Remove hardcoded 1920×1080 assumption

5. **User Preferences UI**
   - Reset window size/position button in settings
   - Option to override golden ratio with custom aspect

## Implementation Notes

- All size/position calculations use logical pixels (MAUI handles DPI automatically)
- Preferences API is cross-platform and handles serialization automatically
- WindowSizer is stateless - all methods are static utilities
- No database or file I/O needed (Preferences API handles storage)
- Thread-safe: All preference operations are synchronous and atomic
