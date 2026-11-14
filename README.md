# jtimer

A countdown timer built with **Blazor WebAssembly** (.NET 8). Set hours, minutes, and
seconds (or pick a quick preset), start the countdown, and a chime plays when the time
is up. The project is rooted at `/Users/jonastasantos/DEV/jtimer`.

## Features

- Configurable countdown with hour/minute/second fields and quick presets.
- **Other** adds a custom preset (hours/minutes/seconds); hold a preset on touch or
  right-click it on desktop to remove it, with a 5-second undo.
- Presets are saved in `localStorage`, so your own durations survive a reload.
- Tapping a duration field selects its value, so typing replaces it immediately.
- Circular progress ring that empties as time runs out.
- Full-screen layout that fits the viewport without page scroll bars, including short and
  landscape screens.
- Bottom menu with three screens: **Configuration**, **Intervals**, and **Timer**, with an
  animated highlight that slides between them. Labels sit below each icon.
- Pause, resume, and reset while a countdown is running.
- Requests that the screen stay awake while the countdown runs on browsers that support the
  Screen Wake Lock API; releases the lock when paused, reset, or finished.
- The countdown keeps running while you open the Configuration screen; coming back shows
  the updated time instead of restarting.
- An **Intervals** workout timer runs 10 seconds of preparation, followed by four 50-second
  exercise rounds with 1-minute rests between rounds. Phase colors, a circular countdown,
  progress bar, round markers, and an up-next cue make transitions clear. It supports pause,
  resume, and reset, and continues while navigating to another screen.
- A synthesized three-note chime (Web Audio API, no audio files) plus a vibration on
  devices that support it.
- Tab title shows the remaining time and announces when the timer finishes.
- Screen color alerts near the end of a countdown: yellow for the last 10 seconds and a
  red flash for 2 seconds when time is up.
- Sound and screen color alerts can be enabled or disabled on the Configuration screen;
  sound also has a "Preview sound" button. Both settings are remembered.
- Theme color picker on Configuration (purple, ocean, teal, green, amber, rose). The choice
  applies immediately and is remembered across reloads.
- Installable Progressive Web App (PWA) with a standalone display and offline support for
  the published application shell and its static assets.
- Installable Progressive Web App (PWA) with a standalone display and offline support for
  the published application shell and its static assets.

> **Note:** This project was created with the .NET SDK installed on this machine (`net8.0`).
> Newer Blazor/.NET releases exist (for example .NET 10 LTS). To use the latest, install a
> newer .NET SDK and create/retarget the app accordingly.

## Prerequisites

- **.NET SDK**
  - Check installed SDKs/version:
    ```bash
    dotnet --list-sdks
    dotnet --version
    ```
  - Download/install the latest .NET SDK: https://dotnet.microsoft.com/download
  - Blazor docs: https://learn.microsoft.com/aspnet/core/blazor/

## Get started & run

From the project root:

```bash
cd /Users/jonastasantos/DEV/jtimer
dotnet restore   # optional
dotnet run
```

- The terminal prints local URLs (e.g. `http://localhost:5156`).
- Open the URL in your browser.
- Stop with `Ctrl+C`.

Live reload during development:

```bash
dotnet watch
```

Keep the browser tab open while a timer runs. Browsers throttle timers in background
tabs, so the countdown display and chime can lag if the tab is hidden for a long time.

### Access from another device on your network

The default `http` profile listens on `localhost` only. Use the included `lan` profile to
listen on every network interface:

```bash
dotnet run --launch-profile lan
```

Then open `http://<your-computer-LAN-IP>:5156` on the other device (for example
`http://192.168.68.94:5156`). Find the LAN IP on macOS with:

```bash
ipconfig getifaddr en0
```

Use `en1` instead if `en0` is not your active Wi-Fi or Ethernet interface. Keep both
devices on the same network, and allow incoming connections for `dotnet` if macOS asks.
The development certificate is only trusted on this machine, so use the HTTP URL on other
devices; HTTPS would show a certificate warning unless you install/trust the certificate
there too.

You can also expose the app without the profile:

```bash
dotnet run --urls "http://0.0.0.0:5156"
```

This is for local-network testing. For a longer-lived deployment, publish the static
output (`dotnet publish -c Release -o publish`) and serve `publish/wwwroot` from a web
server or static host.

## Project structure (key files)

- `jtimer.csproj` – project settings and package references (`net8.0`)
- `Program.cs` – app startup, root component registration, and shared service registration
- `App.razor` – router/root component
- `Pages/Home.razor` – the timer page (`/`) and its timer-screen state
- `Pages/Home.razor.css` – mobile-first scoped styles for the timer page, including color alerts
- `Pages/Intervals.razor` – interval workout timer screen and visual phase/round status
- `wwwroot/css/intervals.css` – phase colors, countdown ring, round progress, and mobile layout
- `Pages/Configuration.razor` – the settings page (`/configuration`)
- `Pages/Configuration.razor.css` – scoped styles for the settings page
- `Services/CountdownTimer.cs` – the singleton countdown that survives screen changes
- `Services/IntervalTimer.cs` – shared prepare/exercise/rest timer state and phase transitions
- `Services/TimerPreferences.cs` – persisted sound, color alert, and theme preferences
- `Services/AppThemes.cs` – the accent palettes offered by the theme picker
- `wwwroot/js/timer.js` – Web Audio chime, audio unlocking, and tab-title alert helpers
- `wwwroot/js/ui.js` – timer-screen helpers (preset storage and tap-to-select fields)
- `wwwroot/js/theme.js` – applies the selected accent palette to the document
- `wwwroot/css/app.css` – shared shell styles and the accent CSS variables for every theme
- `Layout/MainLayout.razor` – full-viewport app shell with the animated bottom menu
- `Layout/MainLayout.razor.css` – shell, three-position menu indicator, and nav item layout
- `wwwroot/index.html` – host page for the WebAssembly app
- `wwwroot/manifest.webmanifest` – install metadata, display mode, theme, and app icons
- `wwwroot/service-worker.js` – development service worker (doesn't cache app content)
- `wwwroot/service-worker.published.js` – published offline asset cache and navigation fallback
- `Properties/launchSettings.json` – local run profiles

## How the timer works

- Duration is stored as three integers (hours, minutes, seconds). Changing them (or a
  preset) updates the idle countdown immediately.
- Starting the timer sets a deadline (`DateTimeOffset.UtcNow + remaining`) and a loop
  refreshes the display every 200 ms. Because the remaining time is always recomputed
  from the deadline, the countdown does not drift.
- Pausing cancels the loop and keeps the exact remaining time; resume sets a new deadline.
- Finishing zeroes the display, updates the state to "Time's up", and calls the JS chime.
- While running, the app requests a screen wake lock where supported, releases it on pause,
  reset, finish, or disposal, and requests it again when the app becomes visible. iOS/iPadOS
  Home Screen apps require version 18.4 or later for Wake Lock support; iOS can still release
  the lock while the app is backgrounded. Wake Lock requires a secure context (HTTPS, or
  localhost); plain HTTP over a LAN IP address cannot keep the screen awake.
- The countdown lives in the singleton `CountdownTimer` service, not in the page. The
  Timer and Configuration screens both read that service, so navigating between them
  neither pauses nor resets a running countdown.
- With screen color alerts enabled, the timer theme turns yellow for the last 10 seconds
  and flashes red for 2 seconds when time is up. The Configuration toggle disables both.
- Audio is unlocked on the first Start click, as browsers only allow sound after a user
  gesture.
- Preferences are stored in `localStorage` under `jtimer.soundEnabled`,
  `jtimer.colorAlertsEnabled` (both default to enabled), and `jtimer.themeColor`
  (defaults to purple). They are read once at startup through `TimerPreferences`.
- Theme colors are CSS custom properties on `:root` (`--accent`, `--accent-strong`, and
  derived tints). A `data-theme` attribute selects the palette; `index.html` applies the
  saved value before Blazor starts, so a reload does not flash the default purple.
- Presets are stored as total seconds in `localStorage` under `jtimer.presets`. A press
  held for 600 ms (or a right-click) removes the preset; the toast restores it when the
  user taps Undo.

## Build & publish

```bash
dotnet build
dotnet publish -c Release -o publish
```

Static publish output for standalone WebAssembly: `publish/wwwroot/` (suitable for static
hosting such as **Azure Static Web Apps**).

The published app registers a service worker and caches the Blazor asset manifest's
integrity-verified static assets for offline startup. Service-worker caching is disabled in
development to keep local changes visible. Serve the published files over HTTPS (or from
`localhost`) for service workers and installation to work. The service worker keeps an
existing app version active until its open clients close, then removes old jtimer caches;
plan and test deployments with that lifecycle in mind. Offline support applies to the app
shell and static assets only; it doesn't make the timer continue running while the app is
closed or guarantee background audio.

## Use the latest Blazor version

1. Install the latest .NET SDK: https://dotnet.microsoft.com/download (e.g. .NET 10 LTS or newer)
2. Verify: `dotnet --list-sdks`
3. Create with that SDK (from an empty/new folder):
   ```bash
   dotnet new blazorwasm -n MyApp      # standalone WASM
   dotnet new blazor -n MyApp          # Blazor Web App model
   ```
4. To retarget this project: update `<TargetFramework>` in `jtimer.csproj` (e.g. `net10.0`)
   and package versions to match, then run `dotnet restore`.

## Project standards

This project follows industry-standard, human-readable coding practices. Agent/contributor
rules are defined in [`AGENTS.md`](AGENTS.md).

Standards applied:

- **Industry-standard .NET/Blazor conventions:** Idiomatic C# and Blazor component
  structure, aligned with common ASP.NET Core/Blazor guidance.
- **Naming:** .NET naming conventions (PascalCase for types, methods, properties, and
  components; camelCase for locals/parameters; descriptive, intention-revealing names).
- **Readability:** Focused components/methods, clear Razor markup and code layout, and
  comments only when intent is not obvious.
- **Simplicity:** Prefer the simplest maintainable solution; avoid unused code and
  unnecessary dependencies.
- **Consistency:** Match existing formatting and project organization when making changes.
- **Mobile-first layout:** Base CSS targets phones (single column, touch-sized controls); use
  `min-width` media queries to enhance wider screens. The shell fills the viewport exactly
  (full screen, no page scrolling); content sizes use `vh`/`vmin` clamps and short-viewport
  media queries so the timer always fits without scroll bars.
- **Scoped styles:** Component-specific CSS lives in a matching `.razor.css` file; shared
  styles stay in `wwwroot/css/app.css`.
- **Theme colors:** Use the accent custom properties (`--accent`, `--accent-strong`,
  `--accent-contrast`, and the derived `--accent-soft`/`--accent-faint`/`--accent-glow`
  tints) instead of hard-coded accent hex values, so every palette applies consistently.
  Because `NavLink` renders the anchor itself, style it through `::deep` in the scoped
  layout CSS.
- **Cross-screen state:** State that must outlive a page (the countdown) or be shared by
  several screens (preferences) lives in a singleton under `Services/`, registered in
  `Program.cs`. Pages subscribe to its change events and stay free of duplicated logic.
- **JavaScript interop:** Browser APIs (Web Audio, tab title) and other DOM helpers live in
  small ES modules under `wwwroot/js/` and are imported where needed; keep them free of
  UI logic.
- **PWA and offline caching:** Keep install metadata in the standards-based Web App Manifest.
  Register the worker with `updateViaCache: 'none'`, avoid caching during development, and
  in published builds cache only the generated Blazor assets manifest entries with their
  integrity hashes. Treat an offline deployment as a versioned snapshot; don't force
  immediate worker activation unless the app can safely handle mixed versions.
- **iOS safe areas:** Set the document and app-root background to the app shell color so
  the iPhone home-indicator safe area blends with the bottom navigation in standalone mode.
- **Documentation:** Setup, run, and project standards are documented in this README; new
  conventions/tools should be added here.

## Useful docs

- Blazor overview & tutorials: https://learn.microsoft.com/aspnet/core/blazor/
- Blazor Progressive Web Apps: https://learn.microsoft.com/aspnet/core/blazor/progressive-web-app?view=aspnetcore-8.0
- Web App Manifest: https://developer.mozilla.org/docs/Web/Manifest
- Service Worker API: https://developer.mozilla.org/docs/Web/API/Service_Worker_API
- Build your first Blazor app: https://dotnet.microsoft.com/learn/aspnet/blazor-tutorial/intro
- Hosting models/render modes: https://learn.microsoft.com/aspnet/core/blazor/components/render-modes
- Host & deploy Blazor WebAssembly: https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly
- Web Audio API: https://developer.mozilla.org/docs/Web/API/Web_Audio_API
- Screen Wake Lock API: https://developer.mozilla.org/docs/Web/API/Screen_Wake_Lock_API
- WebKit Safari 18.4 release notes (Wake Lock in iOS Home Screen apps): https://webkit.org/blog/16574/webkit-features-in-safari-18-4/
- .NET support policy: https://dotnet.microsoft.com/platform/support/policy/dotnet-core
- .NET/C# coding conventions: https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions
