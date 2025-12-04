# Avionics
Avionics is a mod for Kitten Space Agency that adds airplane flight instruments. The current features are:
* A Horizontal Situation Indicator (HSI)
* A Vertical Speed Indicator (VSI)
* A Radar Altimeter
* An autopilot with altitude hold, heading hold, and airport navigation
* The positions of thousands of airports across the world, which work with the HSI and autopilot.

This mod does *not* add airport models, runways, or airplanes to KSA; it only adds the navigation tools to get to where the airports would be.

**Updated for KSA v2025.12.14.3000**

<img width="401" height="303" alt="image" src="https://github.com/user-attachments/assets/8073446d-3090-4607-85ad-e2065c81c0ea" />

## How to install
1. Install [StarMap](https://github.com/StarMapLoader/StarMap/)
   1. Download and unzip [the latest release of StarMap](https://github.com/StarMapLoader/StarMap/releases/latest)
   2. Run the .exe and follow the instructions
2. Install [ModMenu](https://github.com/MrJeranimo/ModMenu/)
   1. Download and unzip [the latest release of ModMenu](https://github.com/MrJeranimo/ModMenu/releases/latest)
   2. Put the contents in `Kitten Space Agency\Content\`
3. Download and unzip the latest release of Avionics [from Github](https://github.com/DavidK0/Avionics/releases/latest) or [from SpaceDock](https://spacedock.info/mod/4057/Avionics)
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
1. Enter the ICAO code of an airport in the 'Runway Selection' window and select a runway
3. View the directions to the runway on the 'HSI' window.
4. Enable autopilot in the 'Autopilot' window and set the target altitude.

## Known Problems
* The game will crash if you leave the SoI of the Earth.

## Links
* [Avionics on Ahwoo Forums](https://forums.ahwoo.com/threads/avionics.560/)
* [Avionics on SpaceDock](https://spacedock.info/mod/4057/Avionics)
