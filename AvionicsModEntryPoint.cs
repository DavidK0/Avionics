using Brutal.ImGuiApi;
using KSA;
using ModMenu;
using StarMap.API;

namespace Avionics {
    [StarMapMod]
    public class AvionicsModEntryPoint {
        [StarMapAllModsLoaded]
        public void fullyLoaded() {
            Patcher.Patch();
        }

        [StarMapUnload]
        public void unload() {
            Patcher.Unload();
        }

        internal AvionicsComputer avionicsComputer;

        [ModMenuEntry("Avionics")]
        public static void DrawMenu() {

            if(ImGui.BeginMenu("Change Units")) {
                if(ImGui.MenuItem("Kilometers")) {
                    UnitController.ChangeUnit(UnitController.UnitSystem.Kilometers);
                } else if(ImGui.MenuItem("Statute Miles")) {
                    UnitController.ChangeUnit(UnitController.UnitSystem.StatuteMiles);
                } else if(ImGui.MenuItem("Nautical Miles")) {
                    UnitController.ChangeUnit(UnitController.UnitSystem.NauticalMiles);
                }
                ImGui.EndMenu();
            }
            if(ImGui.MenuItem("Flight Management System"))
                FlightManagementSystemWindow.Toggle();
            if(ImGui.MenuItem("Autopilot"))
                AutopilotWindow.Toggle();
            if(ImGui.BeginMenu("Flight Instruments")) {
                if(ImGui.MenuItem("Horizontal Situation Indicator"))
                    FlightInstruments.hsiPageOn = !FlightInstruments.hsiPageOn;
                if(ImGui.MenuItem("Vertical Speed Indicator"))
                    FlightInstruments.vsiPageOn = !FlightInstruments.vsiPageOn;
                if(ImGui.MenuItem("Radar Altimeter"))
                    FlightInstruments.raPageOn = !FlightInstruments.raPageOn;
                if(ImGui.MenuItem("Airspeed Indicator"))
                    FlightInstruments.asiPageOn = !FlightInstruments.asiPageOn;
                if(ImGui.MenuItem("Heading Indicator"))
                    FlightInstruments.hiPageOn = !FlightInstruments.hiPageOn;
                if(ImGui.MenuItem("Turn Indicator"))
                    FlightInstruments.tiPageOn = !FlightInstruments.tiPageOn;
                if(ImGui.MenuItem("Artificial Horizion"))
                    FlightInstruments.aiPageOn = !FlightInstruments.aiPageOn;
                ImGui.EndMenu();
            }
        }

        [StarMapImmediateLoad]
        public void Init(Mod definingMod) {
            FlightManagementSystem.LoadAirportData("Content/Avionics/airports.json");
        }

        [StarMapAfterGui]
        public void OnAfterUi(double dt) {
            if(avionicsComputer == null && Program.ControlledVehicle != null)
                avionicsComputer = new AvionicsComputer(Program.ControlledVehicle);
            if(avionicsComputer == null)
                return;

            // Update avionics computer state
            avionicsComputer.Update((float) dt);

            // Runway selection page
            FlightManagementSystemWindow.Render(avionicsComputer);

            // Autopilot page
            AutopilotWindow.Render(avionicsComputer);

            // HSI, VSI, RA, ASI, HI pages
            FlightInstruments.RenderFlightInstruments(avionicsComputer);
        }
    }
}