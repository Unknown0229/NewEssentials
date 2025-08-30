using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewEssentials.API.Players;
using OpenMod.API.Users;
using OpenMod.Core.Users;
using OpenMod.Unturned.Users;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace NewEssentials.Players
{
    public class AutoRefuelService : IAutoRefuelService, IDisposable
    {
        private readonly IConfiguration m_Configuration;
        private readonly ILogger<AutoRefuelService> m_Logger;
        private readonly Dictionary<CSteamID, bool> m_AutoRefuelEnabled;
        private bool m_IsRunning;
        private UniTask m_MonitorTask;

        public AutoRefuelService(IConfiguration configuration, ILogger<AutoRefuelService> logger)
        {
            m_Configuration = configuration;
            m_Logger = logger;
            m_AutoRefuelEnabled = new Dictionary<CSteamID, bool>();
            m_IsRunning = false;
        }

        public void Start()
        {
            if (m_IsRunning) return;
            
            m_IsRunning = true;
            m_MonitorTask = MonitorVehiclesAsync();
            m_Logger.LogInformation("Auto-refuel service started");
        }

        public void Stop()
        {
            if (!m_IsRunning) return;
            
            m_IsRunning = false;
            m_Logger.LogInformation("Auto-refuel service stopped");
        }

        public void EnableForPlayer(CSteamID steamID)
        {
            m_AutoRefuelEnabled[steamID] = true;
            m_Logger.LogDebug($"Auto-refuel enabled for player {steamID}");
        }

        public void DisableForPlayer(CSteamID steamID)
        {
            m_AutoRefuelEnabled[steamID] = false;
            m_Logger.LogDebug($"Auto-refuel disabled for player {steamID}");
        }

        public bool IsEnabledForPlayer(CSteamID steamID)
        {
            return m_AutoRefuelEnabled.ContainsKey(steamID) && m_AutoRefuelEnabled[steamID];
        }

        private async UniTask MonitorVehiclesAsync()
        {
            while (m_IsRunning)
            {
                try
                {
                    await UniTask.SwitchToMainThread();
                    
                    var players = Provider.clients;
                    foreach (var client in players)
                    {
                        if (client?.player == null) continue;
                        
                        var steamID = client.playerID.steamID;
                        if (!IsEnabledForPlayer(steamID)) continue;

                        var vehicle = client.player.movement.getVehicle();
                        if (vehicle == null) continue;

                        // Check if vehicle needs refueling
                        if (ShouldRefuelVehicle(vehicle))
                        {
                            RefuelVehicle(vehicle);
                            m_Logger.LogDebug($"Auto-refueled vehicle for player {steamID}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_Logger.LogError(ex, "Error in auto-refuel monitor loop");
                }

                // Wait before next check
                await UniTask.Delay(GetCheckInterval());
            }
        }

        private bool ShouldRefuelVehicle(InteractableVehicle vehicle)
        {
            if (!vehicle.usesFuel || vehicle.isExploded) return false;
            
            var threshold = m_Configuration.GetValue<int>("autorefuel:threshold", 80);
            var currentPercentage = (vehicle.fuel / (float)vehicle.asset.fuel) * 100;
            
            return currentPercentage <= threshold;
        }

        private void RefuelVehicle(InteractableVehicle vehicle)
        {
            if (!vehicle.usesFuel || vehicle.fuel >= vehicle.asset.fuel || vehicle.isExploded) return;
            
            vehicle.fuel = vehicle.asset.fuel;
            VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
        }

        private int GetCheckInterval()
        {
            return m_Configuration.GetValue<int>("autorefuel:checkInterval", 5000); // Default 5 seconds
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
