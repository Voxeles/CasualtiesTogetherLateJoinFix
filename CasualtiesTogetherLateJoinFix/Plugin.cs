using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using UnityEngine;

namespace CasualtiesTogetherLateJoinFix;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "cump.latejoin.maybefix";
    public const string ModName = "CasualtiesTogetherLateJoinFix";
    public const string ModVersion = "0.0.2";

    internal static new ManualLogSource Logger;
    
    private readonly Harmony _harmony = new(ModGuid);
    
    private float _timer = 0;
    private int _attempts = 0;
    private bool _loaded = false;
    
    private void Awake()
    {
        Logger = base.Logger;
        var mpModVersion = (string)AccessTools.Field(typeof(KrokoshaCasualtiesMP.Plugin), nameof(KrokoshaCasualtiesMP.Plugin.MOD_VERSION)).GetValue(null);
        if (!mpModVersion.Equals("4.0.1"))
        {
            Logger.LogFatal($"This mod {ModName} is intended ONLY for the v4.0.1 version of the multiplayer mod!!! Uninstall me ({ModName}) NOW!!!");
            return;
        }
        WorldgenPatches.OnWorldgenFinish += OnWorldgenFinish;
        NetPlayer.OnPlayerJoined += OnPlayerJoined;
        _harmony.PatchAll();
        Logger.LogInfo($"Plugin {ModName} is loaded!");
        _loaded = true;
    }

    private void OnDestroy()
    {
        if (!_loaded)
            return;
        WorldgenPatches.OnWorldgenFinish -= OnWorldgenFinish;
        NetPlayer.OnPlayerJoined -= OnPlayerJoined;
        _harmony?.UnpatchSelf();
    }

    private void OnWorldgenFinish()
    {
        _attempts = 0;
        _timer = 0;
    }

    private void OnPlayerJoined(NetPlayer _)
    {
        _attempts = 0;
        _timer = 0;
    }

    private void LateUpdate()
    {
        if (!_loaded)
            return;
        
        if (!Net.running || Net.is_host || !Util.IsWorldGenerated())
            return;
        
        if (NetPlayer.ClientIdToPlayerDict.Count == NetPlayer.BodyToPlayerDict.Count)
            return;
        
        _timer += Time.deltaTime;
        if (_timer < 10.0f)
            return;
        if (_attempts >= 10)
        {
            _timer = 0;
            return;
        }
        
        Logger.LogWarning("Attempting to fix player-body desync!");
        _attempts += 1;
        
        var bodies = FindObjectsByType<Body>(FindObjectsSortMode.None);
        var netBodies = FindObjectsByType<NetBody>(FindObjectsSortMode.None);
        Logger.LogInfo($"Players: {NetPlayer.ClientIdToPlayerDict.Count}, bodies: {NetPlayer.BodyToPlayerDict.Count}, Body objects: {bodies.Length}, NetBody objects: {netBodies.Length}, NetBody.all_instances: {NetBody.all_instances.Count}.");
        if (bodies.Length != netBodies.Length)
        {
            Logger.LogError($"Bodies ({bodies.Length}) and netBodies ({netBodies.Length}) doesn't match!");
            return;
        }
        foreach (var netBody in netBodies)
        {
            if (!netBody.player)
            {
                Logger.LogError($"{netBody.name} doesn't have a player!");
                continue;
            }

            bool didSomething = false;
            
            if (!NetBody.all_instances.Contains(netBody))
            {
                NetBody.all_instances.Add(netBody);
                Logger.LogInfo($"Added {netBody.player.playername}'s netBody to all_instances");
                didSomething = true;
            }
            
            if (!NetPlayer.BodyToPlayerDict.ContainsKey(netBody.body))
            {
                NetPlayer.BodyToPlayerDict.Add(netBody.body, netBody.player);
                Logger.LogInfo($"Added {netBody.player.playername}'s netBody to BodyToPlayerDict");
                didSomething = true;
            }
            
            if (netBody.player.body == null)
            {
                netBody.player.body = netBody.body;
                Logger.LogInfo($"Assigned {netBody.player.playername}'s netBody.body to their player.body");
                didSomething = true;
            }
            
            if (didSomething)
                Logger.LogMessage($"Adjusted player {netBody.player.playername}!");
        }

        _timer = 0;
    }

    public static void HandleMissingPlayerForNetBody(knetid clientId, NetBodySyncPacket packet)
    {
        if (Util.IsGeneratingWorld() || !Util.IsInWorld())
            return;
        
        if (!NetPlayer.TryGetPlayerFromClientId(clientId, out NetPlayer player))
        {
            Logger.LogWarning($"Got a sync packet for clientId {clientId}, but no such player with this ID exists!");
            return;
        }
        
        Logger.LogWarning($"Got a sync packet for client {clientId} ({player.playername}) with no body, attempting to make one.");

        var pb = NetBody.CreateNewPlayerCharacter(player);
        pb.netId = clientId;
        packet.Apply(pb);
        Logger.LogMessage($"Applied packet for {pb.playername}");
    }
}

