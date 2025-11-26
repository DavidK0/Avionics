# Avionics
Avionics is a mod for Kitten Space Agency that adds a Horizontal Situation Indicator (HSI) that you can use to fly an ILS approach to the position of any airport in the world. This mod does *not* add airport models or runways to KSA; it only adds the navigation tools to get to where they would be.

**Updated for KSA v2025.11.11.2924**
## How to install
1. Install [StarMap](https://github.com/StarMapLoader/StarMap/)
   1. Download and unzip [the latest release of StarMap](https://github.com/StarMapLoader/StarMap/releases/latest)
   2. Run the .exe and follow the instructions
2. Install [ModMenu](https://github.com/MrJeranimo/ModMenu/)
   1. Download and unzip [the latest release of ModMenu](https://github.com/MrJeranimo/ModMenu/releases/latest)
   2. Put the contents in `Kitten Space Agency\Content\`
3. Download and unzip [the latest release of Avionics](https://github.com/DavidK0/Avionics/releases/latest)
4. Place the contents into `Kitten Space Agency\Content\`. Your content folder should look something like this:
```
├── Core
├── Avionics
│   ├── Avionics.deps.json
│   ├── Avionics.dll
│   ├── airports.json
│   ├── mod.toml
│   └── ModMenu.Attributes.dll
├── ModMenu
│   ├── LICENSE.txt
│   ├── mod.toml
│   └── ModMenu.dll
```
5. Edit the `Manifest.toml` in `My Games\Kitten Space Agency\` to include Avionics and ModMenu. Your final `Manifest.toml` should look something like this:
```
[[mods]]
id = "Core"
enabled = true

[[mods]]
id = "Avionics"
enabled = true

[[mods]]
id = "ModMenu"
enabled = true
```

6. Run KSA through StarMap

## How to use in game
1. Open the Avionics pages with the 'Avionics' button in the mod menu.
1. Enter the ICAO code of an airport in the runway selection window and select a runway
3. View the directions to the runway on the 'HSI' page.
