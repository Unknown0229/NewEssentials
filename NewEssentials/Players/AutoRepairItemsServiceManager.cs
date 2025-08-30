using Steamworks;
using NewEssentials.API.Players;

namespace NewEssentials.Players
{
    /// <summary>
    /// Static manager for the auto-repair items service to avoid dependency injection issues
    /// </summary>
    public static class AutoRepairItemsServiceManager
    {
        private static IAutoRepairItemsService s_Service;

        public static void SetService(IAutoRepairItemsService service)
        {
            s_Service = service;
        }

        public static IAutoRepairItemsService GetService()
        {
            return s_Service;
        }

        public static bool IsServiceAvailable()
        {
            return s_Service != null;
        }
    }
}


