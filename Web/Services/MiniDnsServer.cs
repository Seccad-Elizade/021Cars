using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EnterpriseAeroStudio.Web.Services;

/// <summary>
/// Minimal DNS serveri — WiFi-dəki cihazlar <c>021cars.az</c> yazanda
/// avtomatik bu kompüterə yönlənsin deyə.
/// <para>
/// • Yalnız bizim domen adı üçün cavab verir (A qeydi → bu kompüterin LAN IP-si)<br/>
/// • Bütün digər sorğular yuxarı DNS-ə (məs. 8.8.8.8) olduğu kimi ötürülür<br/>
/// • Domen ALINMASA da işləyir — heç bir qeydiyyat lazım deyil
/// </para>
/// </summary>
public sealed class MiniDnsServer : BackgroundService
{
    private const int HeaderSize = 12;

    private readonly ILogger<MiniDnsServer> _logger;
    private readonly int _port;
    private readonly IPEndPoint _upstream;
    private readonly string[] _domains;
    private readonly IPAddress _answer;

    public MiniDnsServer(
        ILogger<MiniDnsServer> logger,
        int port,
        string upstream,
        IEnumerable<string> domains,
        IPAddress answer)
    {
        _logger = logger;
        _port = port;
        _upstream = new IPEndPoint(IPAddress.Parse(upstream), 53);
        _domains = domains
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().TrimEnd('.').ToLowerInvariant())
            .Distinct()
            .ToArray();
        _answer = answer;
    }

    /// <summary>Server həqiqətən işə düşdü?</summary>
    public bool IsRunning { get; private set; }

    /// <summary>İşə düşmə xətası (əgər varsa).</summary>
    public string? Error { get; private set; }

    // ---- Diagnostika üçün sayğaclar ----
    private long _queryCount;
    private long _answerCount;
    private long _forwardCount;

    /// <summary>Qəbul edilmiş ümumi DNS sorğu sayı.</summary>
    public long QueryCount => Interlocked.Read(ref _queryCount);

    /// <summary>"021cars.az" üçün verilmiş cavab sayı.</summary>
    public long AnswerCount => Interlocked.Read(ref _answerCount);

    /// <summary>Yuxarı DNS-ə ötürülmüş sorğu sayı.</summary>
    public long ForwardCount => Interlocked.Read(ref _forwardCount);

    /// <summary>Son sorğunun domen adı.</summary>
    public string LastQueryName { get; private set; } = string.Empty;

    /// <summary>Son sorğunun müştəri IP-si.</summary>
    public string LastQueryClient { get; private set; } = string.Empty;

    /// <summary>Son sorğunun vaxtı.</summary>
    public DateTime? LastQueryTime { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        UdpClient? server = null;

        try
        {
            server = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _logger.LogWarning(ex,
                "DNS serveri {Port} portunda başladıla bilmədi — " +
                "yalnız 'hosts' faylı ilə işləyəcək (bu kompüter).", _port);
            return;
        }

        IsRunning = true;
        _logger.LogInformation(
            "DNS serveri işə düşdü (:{Port}) → {Domains} = {Answer}",
            _port, string.Join(", ", _domains), _answer);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var received = await server.ReceiveAsync(stoppingToken);
                _ = Task.Run(() => HandleAsync(server, received, stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DNS sorğusu oxuna bilmədi.");
            }
        }

        server.Dispose();
        IsRunning = false;
    }

    private async Task HandleAsync(UdpClient server, UdpReceiveResult received, CancellationToken token)
    {
        var query = received.Buffer;

        try
        {
            if (query.Length < HeaderSize + 5)
            {
                return;
            }

            var name = ParseQuestionName(query);

            Interlocked.Increment(ref _queryCount);
            LastQueryName = name;
            LastQueryClient = received.RemoteEndPoint?.ToString() ?? "?";
            LastQueryTime = DateTime.Now;

            if (IsOurDomain(name))
            {
                var answer = BuildAnswer(query);
                if (answer.Length > 0)
                {
                    Interlocked.Increment(ref _answerCount);
                    await server.SendAsync(answer, answer.Length, received.RemoteEndPoint);
                    _logger.LogInformation("DNS: {Name} → {Ip}  (müştəri: {Client})",
                        name, _answer, LastQueryClient);
                    return;
                }
            }

            // Bizim domen deyil → yuxarı DNS-ə olduğu kimi ötür.
            Interlocked.Increment(ref _forwardCount);
            var forward = await ForwardAsync(query, token);
            if (forward is not null)
            {
                await server.SendAsync(forward, forward.Length, received.RemoteEndPoint);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DNS sorğusu emal edilə bilmədi.");
        }
    }

    /// <summary>Sorğudaki domen adı bizə aiddirmi?</summary>
    private bool IsOurDomain(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var domain in _domains)
        {
            if (name.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Sorğunun başlığından (header) domen adını oxuyur.</summary>
    private static string ParseQuestionName(byte[] packet)
    {
        var offset = HeaderSize;
        var name = new StringBuilder();

        while (offset < packet.Length)
        {
            var length = packet[offset++];

            if (length == 0)
            {
                break;
            }

            // Sıxılma göstəricisi (compression pointer).
            if ((length & 0xC0) == 0xC0)
            {
                break;
            }

            if (offset + length > packet.Length)
            {
                break;
            }

            if (name.Length > 0)
            {
                name.Append('.');
            }

            name.Append(Encoding.ASCII.GetString(packet, offset, length));
            offset += length;
        }

        return name.ToString();
    }

    /// <summary>Sual bölməsinin bitdiyi ofseti tapır.</summary>
    private static int FindQuestionEnd(byte[] packet)
    {
        var offset = HeaderSize;

        while (offset < packet.Length)
        {
            var length = packet[offset];

            if (length == 0)
            {
                offset++;
                break;
            }

            if ((length & 0xC0) == 0xC0)
            {
                offset += 2;
                break;
            }

            offset += length + 1;
        }

        // QTYPE (2) + QCLASS (2)
        return offset + 4 <= packet.Length ? offset + 4 : -1;
    }

    /// <summary>Bizim domen üçün A qeydi cavabı qurur.</summary>
    private byte[] BuildAnswer(byte[] query)
    {
        var questionEnd = FindQuestionEnd(query);
        if (questionEnd <= 0)
        {
            return Array.Empty<byte>();
        }

        var response = new byte[questionEnd + 16];

        // --- Başlıq (sual olduğu kimi köçürülür, sonra düzəliş edilir) ---
        Array.Copy(query, 0, response, 0, questionEnd);

        response[2] = 0x81;   // QR=1 (cavab), Opcode=0, RD=1
        response[3] = 0x80;   // RA=1, RCODE=0 (uğurlu)
        response[6] = 0x00;   // ANCOUNT = 1
        response[7] = 0x01;
        response[8] = 0x00;   // NSCOUNT = 0
        response[9] = 0x00;
        response[10] = 0x00;  // ARCOUNT = 0
        response[11] = 0x00;

        // --- Cavab bölməsi ---
        var p = questionEnd;

        response[p++] = 0xC0;   // Ad göstəricisi → sualdaki ada (offset 12)
        response[p++] = 0x0C;
        response[p++] = 0x00;   // TYPE = A
        response[p++] = 0x01;
        response[p++] = 0x00;   // CLASS = IN
        response[p++] = 0x01;
        response[p++] = 0x00;   // TTL = 60 saniyə
        response[p++] = 0x00;
        response[p++] = 0x00;
        response[p++] = 0x3C;
        response[p++] = 0x00;   // RDLENGTH = 4
        response[p++] = 0x04;

        var octets = _answer.GetAddressBytes();
        response[p++] = octets[0];
        response[p++] = octets[1];
        response[p++] = octets[2];
        response[p] = octets[3];

        return response;
    }

    /// <summary>Sorğunu yuxarı DNS serverinə ötürüb cavabı qaytarır.</summary>
    private async Task<byte[]?> ForwardAsync(byte[] query, CancellationToken token)
    {
        try
        {
            using var client = new UdpClient();
            client.Client.ReceiveTimeout = 4000;
            client.Client.SendTimeout = 4000;

            await client.SendAsync(query, query.Length, _upstream);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));

            var reply = await client.ReceiveAsync(timeout.Token);
            return reply.Buffer;
        }
        catch
        {
            return null;
        }
    }
}
