# GnomeBraviaScreenSaver

This .NET application hooks into GNOME's Blank Screen feature and will send a request to turn on/off a Bravia TV using Simple IP Control rather than seeing the stupid "No signal" screen.

## Fedora Install/Setup

1. Install .NET 8 `sudo dnf install dotnet-sdk-8.0`.
2. Make sure that your TV is connected to your local network and note its IP Address.
3. Enable Simple IP Control in the Bravia menu on the TV i.e. `Network and internet` > `Home network setup` > `IP control` > `Simple IP control`. See https://pro-bravia.sony.net/develop/integrate/ssip/overview/index.html.
4. Clone this repo then `cd` into the directory `cd GnomeBraviaScreenSaver`.
5. Edit gnome-bravia-screensaver.config or create a gnome-bravia-screensaver.local.config and set the IP address of your TV.  You can get this from `Bravia` > `IPAddress`.
6. Build, publish and install, `dotnet publish`

The app will autostart on you next GNOME session.

## Uninstall

`dotnet build -t:Uninstall`

Note, I've only tested this on Fedora 42 using an Intel NUC and a Sony Bravia XR-55X90K.
