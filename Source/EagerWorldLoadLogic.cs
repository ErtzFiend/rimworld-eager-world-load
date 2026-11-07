using System;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace EagerWorldLoad
{
    public static class EagerWorldLoadLogic
    {
        private static readonly Func<WorldRenderer, bool> ShouldRegenerateDelegate;

        static EagerWorldLoadLogic()
        {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(WorldRenderer), "ShouldRegenerateDirtyLayersInLongEvent");
            if (getter != null)
            {
                ShouldRegenerateDelegate = AccessTools.MethodDelegate<Func<WorldRenderer, bool>>(getter);
            }
        }

        public static bool CheckShouldRegenerate(WorldRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            try
            {
                if (ShouldRegenerateDelegate != null)
                {
                    return ShouldRegenerateDelegate(renderer);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[EagerWorldLoad] Exception checking ShouldRegenerateDirtyLayersInLongEvent: {ex.Message}");
                return false;
            }

            return true;
        }

        public static bool ShouldTriggerRegeneration(World world)
        {
            if (world == null)
            {
                return false;
            }

            WorldRenderer renderer = world.renderer;
            if (renderer == null)
            {
                return false;
            }

            return CheckShouldRegenerate(renderer);
        }

        public static bool TryRegenerateWorldLayers()
        {
            World world = Find.World;
            if (world == null)
            {
                return false;
            }

            WorldRenderer renderer = world.renderer;
            if (renderer == null)
            {
                return false;
            }

            if (CheckShouldRegenerate(renderer))
            {
                return renderer.RegenerateLayersIfDirtyInLongEvent();
            }

            return false;
        }
    }
}
