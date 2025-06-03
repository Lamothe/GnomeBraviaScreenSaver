using System.Net.Sockets;
using ScreenSaver.DBus;
using Tmds.DBus.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

const int braviaPort = 20060;
const string braviaPowerOffCommand = "*SCPOWR0000000000000000";
const string braviaPowerOnCommand = "*SCPOWR0000000000000001";

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
var settings = config.GetRequiredSection("Bravia").Get<BraviaSettings>();

if (settings == null)
{
    throw new Exception("Failed to load settings.");
}

logger.LogInformation("Bravia TV IP address is {IPAddress}", settings.IPAddress);

// Connect to D-Bus.
using var connection = new Connection(Address.Session!);
await connection.ConnectAsync();
logger.LogInformation("Connected to session bus.");

// Connect to the GNOME ScreenSaver service.
var service = new ScreenSaverService(connection, "org.gnome.ScreenSaver");
var screenSaver = service.CreateScreenSaver("/org/gnome/ScreenSaver");

// Watch for events
await screenSaver.WatchActiveChangedAsync(async (ex, active) =>
{
    logger.LogInformation("Active changed to {active}", active);

    try
    {
        // Send command using Simple IP Control
        using var client = new TcpClient(settings.IPAddress, braviaPort);
        using var stream = client.GetStream();
        using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync(active ? braviaPowerOffCommand : braviaPowerOnCommand);
        writer.Close();
        stream.Close();
        client.Close();
    }
    catch (Exception exception)
    {
        ex = exception;
        logger.LogError("Exception: {exception}", ex.ToString());
    }
});

// Wait forever.
await Task.Delay(-1);


class BraviaSettings
{
    public required string IPAddress { get; set; }
}
