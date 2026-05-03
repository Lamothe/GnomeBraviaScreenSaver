using System.Net.Sockets;
using Tmds.DBus.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Gtk;

const int braviaPort = 20060;
const string braviaPowerOffCommand = "*SCPOWR0000000000000000";
const string braviaPowerOnCommand = "*SCPOWR0000000000000001";
const string braviaSetInputHdmi1Command = "*SCINPT0000000100000001";
const string applicationName = "gnome-bravia-screensaver";

using var factory = LoggerFactory.Create(builder => builder.AddSystemdConsole());
var logger = factory.CreateLogger(applicationName);

var config = new ConfigurationBuilder()

    // When deployed/published
    .AddJsonFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".config/{applicationName}.config"))

    // For local development, gnome-bravia-screensaver.local.json is ignored by git
    .AddJsonFile($"{applicationName}.local.config", true)

    .AddEnvironmentVariables()
    .Build();

// Get values from the config given their key and their target type.
var settings = config.GetRequiredSection(nameof(BraviaSettings)).Get<BraviaSettings>()
    ?? throw new Exception("Failed to load settings.");

logger.LogInformation("Bravia TV IP address is {IPAddress}", settings.IPAddress);

foreach (var arg in args)
{
    if (arg == "on")
    {
        await SendBraviaPowerCommand(true);
        return 0;
    }
    else if (arg == "off")
    {
        await SendBraviaPowerCommand(false);
        return 0;
    }
    else if (arg == "--service")
    {
        // Connect to D-Bus.
        using var connection = new Connection(Address.Session!);
        await connection.ConnectAsync();
        logger.LogInformation("Connected to session bus.");

        await connection.AddMatchAsync(
            new MatchRule
            {
                Type = Tmds.DBus.Protocol.MessageType.Signal,
                Sender = "org.gnome.ScreenSaver",
                Path = "/org/gnome/ScreenSaver",
                Member = "ActiveChanged",
                Interface = "org.gnome.ScreenSaver"
            },
            (m, s) => m.GetBodyReader().ReadBool(),
            (ex, arg, rs, hs) => ((Func<Exception?, bool, Task>)hs!).Invoke(ex, arg),
            null,
            // This argument is the 'hs' (handler state). Since it uses 'async/await', it returns a Task.
            async (Exception? ex, bool active) => await OnActiveChanged(ex, active),
            true,
            ObserverFlags.None);

        // Wait forever.
        await Task.Delay(-1);

        // We'll never get here
        return 0;
    }
    else
    {
        logger.LogInformation("Usage: gnome-bravia-screensaver [on|off|--service|--help|-h]");
        logger.LogInformation("A simple application that listens (--service) for the ActiveChanged signal from org.gnome.ScreenSaver and sends power on/off commands to a Bravia TV using Simple IP Control.");
        logger.LogInformation("Launching the application without any parameters will open a GTK dialog allowing the user to manually control the TV.");
        logger.LogInformation("The `on` or `off` parameters can be used to control the TV from the command line.");
        logger.LogInformation("Configuration is loaded from ~/.config/gnome-bravia-screensaver.config or gnome-bravia-screensaver.local.config (for local development).");
        return -1;
    }
}

// ------------------------------------------------------------
// Launch the UI
// ------------------------------------------------------------

// Initialize a new GTK application
var application = Application.New("org.lamothe.gnome-bravia-screensaver", Gio.ApplicationFlags.FlagsNone);

// Declare the window outside the event so we can track its state
ApplicationWindow? mainWindow = null;

application.OnActivate += (sender, e) =>
{
    var app = (Application)sender;

    // If the window already exists, a second instance tried to launch.
    // Bring the existing window to the front and exit this event.
    if (mainWindow != null)
    {
        mainWindow.Present();
        return;
    }

    // Otherwise, create the main window for the first time
    mainWindow = ApplicationWindow.New(app);
    mainWindow.Title = "GNOME Bravia Screensaver";
    mainWindow.DefaultWidth = 240;
    mainWindow.DefaultHeight = 100;
    mainWindow.Resizable = false;

    // Create a horizontal box layout with 15px spacing
    var box = Box.New(Orientation.Horizontal, 15);
    box.MarginTop = 20;
    box.MarginBottom = 20;
    box.MarginStart = 20;
    box.MarginEnd = 20;
    box.Halign = Align.Center;
    box.Valign = Align.Center;

    // "Turn On" Button (Native GNOME Blue)
    var btnOn = Button.New();
    btnOn.Label = "Turn On";
    btnOn.WidthRequest = 90;
    btnOn.AddCssClass("suggested-action");
    btnOn.OnClicked += async (_, _) => await SendBraviaPowerCommand(true);

    // "Turn Off" Button (Native GNOME Red)
    var btnOff = Button.New();
    btnOff.Label = "Turn Off";
    btnOff.WidthRequest = 90;
    btnOff.AddCssClass("destructive-action");
    btnOff.OnClicked += async (_, _) => await SendBraviaPowerCommand(false);

    // Add buttons to the box, and the box to the window
    box.Append(btnOn);
    box.Append(btnOff);
    mainWindow.Child = box;

    mainWindow.Show();
};

// Run the application loop
return application.RunWithSynchronizationContext(null);

async Task OnActiveChanged(Exception? ex, bool active)
{
    logger.LogInformation("Active changed to {active}", active);

    try
    {
        // If screensaver is "active" then turn TV off.
        var newTvState = !active;

        // Only send the command if we're configured to.
        var sendCommand = (newTvState && settings.AutoTurnOn) || (!newTvState && settings.AutoTurnOff);

        if (sendCommand)
        {
            await SendBraviaPowerCommand(newTvState);
        }
    }
    catch (Exception exception)
    {
        ex = exception;
        logger.LogError("Exception: {exception}", ex.ToString());
    }
}

async Task SendBraviaPowerCommand(bool powerOn)
{
    if (powerOn)
    {
        await SendBraviaCommand(braviaPowerOnCommand, braviaSetInputHdmi1Command);
    }
    else
    {
        await SendBraviaCommand(braviaPowerOffCommand);
    }
}

async Task SendBraviaCommand(params string[] commands)
{
    using var client = new TcpClient(settings.IPAddress, braviaPort);
    using var stream = client.GetStream();
    using var writer = new StreamWriter(stream);
    foreach (var command in commands)
    {
        logger.LogInformation("Sending command: {command}", command);
        await writer.WriteLineAsync(command);
    }
    writer.Close();
    stream.Close();
    client.Close();
}

class BraviaSettings
{
    public required string IPAddress { get; set; }
    public bool AutoTurnOn { get; set; }
    public bool AutoTurnOff { get; set; }
}
