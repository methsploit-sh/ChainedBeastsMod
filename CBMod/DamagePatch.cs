using HarmonyLib;

namespace CBMod
{
    // One-hit kill: scales the damage of the local player's attacks against non-player targets.
    [HarmonyPatch(typeof(Player), nameof(Player.ModifyAttackDamage))]
    internal static class DamagePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, NetworkHealthObject target, ref float __result)
        {
            if (!Features.OneHitKill || !__instance.IsOwner) return;
            if (target == null || target.TryCast<Player>() != null) return;   // never boost damage to players
            __result *= Cfg.DamageMultiplier.Value;
        }
    }
}
