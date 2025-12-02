using HarmonyLib;
using KSA;

namespace Avionics {
    [HarmonyPatch]
    internal static class Patcher {
        private static Harmony? _harmony = new Harmony("Avionics");

        public static void Patch() {
            Console.WriteLine("Patching Avionics...");
            _harmony?.PatchAll(typeof(Patcher).Assembly);
        }

        public static void Unload() {
            _harmony?.UnpatchAll("Avionics");
            _harmony = null;
        }

        [HarmonyPatch(typeof(ModLibrary), nameof(ModLibrary.LoadAll))]
        [HarmonyPostfix]
        public static void AfterLoad() {
            Console.WriteLine("ModLibrary.LoadAll patched by Avionics.");
        }
    }
}