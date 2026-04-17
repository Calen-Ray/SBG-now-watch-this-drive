# NowWatchThisDrive

Replaces the announcer's "nice shot" audio with "now watch this drive" on a 100%-charged swing.

## What it patches

- **`CourseManager.PlayAnnouncerLineLocalOnly(AnnouncerLine)`** — Harmony prefix. When the line is
  `NiceShot`, plays our WAV through FMOD and returns `false` so the vanilla FMOD event is skipped.
  Any other announcer line falls through untouched.

## Audio path

The game has Unity's native audio disabled (see [docs/audio-is-fmod.md](../../docs/audio-is-fmod.md))
so `AudioClip.Create`, `AudioSource.Play`, and `UnityWebRequestMultimedia.GetAudioClip` are all
silent no-ops. We route playback through FMOD directly:

1. `FMODUnity.RuntimeManager.CoreSystem.createSound(path, MODE.DEFAULT | MODE.CREATESAMPLE, out Sound)`
   on plugin `Start` — FMOD decodes the WAV and keeps the PCM in memory.
2. `CoreSystem.playSound(sound, masterChannelGroup, false, out Channel _)` on each `NiceShot`
   intercept.

## Files shipped

| File | Purpose |
| --- | --- |
| `NowWatchThisDrive.dll` | The plugin |
| `NowWatchThisDrive.wav` | 16-bit stereo 44.1kHz PCM, loaded by FMOD at Start |
| `manifest.json`, `icon.png`, `README.md` | Thunderstore package metadata |

Deployed to `%APPDATA%\r2modmanPlus-local\SuperBattleGolf\profiles\Default\BepInEx\plugins\Calen-NowWatchThisDrive\`.

## Regenerate the audio

```
ffmpeg -y -i "..\..\Audio clip reference\now watch this drive .mp3" \
       -acodec pcm_s16le -ar 44100 -ac 2 Audio\NowWatchThisDrive.wav
```

## Test

1. Launch via r2modman.
2. `LogOutput.log` should show `FMOD sound loaded: …\NowWatchThisDrive.wav (2131 ms)`.
3. Perfect-power (100%) swing → `Intercepting NiceShot -> NowWatchThisDrive (FMOD)` and the clip plays.
