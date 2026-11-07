using HarmonyLib;
using Verse;

namespace EagerWorldLoad
{
    [StaticConstructorOnStartup]
    public static class EagerWorldLoadMain
    {
        public const string HarmonyId = "ertzfiend.eagerworldload";

        static EagerWorldLoadMain()
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            Log.Message("[EagerWorldLoad] Initialized. World layers will be regenerated eagerly during save load.");
        }
    }
}
