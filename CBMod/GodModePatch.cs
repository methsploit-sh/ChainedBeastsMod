using HarmonyLib;

namespace CBMod
{
    // Instead of cancelling the hit we zero it out: the original method keeps running,
    // so callers that expect a DamageEventData return value never see a null.
    [HarmonyPatch(typeof(NetworkHealthObject))]
    internal static class GodModePatch
    {
        // Only the host's own player is protected.
        private static bool IsHostsOwnPlayer(NetworkHealthObject o)
        {
            if (!Features.GodMode || !Features.IsHost()) return false;
            var p = o.TryCast<Player>();
            return p != null && p.IsOwner;
        }

        // Local damage path (damage the host takes directly).
        [HarmonyPrefix]
        [HarmonyPatch(nameof(NetworkHealthObject.TakeDamage))]
        private static void TakeDamagePrefix(NetworkHealthObject __instance, ref float damage, ref bool interrupt)
        {
            if (!IsHostsOwnPlayer(__instance)) return;
            damage = 0f;
            interrupt = false;
        }

        // Network path: damage reported over RPC is zeroed on the host side.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(NetworkHealthObject.TakeDamageRpc))]
        private static void TakeDamageRpcPrefix(NetworkHealthObject __instance, ref int damage, ref bool interrupt)
        {
            if (!IsHostsOwnPlayer(__instance)) return;
            damage = 0;
            interrupt = false;
        }
    }
}
