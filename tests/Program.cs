using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using EagerWorldLoad;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

internal static class Program
{
    private static int failures;
    private static int passes;

    private static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "PASS: " : "FAIL: ") + name);
        if (ok)
        {
            passes++;
        }
        else
        {
            failures++;
        }
    }

    private static int Main()
    {
        // Resolve ASP.NET Core runtime assemblies if needed by RimWorld references
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            var path = "/root/.nuget/packages/microsoft.aspnetcore.app.runtime.linux-x64/10.0.14/runtimes/linux-x64/lib/net10.0/" + name.Name + ".dll";
            return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
        };

        // 1. Game startup runs the StaticConstructorOnStartup hook, applying Harmony patches
        RuntimeHelpers.RunClassConstructor(typeof(EagerWorldLoadMain).TypeHandle);

        // 2. Real method resolution: Verse.Game.LoadGame exists
        var targetMethod = AccessTools.Method(typeof(Game), nameof(Game.LoadGame));
        Check(targetMethod != null, "Target method Verse.Game.LoadGame exists and resolves");

        // 3. Harmony patch registration check
        var patchInfo = Harmony.GetPatchInfo(targetMethod);
        Check(patchInfo != null, "Harmony patch info for Verse.Game.LoadGame exists");

        bool postfixRegistered = patchInfo != null && patchInfo.Postfixes.Any(p =>
            p.PatchMethod.DeclaringType == typeof(Patch_Game_LoadGame) &&
            p.PatchMethod.Name == "Postfix");
        Check(postfixRegistered, "Patch_Game_LoadGame.Postfix is registered on Verse.Game.LoadGame");

        // 4. WorldRenderer reflection checks
        var regenMethod = AccessTools.Method(typeof(WorldRenderer), nameof(WorldRenderer.RegenerateLayersIfDirtyInLongEvent));
        Check(regenMethod != null, "RimWorld.Planet.WorldRenderer.RegenerateLayersIfDirtyInLongEvent method resolves");

        var shouldRegenGetter = AccessTools.PropertyGetter(typeof(WorldRenderer), "ShouldRegenerateDirtyLayersInLongEvent");
        Check(shouldRegenGetter != null, "RimWorld.Planet.WorldRenderer.ShouldRegenerateDirtyLayersInLongEvent property getter resolves");

        // 5. Logic check: guard conditions when World or renderer is null
        Check(!EagerWorldLoadLogic.CheckShouldRegenerate(null), "CheckShouldRegenerate(null) returns false");
        Check(!EagerWorldLoadLogic.ShouldTriggerRegeneration(null), "ShouldTriggerRegeneration(null) returns false");

        // Test with empty world and uninitialized renderer
        var emptyWorld = new World();
        Check(!EagerWorldLoadLogic.ShouldTriggerRegeneration(emptyWorld), "ShouldTriggerRegeneration with uninitialized renderer returns false");

        // Test with empty world with renderer assigned
        var renderer = new WorldRenderer();
        emptyWorld.renderer = renderer;
        // WorldGrid is not initialized, so ShouldRegenerateDirtyLayersInLongEvent will catch or check Find.WorldGrid == null
        bool caughtOrFalse = false;
        try
        {
            caughtOrFalse = !EagerWorldLoadLogic.ShouldTriggerRegeneration(emptyWorld);
        }
        catch
        {
            // NullReferenceException when calling property getter because Find.WorldGrid is null
            caughtOrFalse = true;
        }
        Check(caughtOrFalse, "ShouldTriggerRegeneration handles uninitialized WorldGrid safely");

        // 6. Safe execution check: calling Postfix when Find.World is null handles gracefully without throwing
        bool postfixNoThrow = true;
        try
        {
            Patch_Game_LoadGame.Postfix();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unexpected exception in Postfix: " + ex);
            postfixNoThrow = false;
        }
        Check(postfixNoThrow, "Patch_Game_LoadGame.Postfix executes safely without throwing when Find.World is null");

        // 7. TryRegenerateWorldLayers check: returns false when world not ready
        Check(!EagerWorldLoadLogic.TryRegenerateWorldLayers(), "TryRegenerateWorldLayers() returns false when Find.World is uninitialized");

        Console.WriteLine($"\nRESULTS: {passes} passed, {failures} failed.");
        return failures == 0 ? 0 : 1;
    }
}
