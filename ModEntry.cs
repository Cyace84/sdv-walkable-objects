using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace WalkableObjects
{
    public class ModEntry : Mod
    {
        public static IMonitor SMonitor;
        private static string sprinklerKey = "WalkableObjects/sprinkler";

        public override void Entry(IModHelper helper)
        {
            SMonitor = Monitor;
            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.PatchAll();
        }

        // Determines the corner of the tile under the cursor
        private static int GetMouseCorner()
        {
            var x = Game1.getMouseX() + Game1.viewport.X;
            var y = Game1.getMouseY() + Game1.viewport.Y;

            if (x % 64 < 32)
            {
                return y % 64 < 32 ? 0 : 2;
            }
            else
            {
                return y % 64 < 32 ? 1 : 3;
            }
        }

        // Gets the offset for drawing based on the corner
        private static Vector2 GetSprinklerCorner(int corner)
        {
            return corner switch
            {
                0 => new Vector2(-1, -1),
                1 => new Vector2(1, -1),
                2 => new Vector2(-1, 1),
                _ => new Vector2(1, 1)
            };
        }

        [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.placementAction))]
        public class Object_placementAction_Patch
        {
            public static bool Prefix(
                StardewValley.Object __instance,
                GameLocation location,
                int x,
                int y,
                Farmer who,
                ref bool __result
            )
            {
                if (!__instance.IsSprinkler())
                    return true;

                Vector2 tile = new Vector2(x / 64, y / 64);
                if (!location.terrainFeatures.TryGetValue(tile, out var tf) || tf is not HoeDirt)
                    return true;

                int corner = GetMouseCorner();

                // Check if the corner is already occupied
                if (tf.modData.ContainsKey(sprinklerKey + corner))
                {
                    __result = false;
                    return false;
                }

                // Save the sprinkler
                tf.modData[sprinklerKey + corner] = __instance.ParentSheetIndex.ToString();
                location.playSound("woodyStep");

                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(HoeDirt), nameof(HoeDirt.DrawOptimized))]
        [HarmonyPatch(new Type[] { typeof(SpriteBatch), typeof(SpriteBatch), typeof(SpriteBatch) })]
        public class HoeDirt_DrawOptimized_Patch
        {
            public static void Postfix(
                HoeDirt __instance,
                SpriteBatch dirt_batch,
                SpriteBatch fert_batch,
                SpriteBatch crop_batch
            )
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    if (
                        !__instance.modData.TryGetValue(sprinklerKey + corner, out string indexStr)
                        || !int.TryParse(indexStr, out int sprinklerIndex)
                    )
                        continue;

                    // Determine the position for drawing
                    var offset = GetSprinklerCorner(corner) * 32;
                    var position = Game1.GlobalToLocal(
                        __instance.Tile * 64 + new Vector2(32, 32) + offset
                    );

                    // Draw the sprinkler in the dirt_batch layer
                    var sourceRect = GameLocation.getSourceRectForObject(sprinklerIndex);
                    dirt_batch.Draw(
                        Game1.objectSpriteSheet,
                        position,
                        sourceRect,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        4f,
                        SpriteEffects.None,
                        (__instance.Tile.Y * 64 + 32) / 10000f
                    );
                }
            }
        }

        // Remove collision with sprinklers
        [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.isPassable))]
        public class Object_isPassable_Patch
        {
            public static void Postfix(StardewValley.Object __instance, ref bool __result)
            {
                if (__instance.IsSprinkler())
                {
                    __result = true;
                }
            }
        }
    }
}
