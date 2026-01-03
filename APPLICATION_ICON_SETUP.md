# Application Icon Configuration

## Overview

KopioRapido's application icon has been configured using custom PNG icons created by the user. .NET MAUI automatically generates all required platform-specific icon sizes from a single source image.

## Icon Setup

### Source Icons
Location: `Resources/Images/icons_logos/`

Available sizes:
- 16x16.png
- 32x32.png
- 48x48.png
- 64x64.png
- 128x128.png
- 256x256.png
- 512x512.png
- 1024x1024.png (source for MAUI generation)

### MAUI Configuration

**Primary Icon:** `Resources/AppIcon/appicon.png` (1024x1024)

The 1024x1024 PNG is used as the source for all platform-specific icon generation. MAUI's build process automatically:

1. **Generates all required sizes** for each platform
2. **Creates platform-specific formats** (.ico for Windows, .icns for macOS)
3. **Handles Retina/HiDPI scaling** automatically
4. **Embeds icons** in the application bundle

**Important:** `ForegroundScale="1.0"` ensures the icon uses 100% of available space without padding.

### Project Configuration

In `KopioRapido.csproj`:

```xml
<ItemGroup>
    <!-- App Icon - Full bleed without padding -->
    <MauiIcon Include="Resources\AppIcon\appicon.png" 
              BaseSize="1024,1024" 
              ForegroundScale="1.0" />
</ItemGroup>
```

**Configuration Properties:**
- `BaseSize="1024,1024"` - Specifies source image dimensions
- `ForegroundScale="1.0"` - Uses 100% of space (default is 0.65 which adds 35% padding)

**Note:** Previously used SVG icons (`appicon.svg`, `appiconfg.svg`) have been replaced with PNG for better quality and platform compatibility.

## Platform-Specific Behavior

### macOS (Mac Catalyst)

**Generated Icons:**
- MAUI creates an `Assets.xcassets` structure automatically
- Generates `.icns` file with all required sizes
- App icon appears in:
  - Dock
  - Application folder
  - Window title bar
  - Task switcher (Cmd+Tab)
  - Spotlight search

**Info.plist Configuration:**
- `XSAppIconAssets`: Points to MAUI-generated asset catalog
- `LSApplicationCategoryType`: Set to `public.app-category.utilities`

**Resolution Support:**
- Standard resolution (16x16 through 512x512)
- Retina resolution (@2x versions automatically generated)
- All sizes embedded in `.icns` bundle

### Windows

**Generated Icons:**
- MAUI creates `.ico` file with embedded sizes
- Icon appears in:
  - Taskbar
  - Title bar
  - File Explorer
  - Start Menu
  - Alt+Tab switcher
  - System tray (if used)

**Required Sizes (automatically generated):**
- 16x16 - Taskbar (small icons), File Explorer
- 32x32 - File Explorer, taskbar
- 48x48 - Large icons view
- 256x256 - Extra large icons, high DPI

**Package.appxmanifest:**
- MAUI automatically updates manifest with icon references
- Supports Windows 10/11 high DPI scaling

## Best Practices Followed

### 1. Full Bleed Icon (No Padding)
✅ Using `ForegroundScale="1.0"` for full image utilization
✅ Icon designed to fill entire space without safe margins
✅ No gray background showing through

**Why This Matters:**
- MAUI's default `ForegroundScale="0.65"` was designed for SVG icons that might need padding
- This shrinks the icon to 65% of its size, leaving 35% empty space
- For pre-designed PNG icons (like yours), this creates unwanted padding
- Setting to 1.0 uses 100% of available space as intended

### 2. Single Source Image
✅ Using 1024x1024 PNG allows MAUI to generate optimal sizes for all platforms
✅ Prevents size inconsistencies across platforms
✅ Simplifies updates (change one file, rebuild)

### 2. PNG vs SVG
- PNG chosen over SVG for pixel-perfect rendering at all sizes
- Icon design optimized for small sizes (16x16, 32x32)
- Prevents scaling artifacts that can occur with complex SVG

### 3. Icon Design Guidelines
✅ **High contrast** - Icon visible in light and dark modes
✅ **Simple shapes** - Recognizable at 16x16
✅ **Consistent branding** - Same visual style as splash screen
✅ **No text** - Remains clear when scaled down

### 4. Platform Integration
✅ **macOS category** - Set to "Utilities" for proper App Store categorization
✅ **Retina support** - Automatic @2x generation
✅ **High DPI** - Windows scales icons automatically

## Build Process

### What Happens During Build

1. **Resizetizer** processes `appicon.png`
2. Generates all required sizes for target platform
3. Creates platform-specific icon formats:
   - macOS: `Assets.xcassets/appicon.appiconset/`
   - Windows: `app.ico`
4. Embeds icons in application bundle
5. Updates platform manifests with icon references

### Build Output Locations

**macOS:**
```
bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/
└── KopioRapido.app/
    └── Contents/
        └── Resources/
            └── Assets.xcassets/
                └── appicon.appiconset/
```

**Windows:**
```
bin/Debug/net10.0-windows/
└── KopioRapido.exe (icon embedded)
```

## Updating the Icon

To update the application icon:

1. Replace `Resources/AppIcon/appicon.png` with new 1024x1024 PNG
2. Clean build: `dotnet clean`
3. Rebuild: `dotnet build`
4. Icon automatically updated in all locations

**Optional:** Update source icons in `Resources/Images/icons_logos/` for consistency

## Testing the Icon

### macOS
1. Build and run application
2. Check Dock icon (while running)
3. Check Applications folder icon
4. Test Cmd+Tab switcher
5. Check in Finder "Get Info" dialog

### Windows
1. Build and run application
2. Check taskbar icon
3. Check Start Menu icon
4. Check File Explorer icon
5. Test Alt+Tab switcher

## Troubleshooting

### Icon has gray background or padding
- **Cause:** Default `ForegroundScale` is 0.65 (adds 35% padding)
- **Solution:** Add `ForegroundScale="1.0"` to MauiIcon in .csproj
- **Result:** Icon fills entire space without padding

### Icon not updating after rebuild
- **Solution:** `dotnet clean` before rebuilding
- **Reason:** Cached assets from previous build

### Icon appears pixelated
- **Check:** Ensure source PNG is 1024x1024 and high quality
- **Verify:** No scaling artifacts in source image

### Wrong icon showing
- **macOS:** Delete `~/Library/Caches/com.companyname.kopiorapido/`
- **Windows:** Clear icon cache or restart

### Build warnings about Assets.xcassets
- **Normal:** MAUI resizetizer generates these automatically
- **Do not** manually create Assets.xcassets in Platforms/MacCatalyst/

## File Locations Summary

| Purpose | Location | Generated |
|---------|----------|-----------|
| Source icons (all sizes) | `Resources/Images/icons_logos/*.png` | No (user-created) |
| MAUI source icon | `Resources/AppIcon/appicon.png` | No (copied from 1024x1024) |
| Old SVG icons | `Resources/AppIcon/*.svg` | No (deprecated) |
| macOS icon assets | `obj/.../Assets.xcassets/` | Yes (MAUI) |
| Windows .ico | `obj/.../app.ico` | Yes (MAUI) |
| Final app bundle | `bin/.../KopioRapido.app` or `.exe` | Yes (build) |

## Future Enhancements

1. **Splash Screen** - Update to match new icon design
2. **Document Icons** - Create icons for file associations
3. **Notification Icons** - Smaller variants for system notifications
4. **About Dialog** - Use high-res 512x512 for app info

## References

- [.NET MAUI App Icons Documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/images/app-icons)
- [Apple Human Interface Guidelines - App Icons](https://developer.apple.com/design/human-interface-guidelines/app-icons)
- [Windows App Icon Guidelines](https://learn.microsoft.com/en-us/windows/apps/design/style/iconography)
