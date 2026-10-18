using System.Collections.Generic;
using Unity.Netcode;

namespace CBMod
{
    // Shared toggle state and the "is this allowed right now" checks.
    internal static class Features
    {
        public static bool GodMode;
        public static bool InfiniteStamina;
        public static bool InfiniteMoney;
        public static bool OneHitKill;
        public static bool CameraZoomOut;

        // Every feature that affects the chain or the lobby must pass this check.
        public static bool IsHost()
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening && nm.IsHost;
        }

        // Players controlled on this machine (PlayerManager.LocalPlayers) that we also own.
        public static List<Player> LocalPlayers()
        {
            var list = new List<Player>();
            if (!PlayerManager.InstanceExists) return list;
            var lp = PlayerManager.Instance.localPlayers;   // Il2Cpp List<Player> (the IList interface has no Count)
            if (lp == null) return list;
            for (int i = 0; i < lp.Count; i++)
            {
                var p = lp[i];
                if (p != null && p.IsOwner) list.Add(p);
            }
            return list;
        }
    }
}
