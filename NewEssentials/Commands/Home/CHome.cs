using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using NewEssentials.API.Players;
using NewEssentials.Extensions;
using NewEssentials.Models;
using NewEssentials.Options;
using OpenMod.API.Commands;
using OpenMod.API.Plugins;
using OpenMod.API.Users;
using OpenMod.Core.Commands;
using OpenMod.Unturned.Commands;
using OpenMod.Unturned.Users;
using System;
using System.Collections.Generic;
using InteractableBed = SDG.Unturned.InteractableBed;

namespace NewEssentials.Commands.Home
{
    [Command("home")]
    [CommandDescription("Teleport to your bed or one set with /home set")]
    [CommandSyntax("<name>")]
    [CommandActor(typeof(UnturnedUser))]
    public class CHome : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;
        private readonly IUserDataStore m_UserDataStore;
        private readonly IConfiguration m_Configuration;
        private readonly IPluginAccessor<NewEssentials> m_PluginAccessor;
        private readonly ITeleportService m_TeleportService;

        public CHome(IStringLocalizer stringLocalizer,
            IUserDataStore userDataStore,
            IConfiguration configuration,
            IPluginAccessor<NewEssentials> pluginAccessor,
            ITeleportService teleportService,
            IServiceProvider serviceProvider) :
            base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
            m_UserDataStore = userDataStore;
            m_Configuration = configuration;
            m_PluginAccessor = pluginAccessor;
            m_TeleportService = teleportService;
        }

        protected override async UniTask OnExecuteAsync()
        {
            if (Context.Parameters.Length > 1)
                throw new CommandWrongUsageException(Context);

            UnturnedUser uPlayer = (UnturnedUser)Context.Actor;
            bool bed = Context.Parameters.Length == 0;
            string homeName = Context.Parameters.Length == 1 ? Context.Parameters[0] : "";
            SerializableVector3 home = null;

            // If user is teleporting to a home, query the datastore for the Vector3 location
            if (!bed)
            {
                UserData userData = await m_UserDataStore.GetUserDataAsync(uPlayer.Id, uPlayer.Type);
                if (!userData.Data.ContainsKey("homes"))
                    throw new UserFriendlyException(m_StringLocalizer["home:no_home"]);

                var homes = (Dictionary<object, object>)userData.Data["homes"];

                if (!homes.ContainsKey(homeName))
                    throw new UserFriendlyException(m_StringLocalizer["home:invalid_home", new { Home = homeName }]);

                home = SerializableVector3.Deserialize(homes[homeName]);
                if (home == null)
                {
                    throw new UserFriendlyException(m_StringLocalizer["home:invalid_home", new { Home = homeName }]);
                }
            }

            // For bed teleportation, check if bed exists first, then show countdown
            if (bed)
            {
                // Search for beds in the world that belong to this player
                bool hasBed = false;
                try
                {
                    var beds = UnityEngine.Object.FindObjectsOfType<InteractableBed>();
                    var playerSteamID = uPlayer.Player.Player.channel.owner.playerID.steamID;
                    
                    foreach (var bedObj in beds)
                    {
                        if (bedObj.owner == playerSteamID)
                        {
                            hasBed = true;
                            break;
                        }
                    }
                }
                catch
                {
                    // If search fails, assume bed exists and let teleportToBed handle it
                    hasBed = true;
                }

                if (!hasBed)
                    throw new UserFriendlyException(m_StringLocalizer["home:no_bed"]);

                int delay = m_Configuration.GetValue<int>("teleportation:delay");
                await uPlayer.PrintMessageAsync(m_StringLocalizer["home:success", new { Home = "your bed", Time = delay }]);
                
                // Wait for the full delay from configuration
                await UniTask.Delay(delay * 1000); // Convert seconds to milliseconds
                
                await UniTask.SwitchToMainThread();
                if (!uPlayer.Player.Player.teleportToBed())
                    throw new UserFriendlyException(m_StringLocalizer["home:no_bed"]);
                
                await uPlayer.PrintMessageAsync(m_StringLocalizer["home:set", new { Home = "Bed teleportation" }]);
                return;
            }

            // Home teleportation - apply full delay and teleportation service
            int homeDelay = m_Configuration.GetValue<int>("teleportation:delay");
            bool cancelOnMove = m_Configuration.GetValue<bool>("teleportation:cancelOnMove");
            bool cancelOnDamage = m_Configuration.GetValue<bool>("teleportation:cancelOnDamage");

            await uPlayer.PrintMessageAsync(m_StringLocalizer["home:success", new { Home = homeName, Time = homeDelay }]);

            bool success = await m_TeleportService.TeleportAsync(uPlayer, new TeleportOptions(m_PluginAccessor.Instance, homeDelay, cancelOnMove, cancelOnDamage));

            if (!success)
                throw new UserFriendlyException(m_StringLocalizer["teleport:canceled"]);

            if (!await uPlayer.Player.Player.TeleportToLocationAsync(home.ToUnityVector3()))
                throw new UserFriendlyException(m_StringLocalizer["home:failure", new { Home = homeName }]);
        }
    }
}
