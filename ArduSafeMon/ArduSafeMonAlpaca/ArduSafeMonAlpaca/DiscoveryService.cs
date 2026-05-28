using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ArduSafeMonAlpaca;

/// <summary>
/// Écoute sur UDP port 32227 et répond aux broadcasts de découverte Alpaca de NINA.
/// Quand NINA démarre, il envoie "alpacadiscovery1" en broadcast ;
/// on répond avec le port HTTP du serveur → NINA découvre automatiquement le driver.
/// </summary>
public sealed class DiscoveryService : BackgroundService
{
    private readonly AppSettings _settings;
    private readonly ILogger<DiscoveryService> _logger;

    public DiscoveryService(AppSettings settings, ILogger<DiscoveryService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        UdpClient? udp = null;
        try
        {
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 32227));

            _logger.LogInformation(
                "Alpaca discovery listening on UDP 32227 → HTTP port {port}",
                _settings.AlpacaPort);

            // Dédoublonnage : NINA envoie la requête depuis chaque interface réseau
            // (LAN, virtuel, loopback) → on ne répond qu'une seule fois par rafale (500 ms).
            DateTime lastReply = DateTime.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await udp.ReceiveAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "UDP receive error, retrying in 1 s");
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                string msg = Encoding.UTF8.GetString(result.Buffer);
                if (!msg.StartsWith("alpacadiscovery1", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Ignorer les requêtes dupliquées dans une fenêtre de 500 ms
                var now = DateTime.UtcNow;
                if ((now - lastReply).TotalMilliseconds < 500)
                {
                    _logger.LogDebug("Discovery: duplicate request from {ep}, ignored", result.RemoteEndPoint);
                    continue;
                }
                lastReply = now;

                string json = JsonSerializer.Serialize(
                    new { AlpacaPort = _settings.AlpacaPort });
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                try
                {
                    await udp.SendAsync(bytes, bytes.Length, result.RemoteEndPoint);
                    _logger.LogInformation(
                        "Discovery: replied to {ep}", result.RemoteEndPoint);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reply to discovery from {ep}",
                        result.RemoteEndPoint);
                }
            }
        }
        finally
        {
            udp?.Dispose();
        }
    }
}
