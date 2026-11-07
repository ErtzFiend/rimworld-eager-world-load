using System;
using HarmonyLib;
using Verse;

namespace EagerWorldLoad
{
    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class Patch_Game_LoadGame
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                if (EagerWorldLoadLogic.TryRegenerateWorldLayers())
                {
                    Log.Message("[EagerWorldLoad] Queued eager world layer regeneration during save load.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EagerWorldLoad] Failed to queue eager world layer regeneration: {ex}");
            }
        }
    }
}
