using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Localization;
using OpenMod.API.Commands;
using OpenMod.Core.Commands;
using OpenMod.Core.Permissions;
using OpenMod.Unturned.Commands;
using OpenMod.Unturned.Users;
using SDG.Unturned;
using System;
using NewEssentials.Players;
using NewEssentials.API.Players;
using Microsoft.Extensions.DependencyInjection;

namespace NewEssentials.Commands
{
    [Command("refuel")]
    [CommandDescription("Refuel the object you're looking at or current vehicle")]
    [CommandActor(typeof(UnturnedUser))]
    public class CRefuel : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;

        public CRefuel(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
        }

        protected override async UniTask OnExecuteAsync()
        {
            if (Context.Parameters.Length != 0)
                throw new CommandWrongUsageException(Context);

            UnturnedUser uPlayer = (UnturnedUser)Context.Actor;
            await UniTask.SwitchToMainThread();

            var currentVehicle = uPlayer.Player.Player.movement.getVehicle();
            if (currentVehicle != null)
            {
                if (!RefuelVehicle(currentVehicle))
                    throw new UserFriendlyException(m_StringLocalizer["refuel:vehicle:no_fuel"]);

                await PrintAsync(m_StringLocalizer["refuel:vehicle:success"]);
                return;
            }

            PlayerLook look = uPlayer.Player.Player.look;
            RaycastInfo raycast = DamageTool.raycast(new(look.aim.position, look.aim.forward), 8f, RayMasks.DAMAGE_SERVER);

            if (raycast.vehicle != null)
            {
                if (!RefuelVehicle(raycast.vehicle))
                    throw new UserFriendlyException(m_StringLocalizer["refuel:vehicle:no_fuel"]);

                await PrintAsync(m_StringLocalizer["refuel:vehicle:success"]);
                return;
            }

            if (raycast.transform == null)
            {
                throw new UserFriendlyException(m_StringLocalizer["refuel:none"]);
            }

            var interactable = raycast.transform.GetComponent<Interactable>();
            if (interactable != null)
            {
                if (interactable is InteractableGenerator generator)
                {
                    if (!generator.isRefillable)
                    {
                        throw new UserFriendlyException(m_StringLocalizer["refuel:object:no_fuel", new { Object = generator.name }]);
                    }

                    generator.askFill(generator.capacity);
                    BarricadeManager.sendFuel(raycast.transform, generator.fuel);

                    await PrintAsync(m_StringLocalizer["refuel:object:generator"]);
                    return;
                }
                else if (interactable is InteractableOil oil)
                {
                    if (!oil.isRefillable)
                    {
                        throw new UserFriendlyException(m_StringLocalizer["refuel:object:no_fuel", new { Object = oil.name }]);
                    }

                    oil.askFill(oil.capacity);
                    BarricadeManager.sendFuel(raycast.transform, oil.fuel);

                    await PrintAsync(m_StringLocalizer["refuel:object:oil"]);
                    return;
                }
                else if (interactable is InteractableTank { source: ETankSource.FUEL } tank)
                {
                    if (!tank.isRefillable)
                    {
                        throw new UserFriendlyException(m_StringLocalizer["refuel:object:no_fuel", new { Object = tank.name }]);
                    }

                    BarricadeManager.updateTank(raycast.transform, tank.capacity);

                    await PrintAsync(m_StringLocalizer["refuel:object:tank"]);
                    return;
                }
                else if (interactable is InteractableObjectResource { objectAsset: { interactability: EObjectInteractability.FUEL } } @object)
                {
                    if (!@object.isRefillable)
                    {
                        throw new UserFriendlyException(m_StringLocalizer["refuel:object:no_fuel", new { Object = @object.name }]);
                    }

                    ObjectManager.updateObjectResource(interactable.transform, @object.capacity, true);

                    // todo
                    await PrintAsync(m_StringLocalizer["refuel:object:object"]);
                    return;
                }
            }

            throw new UserFriendlyException(m_StringLocalizer["refuel:none"]);
        }

        private bool RefuelVehicle(InteractableVehicle vehicle)
        {
            // vehicle.isRefillable returns false if the vehicle is driven
            if (!vehicle.usesFuel || vehicle.fuel >= vehicle.asset.fuel || vehicle.isExploded)
                return false;

            vehicle.fuel = vehicle.asset.fuel;
            VehicleManager.sendVehicleFuel(vehicle, vehicle.fuel);
            return true;
        }
    }

    [Command("autorefuel")]
    [CommandDescription("Enable or disable automatic refueling for your vehicle (Admin Only)")]
    [CommandSyntax("[on|off]")]
    [CommandActor(typeof(UnturnedUser))]
    [RegisterCommandPermission("autorefuel", Description = "Allows use of auto-refuel system")]
    public class CAutoRefuel : UnturnedCommand
    {
        private readonly IStringLocalizer m_StringLocalizer;

        public CAutoRefuel(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
        {
            m_StringLocalizer = stringLocalizer;
        }

        protected override async UniTask OnExecuteAsync()
        {
            UnturnedUser uPlayer = (UnturnedUser)Context.Actor;
            var steamID = uPlayer.SteamId;

            // Get the auto-refuel service from the static manager
            var autoRefuelService = AutoRefuelServiceManager.GetService();
            if (autoRefuelService == null)
            {
                await PrintAsync("Auto-refuel service is not available. Please restart the server.");
                return;
            }

            if (Context.Parameters.Length == 0)
            {
                // Show current status
                bool isEnabled = autoRefuelService.IsEnabledForPlayer(steamID);
                string status = isEnabled ? "enabled" : "disabled";
                await PrintAsync($"Auto-refuel is currently {status}. Use '/autorefuel on' to enable or '/autorefuel off' to disable.");
                return;
            }

            string action = Context.Parameters[0].ToLower();
            
            if (action == "on" || action == "enable")
            {
                autoRefuelService.EnableForPlayer(steamID);
                await PrintAsync("Auto-refuel enabled! Your vehicle will now be automatically refueled when fuel drops below 80%.");
            }
            else if (action == "off" || action == "disable")
            {
                autoRefuelService.DisableForPlayer(steamID);
                await PrintAsync("Auto-refuel disabled. Your vehicle will no longer be automatically refueled.");
            }
            else
            {
                throw new CommandWrongUsageException(Context);
            }
        }
    }
}