using System;
using Cysharp.Threading.Tasks;
using OpenMod.Core.Commands;
using Microsoft.Extensions.Localization;
using NewEssentials.Helpers;
using OpenMod.API.Commands;
using OpenMod.Unturned.Commands;
using OpenMod.Unturned.Users;
using SDG.Unturned;

namespace NewEssentials.Commands.Vehicles
{
	[Command("vehicle")]
	[CommandAlias("v")]
	[CommandDescription("Spawn a vehicle")]
	[CommandSyntax("<name>/<id>")]
	[CommandActor(typeof(UnturnedUser))]
	public class CVehicle : UnturnedCommand
	{
		private readonly IStringLocalizer m_StringLocalizer;

		public CVehicle(IStringLocalizer stringLocalizer, IServiceProvider serviceProvider) : base(serviceProvider)
		{
			m_StringLocalizer = stringLocalizer;
		}

		protected override async UniTask OnExecuteAsync()
		{
			if (Context.Parameters.Length != 1)
				throw new CommandWrongUsageException(Context);

			string vehicleSearchTerm = Context.Parameters[0];

			// If the user passed a numeric ID, spawn it directly and bypass asset resolution entirely
			if (ushort.TryParse(vehicleSearchTerm, out ushort requestedId))
			{
				await UniTask.SwitchToMainThread();
				var player = ((UnturnedUser)Context.Actor).Player.Player;
				Console.WriteLine($"[DEBUG] Spawning vehicle by ID directly: {requestedId} for {player.channel.owner.playerID.characterName}");
				if (VehicleTool.giveVehicle(player, requestedId))
				{
					await Context.Actor.PrintMessageAsync(m_StringLocalizer["vehicle:success", new { Vehicle = requestedId.ToString() }]);
					return;
				}
				Console.WriteLine($"[DEBUG] VehicleTool.giveVehicle failed for ID {requestedId}");
				throw new UserFriendlyException(m_StringLocalizer["vehicle:failure"]);
			}

			// Otherwise resolve by name using helper
			if (!UnturnedAssetHelper.GetVehicle(vehicleSearchTerm, out VehicleAsset vehicle))
				throw new UserFriendlyException(m_StringLocalizer["vehicle:invalid",
					new { Vehicle = vehicleSearchTerm }]);

			await UniTask.SwitchToMainThread();
			var resolvedPlayer = ((UnturnedUser)Context.Actor).Player.Player;
			Console.WriteLine($"[DEBUG] Player: {resolvedPlayer.channel.owner.playerID.characterName}");
			Console.WriteLine($"[DEBUG] Player position: {resolvedPlayer.transform.position}");
			Console.WriteLine($"[DEBUG] Vehicle asset ID: {vehicle.id}");
			Console.WriteLine($"[DEBUG] Vehicle name: {vehicle.vehicleName}");
			Console.WriteLine($"[DEBUG] Vehicle asset type: {vehicle.GetType()}");
			if (VehicleTool.giveVehicle(resolvedPlayer, vehicle.id))
				await Context.Actor.PrintMessageAsync(m_StringLocalizer["vehicle:success",
					new { Vehicle = vehicle.vehicleName }]);
			else
			{
				Console.WriteLine($"[DEBUG] VehicleTool.giveVehicle failed for player {resolvedPlayer.channel.owner.playerID.characterName}");
				Console.WriteLine($"[DEBUG] Vehicle ID: {vehicle.id}, Vehicle Name: {vehicle.vehicleName}");
				throw new UserFriendlyException(m_StringLocalizer["vehicle:failure"]);
			}
		}
	}
}
