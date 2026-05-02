# AUS Duct Support Add-in for Revit 2025

Automatically place rectangular duct supports based on SMACNA and international standards with intelligent host detection, load calculations, and comprehensive reporting.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Revit 2025](https://img.shields.io/badge/Revit-2025-blue.svg)](https://www.autodesk.com/products/revit)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

## Features

- **Multi-Standard Support**: SMACNA (North America), DW/144 (UK), VDI 3803 (Germany), AS 4254 (Australia), or custom rules
- **Intelligent Host Detection**: Automatically detects ceiling, beam, floor, or wall hosting
- **Four Support Types**:
  - Ceiling/beam mounted (hanging supports)
  - Ground/floor mounted (when no ceiling above)
  - Wall mounted (for ducts near walls)
  - Vertical duct supports
- **Load Calculations**: Structural load calculations with rod sizing recommendations
- **Insulation Awareness**: Considers insulation thickness in support sizing
- **Clash Detection**: Pre-placement validation against existing elements
- **Beam Alignment**: Optional alignment with structural beams
- **Comprehensive Reporting**: Excel schedules and PDF reports
- **Dark Mode & RTL**: Full dark theme support and Arabic localization

## Installation

### Option 1: Installer (Recommended)

1. Download the latest installer from [Releases](../../releases)
2. Run `DuctSupportAddin-Setup.exe`
3. Follow the installation wizard
4. Restart Revit 2025

### Option 2: Manual Installation

1. Download the latest release ZIP from [Releases](../../releases)
2. Extract to: `%APPDATA%\Autodesk\Revit\Addins\2025\DuctSupportAddin\`
3. Ensure the folder structure looks like:
   ```
   DuctSupportAddin/
   ├── DuctSupportAddin.dll
   ├── DuctSupportAddin.addin
   └── Families/
       └── RfaFiles/
           ├── RecDuctSupport.rfa
           ├── GroundDuctSupport.rfa
           ├── WallSupportDuct.rfa
           ├── VerticalWallSupportDuct.rfa
           └── VerticalFloorSupportDuct.rfa
   ```
4. Restart Revit 2025

## Requirements

- Revit 2025
- Windows 10/11 x64
- .NET 8.0 Runtime (included in installer)

## Building from Source

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK
- Revit 2025 installed (for API references)

### Build Steps

```bash
# Clone the repository
git clone https://github.com/yourusername/DuctSupportAddin.git
cd DuctSupportAddin

# Build Release
dotnet build -c Release

# Or use Visual Studio
# Open DuctSupportAddin.sln and build
```

The add-in automatically deploys to:
```
%APPDATA%\Autodesk\Revit\Addins\2025\DuctSupportAddin\
```

### Creating the Installer

```bash
# Build release first
dotnet build -c Release

# Run the build script (creates installer)
.\build.ps1

# Or manually with Inno Setup
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer\DuctSupportAddin.iss
```

## Usage

1. Open a Revit project with rectangular ducts
2. Go to **AUS MEP** tab → **Duct Supports** panel → **Place Supports**
3. Configure options:
   - Select scope (Entire Model, Active View, or Selection)
   - Choose duct systems (Supply, Return, Exhaust)
   - Select spacing standard and adjustment percentage
   - Enable/disable horizontal and vertical supports
   - Configure wall proximity settings
4. Click **Preview** to see placements before committing
5. Click **Place Supports** to execute

## Support Families

The add-in includes 5 support families:

| Family | Use Case | Key Parameters |
|--------|----------|----------------|
| `RecDuctSupport.rfa` | Ceiling/beam mounted | `Duct_Height`, `Duct_Width` |
| `GroundDuctSupport.rfa` | Floor mounted | `Support_Height`, `Duct_Width` |
| `WallSupportDuct.rfa` | Horizontal duct near wall | `Support_Length` |
| `VerticalWallSupportDuct.rfa` | Vertical duct wall bracket | `Arm_Length`, `Clamp_Size` |
| `VerticalFloorSupportDuct.rfa` | Vertical duct floor support | `X`, `Y` |

## Spacing Standards

| Standard | Region | Example (600mm duct) |
|----------|--------|---------------------|
| SMACNA | North America | 3.0m horizontal |
| DW/144 | United Kingdom | 2.5m |
| VDI 3803 | Germany | 3.0m |
| AS 4254 | Australia | 2.4m |

Use the **Adjustment %** to modify spacing:
- `50%` = Half spacing (more supports)
- `100%` = Standard spacing
- `200%` = Double spacing (fewer supports)

## Configuration

Settings are saved to:
```
%APPDATA%\AUS\DuctSupportAddin\config.json
```

Logs are written to:
```
%APPDATA%\AUS\DuctSupportAddin\Logs\
```

## Project Structure

```
DuctSupportAddin/
├── App.cs                          # Ribbon setup
├── Commands/                       # IExternalCommand implementations
├── Core/
│   ├── Analysis/                   # Duct analysis, fitting detection
│   ├── Hosting/                    # Host detection (ceiling, wall, floor)
│   ├── Placement/                  # Support placement logic
│   ├── Spacing/                    # SMACNA and other standards
│   ├── Structural/                 # Load calculations
│   ├── Validation/                 # Clash detection
│   └── Export/                     # Excel, PDF, CSV export
├── Families/                       # RFA support families
├── Models/                         # Data models
├── UI/
│   ├── Views/                      # WPF windows
│   ├── ViewModels/                 # MVVM view models
│   └── Resources/                  # Themes (Light/Dark)
├── Localization/                   # English + Arabic
└── Utilities/                      # Helpers, logging
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contact

**Abdul Khaled Sultan**  
Email: abdul.khaled.sultan@gmail.com

---

© 2024 AUS. All rights reserved.
