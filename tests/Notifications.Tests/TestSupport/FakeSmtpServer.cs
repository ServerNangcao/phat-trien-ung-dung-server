using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ticketing.Tests.Notifications.TestSupport;

/// <summary>
/// Minimal in-process SMTP server for exercising the real MailKit adapter. Speaks just
/// enough of the protocol (EHLO with AUTH PLAIN, MAIL/RCPT/DATA) and captures received
/// message payloads. Set TransientFailuresBeforeAccept to exercise the retry loop.
/// </summary>
public sealed class FakeSmtpServer : IDisposable
{
    private readonly TcpListener _listener;
    private Task? _acceptLoop;
    private readonly CancellationTokenSource _stopping = new();

    public int Port { get; }
    public List<string> ReceivedMessages { get; } = new();
    public List<string> Transcript { get; } = new();
    public int MailFromAttempts { get; private set; }
    public int TransientFailuresBeforeAccept { get; set; }

    public FakeSmtpServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public void Start()
    {
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_stopping.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        // The MailKit adapter opens a fresh connection per retry attempt, so the server
        // must keep accepting clients until it is disposed.
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            using (client)
            {
                await HandleClientAsync(client, cancellationToken);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            await ReplyAsync(stream, "220 fake.test ESMTP");

            var inData = false;
            var data = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                lock (Transcript) { Transcript.Add($"C: {line ?? "<null>"}"); }
                if (line is null)
                    break;

                if (inData)
                {
                    if (line == ".")
                    {
                        inData = false;
                        lock (ReceivedMessages) { ReceivedMessages.Add(data.ToString()); }
                        data.Clear();
                        await ReplyAsync(stream, "250 OK");
                    }
                    else
                    {
                        data.AppendLine(line);
                    }

                    continue;
                }

                var verb = line.Split(' ')[0].ToUpperInvariant();
                switch (verb)
                {
                    case "EHLO":
                    case "HELO":
                        await ReplyAsync(stream, "250-fake.test greets you");
                        await ReplyAsync(stream, "250-AUTH PLAIN");
                        await ReplyAsync(stream, "250 8BITMIME");
                        break;
                    case "DATA":
                        inData = true;
                        await ReplyAsync(stream, "354 end data with <CR><LF>.<CR><LF>");
                        break;
                    case "MAIL":
                        MailFromAttempts++;
                        if (MailFromAttempts <= TransientFailuresBeforeAccept)
                            await ReplyAsync(stream, "451 temporary failure, try again");
                        else
                            await ReplyAsync(stream, "250 OK");
                        break;
                    case "AUTH":
                        await ReplyAsync(stream, "235 2.7.0 accepted");
                        break;
                    case "QUIT":
                        await ReplyAsync(stream, "221 2.0.0 bye");
                        return;
                    default:
                        await ReplyAsync(stream, "250 OK");
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // listener stopped: normal shutdown
        }
        catch (SocketException)
        {
            // listener stopped: normal shutdown
        }
    }

    private static async Task ReplyAsync(NetworkStream stream, string response)
    {
        var bytes = Encoding.ASCII.GetBytes(response + "\r\n");
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    internal async Task ReplyLoggedAsync(NetworkStream stream, string response, List<string> transcript)
    {
        lock (transcript) { transcript.Add($"S: {response}"); }
        await ReplyAsync(stream, response);
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Stop();
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* shutdown race is fine */ }
        _stopping.Dispose();
    }
}
