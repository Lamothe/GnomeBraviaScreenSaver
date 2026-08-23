# GnomeBraviaScreenSaver

This .NET application hooks into GNOME's Blank Screen feature and sends power on/off commands to a Sony Bravia TV using Simple IP Control — no more annoying "No signal" screen.

## Features

- **Automatic control** — Listens on D-Bus for the `org.gnome.ScreenSaver` `ActiveChanged` signal and turns the TV off when the screen blanks, on when it unblanks.
- **GTK UI** — A simple two-button dialog ("Turn On" / "Turn Off") for manual control.
- **CLI** — Run `gnome-bravia-screensaver on` or `off` from the command line.
- **Autostart** — Installs a `.desktop` autostart entry so the service runs on every GNOME session.

## Fedora Install / Setup

1. Install .NET 10: `sudo dnf install dotnet-sdk-10.0`.
2. Make sure your TV is connected to your local network and note its IP address.
3. Enable Simple IP Control on the Bravia TV: `Settings` > `Network & Internet` > `Home Network Setup` > `IP Control` > `Simple IP Control`. See the [Sony Simple IP Control docs](https://pro-bravia.sony.net/develop/integrate/ssip/overview/index.html).
4. Clone this repo and `cd` into the directory:
   ```
   git clone <repo-url> && cd GnomeBraviaScreenSaver
   ```
5. Edit `gnome-bravia-screensaver.config` (or create `gnome-bravia-screensaver.local.config` for local development) and set your TV's IP address:
   ```json
   {
       "BraviaSettings": {
           "IPAddress": "192.168.1.x",
           "AutoTurnOn": true,
           "AutoTurnOff": true
       }
   }
   ```
   You can find your TV's IP address in the Bravia menu: `Settings` > `Network` > `Network Status` > `IP Address`.
6. Build, publish and install:
   ```
   dotnet publish
   ```
   This copies the binary to `~/.local/bin/gnome-bravia-screensaver`, installs the config to `~/.config/gnome-bravia-screensaver.config`, sets up the autostart entry, creates the Quick Settings extension bundle, and installs that bundle with `gnome-extensions`.

The background service will autostart on your next GNOME session.

The application reads its deployed configuration from
`~/.config/gnome-bravia-screensaver.config`. The repository-local
`gnome-bravia-screensaver.local.config` is ignored and is used only to seed or
update that installed configuration during development.

## Quick Settings

The installer installs the `Bravia Quick Toggle` GNOME Shell extension as a
GNOME extension bundle. After the first install, log out and back in so GNOME
Shell discovers it, then enable it:

```bash
gnome-extensions enable bravia-quick-toggle@lamothe
```

GNOME Shell on Wayland cannot be restarted with `Alt`+`F2`, `r`; a new session
is required.

When updating an already-enabled extension, disable and re-enable it so GNOME
Shell loads the new JavaScript:

```bash
gnome-extensions disable bravia-quick-toggle@lamothe
gnome-extensions enable bravia-quick-toggle@lamothe
```

The toggle queries the TV for its current power state and uses the installed
`gnome-bravia-screensaver` command for power changes. It targets GNOME Shell 50.

## CLI Usage

```
gnome-bravia-screensaver [on|off|status|--service|--help|-h]
```

| Argument | Description |
|----------|-------------|
| *(none)* | Opens the GTK control dialog |
| `on` | Turns the TV on |
| `off` | Turns the TV off |
| `status` | Prints `on` or `off` after querying the TV |
| `--service` | Runs the D-Bus listener (started automatically by autostart) |

## Uninstall

```
dotnet build -t:Uninstall
```

## Tested On

Fedora 42 with an Intel NUC and a Sony Bravia XR-55X90K.
