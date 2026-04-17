# NowWatchThisDrive

Replaces the announcer's "nice shot" audio with the classic "now watch this drive" clip whenever
you pull off a 100%-charged swing.

## Install

Via [r2modman](https://thunderstore.io/c/super-battle-golf/) or Thunderstore Mod Manager —
search for **Cray-NowWatchThisDrive** and install.

Manual install: drop both files into
`<r2modman profile>/BepInEx/plugins/Cray-NowWatchThisDrive/`:
- `NowWatchThisDrive.dll`
- `NowWatchThisDrive.wav`

Requires [BepInExPack 5.4.2305](https://thunderstore.io/c/super-battle-golf/p/BepInEx/BepInExPack/).

## How it works

Super Battle Golf has Unity's native audio disabled and routes everything through FMOD. The
mod preloads the bundled WAV through `FMODUnity.RuntimeManager.CoreSystem.createSound`, then
Harmony-prefixes `CourseManager.PlayAnnouncerLineLocalOnly` — when the announcer would play
`NiceShot`, we play the override clip instead and skip the vanilla FMOD event. Every other
announcer line (hole-in-one, overtime, …) is untouched.

## Building from source

Requirements: .NET SDK 7.0+, a Super Battle Golf install (Steam default path is auto-detected).

```
git clone https://github.com/Calen-Ray/SBG-now-watch-this-drive.git
cd SBG-now-watch-this-drive
dotnet build -c Release
```

Override the game path or r2modman profile by copying `Developer.props.example` to
`Developer.props` and editing.

### Swapping the audio clip

Source WAV is `Audio/NowWatchThisDrive.wav` (16-bit PCM, 44.1 kHz stereo). Replace the file and
rebuild. If starting from an mp3/ogg, convert first:

```
ffmpeg -y -i source.mp3 -acodec pcm_s16le -ar 44100 -ac 2 Audio/NowWatchThisDrive.wav
```

### Packaging for Thunderstore

```
pwsh tools/package.ps1
```

Produces `artifacts/Cray-NowWatchThisDrive-<version>.zip` ready to upload.

## License

MIT — see [LICENSE](LICENSE).
