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
    public class AutoRepairItemsService : IAutoRepairItemsService, IDisposable
    {
        private readonly IConfiguration m_Configuration;
        private readonly ILogger<AutoRepairItemsService> m_Logger;
        private readonly Dictionary<CSteamID, bool> m_AutoRepairItemsEnabled;
        private bool m_IsRunning;
        private UniTask m_MonitorTask;

        public AutoRepairItemsService(IConfiguration configuration, ILogger<AutoRepairItemsService> logger)
        {
            m_Configuration = configuration;
            m_Logger = logger;
            m_AutoRepairItemsEnabled = new Dictionary<CSteamID, bool>();
            m_IsRunning = false;
        }

        public void Start()
        {
            if (m_IsRunning) return;
            
            m_IsRunning = true;
            m_MonitorTask = MonitorInventoriesAsync();
            m_Logger.LogInformation("Auto-repair items service started");
        }

        public void Stop()
        {
            if (!m_IsRunning) return;
            
            m_IsRunning = false;
            m_Logger.LogInformation("Auto-repair items service stopped");
        }

        public void EnableForPlayer(CSteamID steamID)
        {
            m_AutoRepairItemsEnabled[steamID] = true;
            m_Logger.LogDebug($"Auto-repair items enabled for player {steamID}");
        }

        public void DisableForPlayer(CSteamID steamID)
        {
            m_AutoRepairItemsEnabled[steamID] = false;
            m_Logger.LogDebug($"Auto-repair items disabled for player {steamID}");
        }

        public bool IsEnabledForPlayer(CSteamID steamID)
        {
            return m_AutoRepairItemsEnabled.ContainsKey(steamID) && m_AutoRepairItemsEnabled[steamID];
        }

        private async UniTask MonitorInventoriesAsync()
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

                        var inventory = client.player.inventory;
                        if (inventory == null) continue;

                        // Check if inventory needs repair
                        if (ShouldRepairInventory(inventory))
                        {
                            RepairInventory(inventory);
                            m_Logger.LogDebug($"Auto-repaired inventory for player {steamID}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_Logger.LogError(ex, "Error in auto-repair items monitor loop");
                }

                // Wait before next check
                await UniTask.Delay(GetCheckInterval());
            }
        }

        private bool ShouldRepairInventory(PlayerInventory inventory)
        {
            if (inventory.items == null) return false;
            
            var threshold = m_Configuration.GetValue<int>("autorepairitems:threshold", 90);
            
            foreach (var itemContainer in inventory.items)
            {
                if (itemContainer == null) continue;
                
                foreach (var itemJar in itemContainer.items)
                {
                    var item = itemJar.item;
                    if (item.quality < threshold)
                        return true;
                        
                    // Check barrel durability for guns
                    if (HasDurableBarrel(item, out ushort barrelID) && barrelID != 0)
                    {
                        if (item.state[16] < threshold)
                            return true;
                    }
                }
            }
            
            return false;
        }

        private void RepairInventory(PlayerInventory inventory)
        {
            if (inventory.items == null) return;
            
            foreach (var itemContainer in inventory.items)
            {
                if (itemContainer == null) continue;
                
                foreach (var itemJar in itemContainer.items)
                {
                    var item = itemJar.item;
                    
                    // Repair item quality
                    if (item.quality < 100)
                        inventory.sendUpdateQuality(itemContainer.page, itemJar.x, itemJar.y, 100);

                    // Repair barrel durability for guns
                    if (HasDurableBarrel(item, out ushort barrelID) && barrelID != 0)
                    {
                        if (item.state[16] < 100)
                        {
                            item.state[16] = 100;
                            inventory.sendUpdateInvState(itemContainer.page, itemJar.x, itemJar.y, item.state);
                        }
                    }
                }
            }
        }

        private bool HasDurableBarrel(Item item, out ushort barrelID)
        {
            barrelID = 0;
            var itemAsset = (ItemAsset)Assets.find(EAssetType.ITEM, item.id);

            if (itemAsset == null || itemAsset.type != EItemType.GUN || item.state == null || item.state.Length != 18)
                return false;

            var itemGunAsset = (ItemGunAsset)itemAsset;
            
            if (itemGunAsset.hasBarrel)
                barrelID = BitConverter.ToUInt16(item.state, 6);
            
            return itemGunAsset.hasBarrel;
        }

        private int GetCheckInterval()
        {
            return m_Configuration.GetValue<int>("autorepairitems:checkInterval", 5000); // Default 5 seconds
        }

        public void Dispose()
        {
            Stop();
        }
    }
}


