using System.Net.Sockets;
using Tmds.DBus.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
var settings = config.GetRequiredSection("Bravia").Get<BraviaSettings>()
    ?? throw new Exception("Failed to load settings.");

logger.LogInformation("Bravia TV IP address is {IPAddress}", settings.IPAddress);

foreach (var arg in args)
{
    if (arg == "on")
    {
        await SendBraviaPowerCommand(true);
        return;
    }
    else if (arg == "off")
    {
        await SendBraviaPowerCommand(false);
        return;
    }
    else
    {
        logger.LogInformation("Usage: gnome-bravia-screensaver [on|off|--help|-h]");
        logger.LogInformation("A simple application that listens for the ActiveChanged signal from org.gnome.ScreenSaver and sends power on/off commands to a Bravia TV using Simple IP Control.");
        logger.LogInformation("Configuration is loaded from ~/.config/gnome-bravia-screensaver.config or gnome-bravia-screensaver.local.config (for local development).");
        return;
    }
}

// Connect to D-Bus.
using var connection = new Connection(Address.Session!);
await connection.ConnectAsync();
logger.LogInformation("Connected to session bus.");

await connection.AddMatchAsync(
    new MatchRule
    {
        Type = MessageType.Signal,
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

async Task OnActiveChanged(Exception? ex, bool active)
{
    logger.LogInformation("Active changed to {active}", active);

    try
    {
        await SendBraviaPowerCommand(active);
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
}
