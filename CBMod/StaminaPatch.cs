using HarmonyLib;

namespace CBMod
{
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateStamina))]
    internal static class StaminaPatch
    {
        // After the game updates stamina, refill it for the local player.
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (!Features.InfiniteStamina || !__instance.IsOwner) return;
            __instance.stamina = __instance.StaminaMax;
        }
    }
}
