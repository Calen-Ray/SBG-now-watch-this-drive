using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using FMOD;
using FMODUnity;

namespace NowWatchThisDrive
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid    = "sbg.nowwatchthisdrive";
        public const string ModName    = "NowWatchThisDrive";
        public const string ModVersion = "0.2.0";
        private const string AudioFileName = "NowWatchThisDrive.wav";

        internal static ManualLogSource Log;
        private static FMOD.Sound _sound;
        private static bool _soundReady;

        private void Awake()
        {
            Log = Logger;
            new Harmony(ModGuid).PatchAll();
            Log.LogInfo($"{ModName} v{ModVersion} loaded (sound loads in Start).");
        }

        // The game has Unity's native audio disabled ("Disable Unity Audio" project setting)
        // and routes everything through FMOD. We use FMOD directly to play our clip.
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
                    MODE.DEFAULT | MODE.CREATESAMPLE, // decompress + keep in memory
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

        internal static void PlayOverride()
        {
            if (!_soundReady) return;
            try
            {
                var sys = RuntimeManager.CoreSystem;
                var r1 = sys.getMasterChannelGroup(out ChannelGroup group);
                if (r1 != RESULT.OK) { Log.LogWarning($"getMasterChannelGroup: {r1}"); return; }
                var r2 = sys.playSound(_sound, group, false, out Channel _);
                if (r2 != RESULT.OK) Log.LogWarning($"playSound: {r2}");
            }
            catch (Exception ex)
            {
                Log.LogError($"PlayOverride failed: {ex}");
            }
        }

        [HarmonyPatch(typeof(CourseManager), nameof(CourseManager.PlayAnnouncerLineLocalOnly))]
        internal static class Patch_CourseManager_PlayAnnouncerLineLocalOnly
        {
            private static bool Prefix(AnnouncerLine line)
            {
                if (line != AnnouncerLine.NiceShot) return true;
                if (!_soundReady) return true;
                Log?.LogInfo("Intercepting NiceShot -> NowWatchThisDrive (FMOD)");
                PlayOverride();
                return false;
            }
        }
    }
}
