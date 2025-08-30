using Steamworks;
using NewEssentials.API.Players;

namespace NewEssentials.Players
{
    /// <summary>
    /// Static manager for the auto-ammo service to avoid dependency injection issues
    /// </summary>
    public static class AutoAmmoServiceManager
    {
        private static IAutoAmmoService s_Service;

        public static void SetService(IAutoAmmoService service)
        {
            s_Service = service;
        }

        public static IAutoAmmoService GetService()
        {
            return s_Service;
        }

        public static bool IsServiceAvailable()
        {
            return s_Service != null;
        }
    }
}


