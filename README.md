# Audio Router

Standalone Windows desktop app for sending games to a headset and music to speakers at the same time. Controls each **program**, including Chrome as a whole, rather than individual browser tabs.

## Run

Download **AudioRouter-Windows.zip** from [Releases](https://github.com/DoctorCheese1/audio-router/releases/latest), extract it, then open **AudioRouter.exe**. No Node, Python, browser extension, or admin installation is required for ordinary apps.

On first run, click **Install audio engine**. This downloads NirSoft's free SoundVolumeView directly from its official HTTPS website and keeps its original help/license files. The engine runs locally in the background when you use an audio control. Internet access is needed for this first download and GitHub update checks; your audio is never uploaded.

1. Start your game and music program playing audio.
2. Click **Refresh** if they aren't listed.
3. Select the game, choose your headset, then **Apply output**.
4. Select Chrome, Spotify, or another music program, choose your speakers, then **Apply output**.
5. Adjust the volume slider and click **Set volume**. Use **Mute / unmute** as needed.

Windows saves per-program output preferences. Closing Audio Router does not undo routing. **Use Windows default** resets the selected program's output preference without changing its volume. The list shows observed audio-session endpoints; an app can need playback or a process restart before it moves. Refresh after changing devices.

## Updates

The app checks `DoctorCheese1/audio-router` for its latest published stable GitHub Release at startup. Use **Check updates** for a manual check. If a newer numeric tag (for example `v1.0.1`) exists, it shows an update message and **Open release** opens the download page. Close the app, extract the new ZIP to your chosen app folder, and reopen it. Settings and the audio engine stay in `%LOCALAPPDATA%\AudioRouter`.

This version detects updates; it does **not** silently replace or run downloaded code. GitHub failures, missing releases, private repositories, rate limiting, and offline checks show a status message. You can change the repository or turn off startup checks in the app.

## Windows requirements and limits

- Windows 10 April 2018 Update (1803) or newer; .NET Framework 4.7.2+ (included on recent Windows 10 builds).
- Apps appear once Windows creates an audio session; start playback first.
- Chrome processes are grouped by executable, so routing applies across Chrome tabs.
- Apps using exclusive audio, ASIO, or their own explicit device choice may require changing their own audio settings instead.
- Programs running with elevated privileges can be unavailable to a normal-user audio tool.
- Device removal or driver changes can invalidate an output preference; reconnect or select a new device.
- Executable names are grouped: separate installs with the same executable name share this UI's control scope.
- The initial engine download needs access to `www.nirsoft.net`. Updates use `api.github.com` and open `github.com`.

## Build without extra tools

Extract the full source ZIP, then double-click **Start.bat**. It builds `dist\AudioRouter.exe` with the .NET Framework compiler already included in Windows, then starts the app. **Build.bat** builds only. The console stays open if compilation fails. No Node, Python, SDK installer, or admin rights needed when the Windows Framework compiler is present.

## Release publishing

The included GitHub Actions workflow builds on Windows, runs ten core checks, packages a portable ZIP and SHA-256 checksum, then publishes a versioned release. It runs when relevant source files are pushed to `main` or when manually dispatched. Bump `Backend.Version`, `AssemblyVersion`, and `AssemblyFileVersion` together for each release. Existing releases are never overwritten.

## Validation

The self-test covers Windows argument quoting, repository validation, version comparison, output-device filtering, and grouping duplicate application sessions. Run `AudioRouter.exe --self-test`; it writes `self-test-passed.txt` next to the executable on success. GitHub Actions checks this marker and exit code. These checks do not replace physical-device tests on Windows 10.

## Data and third-party component

Settings contain the repository, startup-check preference, and remembered output IDs by executable name in `%LOCALAPPDATA%\AudioRouter\settings.json`. No microphone access, account tokens, audio recording, telemetry, or audio server. SoundVolumeView is separate freeware by Nir Sofer; see THIRD-PARTY.txt. This repository does not relicense or modify that utility.
