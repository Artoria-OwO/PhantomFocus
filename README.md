<div align="center">

<img src="assets/PhantomFocus.ico" width="128" height="128" alt="PhantomFocus icon"/>

# PhantomFocus

**Keep one window "focused" — even when it isn't.**

A tiny Windows tool that lets a chosen window (typically a game) believe it has the foreground while you freely switch to your browser, watch videos, or chat.

</div>

---

## Why this exists

Some games — *Forza Horizon 5* among them — pause, throttle, or stop processing input the moment they lose focus. That makes simple things impossible: you can't AFK-grind credits while watching a video on the second monitor, you can't tab out to read a guide, and Alt-Tab becomes a stuttering mess.

DisplayFusion Pro has a feature for this called **Window Focus**, but it's a $34 commercial product. PhantomFocus is a single-purpose, open-source replacement.

> This is **not** "always on top". A topmost window still loses input focus when you click another window. PhantomFocus operates at the message-loop level instead.

## Features

- **Two focus-keeping strategies**, switchable from the UI:
  - **Fake Focus** *(recommended for AFK)* — synthesizes activation messages (`WM_ACTIVATE` / `WM_ACTIVATEAPP` / `WM_NCACTIVATE` / `WM_SETFOCUS`) so the target's message loop never realises it lost focus. **You can use other apps normally.**
  - **Force Foreground** *(DisplayFusion-style)* — listens for foreground changes via `SetWinEventHook` and snaps the chosen window back. Good if you want a window glued to the front; bad for watching videos elsewhere.
- Clean window picker: lists only visible top-level windows with a title; filters out cloaked UWP shell ghosts and tool windows.
- No installer, no service, no admin rights. A single self-contained executable — the .NET runtime is bundled inside.
- Live activity log shows what's happening.

## Requirements

- Windows 10 (1809+) or Windows 11
- **Nothing else.** The published binary is self-contained — the .NET runtime is bundled inside the exe, so no separate install is required.

## Download

Grab the latest `PhantomFocus.exe` from the [Releases page](../../releases/latest). That single file is everything you need — no installer, no unzip, no runtime download.

## Usage

1. Launch your game and get to the screen where you want to AFK.
2. Run `PhantomFocus.exe`.
3. Pick the game's window from the list (use **Refresh** if it isn't there yet).
4. Choose a mode:
   - Use **Fake Focus** if you want to keep using your PC. This is the right choice for AFK farming.
   - Use **Force Foreground** if you just want a window kept on top and don't mind losing focus elsewhere.
5. Click **Start Keeping Focus** (or double-click the row).
6. Switch to whatever you like. Click **Stop** when you're done.

## Important notes & limitations

- **Fullscreen-exclusive games may ignore fake activation messages.** For Forza Horizon 5 specifically, set the display mode to **Borderless Window** (or windowed) — Fake Focus is most reliable there. In true exclusive fullscreen the game's swapchain often forces a real focus loss that no message trick can hide.
- **Anti-cheat:** PhantomFocus only sends user-mode window messages (`PostMessage`) and reads window metadata. It does not inject code, hook game functions, or modify game memory. That said, no third-party tool is risk-free with online competitive games — use at your own risk, and prefer it for single-player / co-op sessions.
- **`SetForegroundWindow` foreground lock:** Windows blocks arbitrary foreground takeover. Force Foreground mode works around this by attaching input threads and tapping the Alt key, the standard documented workaround. It will briefly steal focus from whatever you were doing — by design.
- **One target at a time.** Stop the current target before picking a new one.

## How it works

| Mode | Mechanism |
|------|-----------|
| Fake Focus | A 250 ms `System.Windows.Forms.Timer` posts `WM_ACTIVATEAPP(true)`, `WM_NCACTIVATE(true)`, `WM_ACTIVATE(WA_ACTIVE)`, and `WM_SETFOCUS` to the target HWND. The game's WndProc treats these as a normal activation cycle and keeps simulating, rendering audio, and processing AI/physics. The real Windows foreground belongs to whoever you actually clicked on. |
| Force Foreground | A `SetWinEventHook` on `EVENT_SYSTEM_FOREGROUND` fires whenever any window becomes the system foreground. If it isn't our target (and isn't from the same process), we run the standard `AttachThreadInput` + Alt-key tap + `SetForegroundWindow` dance to push the target back. |

The window list comes from `EnumWindows`, filtered against `IsWindowVisible`, `WS_EX_TOOLWINDOW`, and DWM's `DWMWA_CLOAKED` attribute (which excludes UWP ghost windows and windows on other virtual desktops).

## Building from source

```powershell
# Requires .NET 8 SDK
dotnet build -c Release

# Standalone single-file build (~70 MB, no .NET runtime needed on target machine)
dotnet publish -c Release -r win-x64 -o publish
# -> publish\PhantomFocus.exe
```

The csproj sets `SelfContained=true`, `PublishSingleFile=true`, `IncludeAllContentForSelfExtract=true`, and `EnableCompressionInSingleFile=true`, so a plain `dotnet publish` produces a single, fully self-contained executable.

## Cutting a release

Releases are produced by [`.github/workflows/release.yml`](.github/workflows/release.yml) on `windows-latest`. There are two ways to trigger it:

**Tag push (creates a real GitHub Release):**

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow builds a self-contained `PhantomFocus.exe`, attaches it to a new GitHub Release named after the tag, and auto-generates release notes from the commit history.

**Manual dispatch (artifact only, no Release):**

Go to the **Actions** tab → *release* workflow → **Run workflow**. Optionally pass a version label. The build runs and the exe is uploaded as a workflow Artifact (downloadable from the run page) without creating a Release entry — handy for testing the pipeline.

## Project layout

```
PhantomFocus/
├── Program.cs              # entry point
├── MainForm.cs             # window picker UI + mode selector
├── FocusKeeper.cs          # Fake Focus + Force Foreground strategies
├── WindowEnumerator.cs     # filtered EnumWindows
├── NativeMethods.cs        # P/Invoke surface (user32, kernel32, dwmapi)
├── app.manifest            # asInvoker, PerMonitorV2 DPI
└── assets/PhantomFocus.ico # multi-resolution icon (16–256)
```

## License

MIT.
