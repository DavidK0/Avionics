using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace Avionics {
    public static class FlightSystems {
        public static bool windowOpen = false;

        public enum PageWindowMode {
            Docked,   // shown inside main CDUnit window
            Windowed, // shown in its own standalone ImGui window
            Hidden    // hidden, for pages that exist but aren’t visible anywhere
        }

        private class PageState {
            public PageWindowMode Mode;
            public bool WindowOpen;  // for the popped-out window's close button (ImGui.Begin with ref bool)
        }

        private static readonly PageState AutopilotPageState = new PageState {
            Mode = PageWindowMode.Docked,
            WindowOpen = false
        };

        private static readonly PageState CduPageState = new PageState {
            Mode = PageWindowMode.Docked,
            WindowOpen = false
        };

        private static readonly PageState NavPageState = new PageState {
            Mode = PageWindowMode.Docked,
            WindowOpen = false
        };

        // Toggle the main CDU window
        internal static void Toggle() {
            windowOpen = !windowOpen;
        }

        internal static void Render(AvionicsComputer avionicsComputer) {

            // Are there any docked pages to show in the main window?
            bool anyDocked =
                AutopilotPageState.Mode == PageWindowMode.Docked ||
                CduPageState.Mode == PageWindowMode.Docked;

            // ---------- Main Flight Systems window (tabs) ----------
            if(windowOpen && anyDocked) {
                ImGui.Begin("Flight Systems", ref windowOpen);

                if(ImGui.BeginTabBar("FlightSystemsTabBar")) {
                    if(AutopilotPageState.Mode == PageWindowMode.Docked) {
                        if(ImGui.BeginTabItem("Autopilot")) {
                            if(ImGui.SmallButton("Pop Out")) {
                                AutopilotPageState.Mode = PageWindowMode.Windowed;
                                AutopilotPageState.WindowOpen = true;
                            }

                            AutopilotPage.Render(avionicsComputer); // no Begin/End inside
                            ImGui.EndTabItem();
                        }
                    }

                    if(CduPageState.Mode == PageWindowMode.Docked) {
                        if(ImGui.BeginTabItem("CDU")) {
                            if(ImGui.SmallButton("Pop Out")) {
                                CduPageState.Mode = PageWindowMode.Windowed;
                                CduPageState.WindowOpen = true;
                            }

                            FlightManagementSystemPage.Render(avionicsComputer); // no Begin/End inside
                            ImGui.EndTabItem();
                        }
                    }

                    if(NavPageState.Mode == PageWindowMode.Docked) {
                        if(ImGui.BeginTabItem("Nav")) {
                            if(ImGui.SmallButton("Pop Out")) {
                                NavPageState.Mode = PageWindowMode.Windowed;
                                NavPageState.WindowOpen = true;
                            }

                            NavigationPage.Render(avionicsComputer); // no Begin/End inside
                            ImGui.EndTabItem();
                        }
                    }

                    ImGui.EndTabBar();
                }

                ImGui.End();
            }

            if(!anyDocked) {
                windowOpen = false;
            }

            // Popped-out Autopilot window
            if(AutopilotPageState.Mode == PageWindowMode.Windowed && AutopilotPageState.WindowOpen) {
                ImGui.Begin("Autopilot", ref AutopilotPageState.WindowOpen);

                if(ImGui.SmallButton("Dock Back")) {
                    AutopilotPageState.Mode = PageWindowMode.Docked;
                    AutopilotPageState.WindowOpen = false;
                } else if(AutopilotPageState.Mode == PageWindowMode.Windowed) {
                    // Only render if we didn't dock back this frame
                    AutopilotPage.Render(avionicsComputer);
                }

                ImGui.End();

                // If user clicked the X on the window:
                if(!AutopilotPageState.WindowOpen) {
                    // You can switch to Hidden instead if you want it not to reappear as a tab automatically
                    AutopilotPageState.Mode = PageWindowMode.Docked;
                    AutopilotPageState.WindowOpen = false;
                }
            }

            // Popped-out CDU window
            if(CduPageState.Mode == PageWindowMode.Windowed && CduPageState.WindowOpen) {
                ImGui.Begin("Control Display Unit", ref CduPageState.WindowOpen);

                if(ImGui.SmallButton("Dock Back")) {
                    CduPageState.Mode = PageWindowMode.Docked;
                    CduPageState.WindowOpen = false;
                } else if(CduPageState.Mode == PageWindowMode.Windowed) {
                    FlightManagementSystemPage.Render(avionicsComputer);
                }

                ImGui.End();

                if(!CduPageState.WindowOpen) {
                    CduPageState.Mode = PageWindowMode.Docked; // or Hidden if you want
                    CduPageState.WindowOpen = false;
                }
            }

            // Popped-out Nav window
            if(NavPageState.Mode == PageWindowMode.Windowed && NavPageState.WindowOpen) {
                ImGui.Begin("Navigation", ref NavPageState.WindowOpen);

                if(ImGui.SmallButton("Dock Back")) {
                    NavPageState.Mode = PageWindowMode.Docked;
                    NavPageState.WindowOpen = false;
                } else if(NavPageState.Mode == PageWindowMode.Windowed) {
                    NavigationPage.Render(avionicsComputer);
                }

                ImGui.End();

                if(!NavPageState.WindowOpen) {
                    NavPageState.Mode = PageWindowMode.Docked; // or Hidden if you want
                    NavPageState.WindowOpen = false;
                }
            }
        }
    }
}
