# Avionics
Avionics is a mod for Kitten Space Agency that adds airplane flight instruments. The current features are:
* All the basic flight instruments of a steam gauge cockpit
* An FMS that stores the positions of thousands of airports across the world
* An autopilot with altitude hold, heading hold, and airport navigation

This mod does *not* add airport models, runways, or airplanes to KSA; it only adds the navigation tools to get to where the airports would be.

**Updated for KSA v2025.12.24.3014**

<img width="456" height="345" alt="ksa_header" src="https://github.com/user-attachments/assets/994fec16-fae4-42e1-b510-250949ad8b9e" />

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
1. Enter the ICAO code of an airport in the FMS, select a runway, and execute the plan
3. View the directions of your flight plan HSI.
4. Enable autopilot in the 'Autopilot' window. You must disable the manual mode on the in game autopilot.

## Links
* [Avionics on Ahwoo Forums](https://forums.ahwoo.com/threads/avionics.560/)
* [Avionics on SpaceDock](https://spacedock.info/mod/4057/Avionics)
* [Demonstration on Youtube](https://youtu.be/QDrmacfHLlM)
