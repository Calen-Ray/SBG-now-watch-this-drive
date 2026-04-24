using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using FMOD;
using FMODUnity;
using UnityEngine;

namespace NowWatchThisDrive
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "sbg.nowwatchthisdrive";
        public const string ModName = "NowWatchThisDrive";
        public const string ModVersion = "0.3.1";

        private const string AudioFileName = "NowWatchThisDrive.wav";
        private const float AudioMinDistance = 1.5f;
        private const float AudioMaxDistance = 1000f;
        private const float AudioHeightOffset = 0.9f;
        private const float DuplicateWindowSeconds = 0.12f;
        private const float DuplicateDistanceSquared = 0.25f;

        internal static ManualLogSource Log;

        private static FMOD.Sound _sound;
        private static bool _soundReady;
        private static float _lastPlayTime;
        private static Vector3 _lastPlayPosition;

        private void Awake()
        {
            Log = Logger;
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

        internal static void PlayOverride(Vector3 worldPosition)
        {
            if (!_soundReady)
            {
                return;
            }

            if (Time.unscaledTime - _lastPlayTime < DuplicateWindowSeconds &&
                (worldPosition - _lastPlayPosition).sqrMagnitude < DuplicateDistanceSquared)
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

                _lastPlayTime = Time.unscaledTime;
                _lastPlayPosition = worldPosition;
            }
            catch (Exception ex)
            {
                Log.LogError($"PlayOverride failed: {ex}");
            }
        }

        private static Vector3 GetEmitterPosition(Vector3 basePosition)
        {
            return basePosition + Vector3.up * AudioHeightOffset;
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

                Log?.LogInfo("Suppressing local NiceShot announcer; clip follows replicated SwingNiceShot VFX.");
                return false;
            }
        }

        [HarmonyPatch(typeof(VfxManager), "PlayPooledVfxLocalOnlyInternal",
            new[] { typeof(VfxType), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(uint), typeof(bool), typeof(float), typeof(Action<PoolableParticleSystem>) })]
        internal static class Patch_VfxManager_PlayPooledVfxLocalOnlyInternal
        {
            private static void Prefix(VfxType vfxType, Vector3 position)
            {
                if (vfxType != VfxType.SwingNiceShot || !_soundReady)
                {
                    return;
                }

                PlayOverride(GetEmitterPosition(position));
            }
        }
    }
}
