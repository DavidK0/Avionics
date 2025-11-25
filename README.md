# Avionics
Avionics is a mod for Kitten Space Agency that adds a Horizontal Situation Indicator (HSI) which you can use to fly to the position of any airport in the world. This mod does *not* add airports models or runways to KSA; it only adds the navigation tools to get to where they would be.
## How to install
1. Install [StarMap](https://github.com/StarMapLoader/StarMap/)
2. Download and unzip [the latest release of this mod](https://github.com/DavidK0/Avionics/releases/latest)
3. Place the contents in `Kitten Space Agency\Content\`. Your content folder should look something like this:
```
├── Core
├── Avionics
│   ├── Avionics.deps.json
│   ├── Avionics.dll
│   ├── airports.json
│   └── mod.toml
```
5. Edit the `Manifest.toml` in `My Games\Kitten Space Agency\` and add the following:
```
[mods]
name = "StellarAnalytics"
enabled = true
```
6. Run KSA through StarMap

## How to use in game
1. Enter the ICAO code of an airport in the Avionics window
2. Select a runway from the menu bar
3. Switch the page to 'HSI'
