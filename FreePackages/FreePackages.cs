﻿using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading.Tasks;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Steam;
using ArchiSteamFarm.Plugins.Interfaces;
using SteamKit2;
using System.Text.Json;
using ArchiSteamFarm.Helpers.Json;
using System.Reflection;

namespace FreePackages {
	[Export(typeof(IPlugin))]
	public sealed class FreePackages : IASF, IBotModules, ISteamPICSChanges, IBotSteamClient, IBotConnection, IBotCommand2 {
		public string Name => nameof(FreePackages);
		public Version Version => typeof(FreePackages).Assembly.GetName().Version ?? new Version("0");
		internal static GlobalCache? GlobalCache;

		public Task OnLoaded() {
			ASF.ArchiLogger.LogGenericInfo("Free Packages ASF Plugin by Citrinate");

			// ASFEnhanced Adapter https://github.com/chr233/ASFEnhanceAdapterDemoPlugin
			var flag = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			var handler = typeof(AdapterBridge).GetMethod(nameof(AdapterBridge.Response), flag);
			const string pluginId = nameof(FreePackages);
			const string cmdPrefix = "FREEPACKAGES";
			const string repoName = "Citrinate/FreePackages";
			AdapterBridge.InitAdapter(Name, pluginId, cmdPrefix, repoName, handler);

			return Task.CompletedTask;
		}

		public Task<string?> OnBotCommand(Bot bot, EAccess access, string message, string[] args, ulong steamID = 0) {
			return Task.FromResult(Commands.Response(bot, access, steamID, message, args));
		}

		public async Task OnASFInit(IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
			if (GlobalCache == null) {
				GlobalCache = await GlobalCache.CreateOrLoad().ConfigureAwait(false);
			}

			CardApps.Update();
		}

		public async Task OnBotInitModules(Bot bot, IReadOnlyDictionary<string, JsonElement>? additionalConfigProperties = null) {
			if (additionalConfigProperties == null) {
				return;
			}

			bool isEnabled = false;
			uint? packageLimit = null;
			bool pauseWhilePlaying = false;
			List<FilterConfig> filterConfigs = new();

			foreach (KeyValuePair<string, JsonElement> configProperty in additionalConfigProperties) {
				switch (configProperty.Key) {
					case "EnableFreePackages" when (configProperty.Value.ValueKind == JsonValueKind.True || configProperty.Value.ValueKind == JsonValueKind.False): {
						isEnabled = configProperty.Value.GetBoolean();
						bot.ArchiLogger.LogGenericInfo("Enable Free Packages : " + isEnabled.ToString());
						break;
					}
					
					case "PauseFreePackagesWhilePlaying" when (configProperty.Value.ValueKind == JsonValueKind.True || configProperty.Value.ValueKind == JsonValueKind.False): {
						pauseWhilePlaying = configProperty.Value.GetBoolean();
						bot.ArchiLogger.LogGenericInfo("Pause Free Packages While Playing : " + isEnabled.ToString());
						break;
					}

					case "FreePackagesPerHour" when configProperty.Value.ValueKind == JsonValueKind.Number: {
						packageLimit = configProperty.Value.GetUInt32();
						bot.ArchiLogger.LogGenericInfo("Free Packages Per Hour : " + packageLimit.ToString());
						break;
					}

					case "FreePackagesFilter": {
						FilterConfig? filter = configProperty.Value.ToJsonObject<FilterConfig>();
						if (filter != null) {
							bot.ArchiLogger.LogGenericInfo("Free Packages Filter : " + JsonSerializer.Serialize(filter));
							filterConfigs.Add(filter);
						}
						break;
					}
					
					case "FreePackagesFilters": {
						List<FilterConfig>? filters = configProperty.Value.ToJsonObject<List<FilterConfig>>();
						if (filters != null) {
							bot.ArchiLogger.LogGenericInfo("Free Packages Filters : " + JsonSerializer.Serialize(filters));
							filterConfigs.AddRange(filters);
						}
						break;
					}
				}
			}
			
			if (isEnabled) {
				await PackageHandler.AddHandler(bot, filterConfigs, packageLimit, pauseWhilePlaying).ConfigureAwait(false);
			}
		}

		public Task<uint> GetPreferredChangeNumberToStartFrom() {
			return Task.FromResult(GlobalCache?.LastChangeNumber ?? 0);
		}

		public Task OnPICSChanges(uint currentChangeNumber, IReadOnlyDictionary<uint, SteamApps.PICSChangesCallback.PICSChangeData> appChanges, IReadOnlyDictionary<uint, SteamApps.PICSChangesCallback.PICSChangeData> packageChanges) {
			PICSHandler.OnPICSChanges(currentChangeNumber, appChanges, packageChanges);
			
			return Task.CompletedTask;
		}

		public async Task OnPICSChangesRestart(uint currentChangeNumber) {
			await PICSHandler.OnPICSRestart(currentChangeNumber).ConfigureAwait(false);
		}

		public Task OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager) {
			callbackManager.Subscribe<SteamUser.AccountInfoCallback>(callback => OnAccountInfo(bot, callback));
			callbackManager.Subscribe<SteamApps.LicenseListCallback>(callback => OnLicenseList(bot, callback));

			return Task.CompletedTask;
		}

		public Task<IReadOnlyCollection<ClientMsgHandler>?> OnBotSteamHandlersInit(Bot bot) {
			return Task.FromResult((IReadOnlyCollection<ClientMsgHandler>?) null);
		}

		private static void OnAccountInfo(Bot bot, SteamUser.AccountInfoCallback callback) {
			PackageHandler.OnAccountInfo(bot, callback);
		}

		private static void OnLicenseList (Bot bot, SteamApps.LicenseListCallback callback) {
			PackageHandler.OnLicenseList(bot, callback);
		}

		public async Task OnBotLoggedOn(Bot bot) {
			await PackageHandler.OnBotLoggedOn(bot).ConfigureAwait(false);
		}

		public Task OnBotDisconnected(Bot bot, EResult reason) {
			return Task.FromResult(0);
		}
	}
}
