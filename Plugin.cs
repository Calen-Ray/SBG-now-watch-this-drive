using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using FMOD;
using FMODUnity;
using Mirror;
using UnityEngine;

namespace NowWatchThisDrive
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "sbg.nowwatchthisdrive";
        public const string ModName = "NowWatchThisDrive";
        public const string ModVersion = "0.3.0";

        private const string AudioFileName = "NowWatchThisDrive.wav";
        private const float AudioMinDistance = 1.5f;
        private const float AudioMaxDistance = 1000f;
        private const float AudioHeightOffset = 0.9f;

        internal static ManualLogSource Log;

        private static FMOD.Sound _sound;
        private static bool _soundReady;

        private void Awake()
        {
            Log = Logger;
            RegisterClientHandlers();
            RegisterServerHandlers();
            NetworkClient.OnConnectedEvent += RegisterClientHandlers;
            new Harmony(ModGuid).PatchAll();
            Log.LogInfo($"{ModName} v{ModVersion} loaded (sound loads in Start).");
        }

        // The game has Unity's native audio disabled ("Disable Unity Audio" project setting)
        // and routes everything through FMOD. We load one 3D sound and position each playback
        // channel at the golfer who triggered the perfect swing.
        private void Start()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Info.Location);
                string audioPath = Path.Combine(pluginDir, AudioFileName);
                if (!File.Exists(audioPath))
                {
                    Log.LogError($"Audio file missing: {audioPath}");
                    return;
                }

                var result = RuntimeManager.CoreSystem.createSound(
                    audioPath,
                    MODE.CREATESAMPLE | MODE._3D | MODE._3D_LINEARROLLOFF,
                    out _sound);

                if (result != RESULT.OK)
                {
                    Log.LogError($"FMOD createSound failed: {result}");
                    return;
                }

                _sound.getLength(out uint lenMs, TIMEUNIT.MS);
                _soundReady = true;
                Log.LogInfo($"FMOD sound loaded: {audioPath} ({lenMs} ms)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Start failed: {ex}");
            }
        }

        private static void RegisterClientHandlers()
        {
            NetworkClient.ReplaceHandler<DriveClipPlayMsg>(OnPlayMessage, false);
        }

        private static void RegisterServerHandlers()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            NetworkServer.ReplaceHandler<DriveClipTriggerMsg>(OnTriggerMessage);
        }

        internal static void PlayOverride(Vector3 worldPosition)
        {
            if (!_soundReady)
            {
                return;
            }

            try
            {
                var sys = RuntimeManager.CoreSystem;
                var playResult = sys.playSound(_sound, default(ChannelGroup), true, out Channel channel);
                if (playResult != RESULT.OK)
                {
                    Log.LogWarning($"playSound: {playResult}");
                    return;
                }

                var modeResult = channel.setMode(MODE._3D | MODE._3D_LINEARROLLOFF);
                if (modeResult != RESULT.OK)
                {
                    Log.LogWarning($"setMode: {modeResult}");
                }

                var rangeResult = channel.set3DMinMaxDistance(AudioMinDistance, AudioMaxDistance);
                if (rangeResult != RESULT.OK)
                {
                    Log.LogWarning($"set3DMinMaxDistance: {rangeResult}");
                }

                var pos = worldPosition.ToFMODVector();
                var vel = Vector3.zero.ToFMODVector();
                var attrResult = channel.set3DAttributes(ref pos, ref vel);
                if (attrResult != RESULT.OK)
                {
                    Log.LogWarning($"set3DAttributes: {attrResult}");
                }

                var unpauseResult = channel.setPaused(false);
                if (unpauseResult != RESULT.OK)
                {
                    Log.LogWarning($"setPaused(false): {unpauseResult}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"PlayOverride failed: {ex}");
            }
        }

        private static void OnTriggerMessage(NetworkConnectionToClient conn, DriveClipTriggerMsg _)
        {
            if (conn == null || conn.identity == null)
            {
                return;
            }

            NetworkServer.SendToAll(new DriveClipPlayMsg
            {
                emitterNetId = conn.identity.netId,
                position = GetEmitterPosition(conn.identity.transform.position)
            });
        }

        private static void OnPlayMessage(DriveClipPlayMsg msg)
        {
            if (!_soundReady)
            {
                return;
            }

            uint localNetId = NetworkClient.connection != null && NetworkClient.connection.identity != null
                ? NetworkClient.connection.identity.netId
                : 0u;
            if (msg.emitterNetId != 0 && msg.emitterNetId == localNetId)
            {
                return;
            }

            Vector3 position = msg.position;
            if (msg.emitterNetId != 0 &&
                NetworkClient.spawned.TryGetValue(msg.emitterNetId, out NetworkIdentity identity) &&
                identity != null)
            {
                position = GetEmitterPosition(identity.transform.position);
            }

            PlayOverride(position);
        }

        private static bool TryGetLocalEmitter(out uint emitterNetId, out Vector3 emitterPosition)
        {
            emitterNetId = 0u;
            emitterPosition = Vector3.zero;

            PlayerGolfer fallbackLocalGolfer = null;
            PlayerGolfer[] golfers = UnityEngine.Object.FindObjectsByType<PlayerGolfer>(FindObjectsSortMode.None);
            for (int i = 0; i < golfers.Length; i++)
            {
                PlayerGolfer golfer = golfers[i];
                if (golfer == null || !golfer.isLocalPlayer)
                {
                    continue;
                }

                fallbackLocalGolfer = fallbackLocalGolfer ?? golfer;
                if (!golfer.IsSwinging)
                {
                    continue;
                }

                emitterNetId = golfer.netId;
                emitterPosition = GetEmitterPosition(golfer.transform.position);
                return true;
            }

            if (NetworkClient.connection != null && NetworkClient.connection.identity != null)
            {
                emitterNetId = NetworkClient.connection.identity.netId;
                emitterPosition = GetEmitterPosition(NetworkClient.connection.identity.transform.position);
                return true;
            }

            if (fallbackLocalGolfer != null)
            {
                emitterNetId = fallbackLocalGolfer.netId;
                emitterPosition = GetEmitterPosition(fallbackLocalGolfer.transform.position);
                return true;
            }

            return false;
        }

        private static Vector3 GetEmitterPosition(Vector3 basePosition)
        {
            return basePosition + Vector3.up * AudioHeightOffset;
        }

        [HarmonyPatch(typeof(CourseManager), nameof(CourseManager.OnStartServer))]
        internal static class Patch_CourseManager_OnStartServer
        {
            private static void Postfix()
            {
                RegisterServerHandlers();
            }
        }

        [HarmonyPatch(typeof(CourseManager), nameof(CourseManager.PlayAnnouncerLineLocalOnly))]
        internal static class Patch_CourseManager_PlayAnnouncerLineLocalOnly
        {
            private static bool Prefix(AnnouncerLine line)
            {
                if (line != AnnouncerLine.NiceShot || !_soundReady)
                {
                    return true;
                }

                if (!TryGetLocalEmitter(out uint emitterNetId, out Vector3 emitterPosition))
                {
                    return true;
                }

                Log?.LogInfo($"Intercepting NiceShot -> NowWatchThisDrive from netId={emitterNetId}");
                PlayOverride(emitterPosition);

                if (NetworkClient.active)
                {
                    RegisterClientHandlers();
                    NetworkClient.Send(new DriveClipTriggerMsg());
                }

                return false;
            }
        }
    }
}
