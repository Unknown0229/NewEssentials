using Steamworks;
using NewEssentials.API.Players;

namespace NewEssentials.Players
{
    /// <summary>
    /// Static manager for the auto-repair service to avoid dependency injection issues
    /// </summary>
    public static class AutoRepairServiceManager
    {
        private static IAutoRepairService s_Service;

        public static void SetService(IAutoRepairService service)
        {
            s_Service = service;
        }

        public static IAutoRepairService GetService()
        {
            return s_Service;
        }

        public static bool IsServiceAvailable()
        {
            return s_Service != null;
        }
    }
}


