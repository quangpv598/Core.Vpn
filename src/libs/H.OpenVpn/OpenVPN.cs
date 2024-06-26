﻿using H.OpenVpn.Utilities;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace H.OpenVpn;

public class HOpenVpn : BaseVPN
{
    #region Private properties

    private Process? Process { get; set; }

    #region Console

    private Channel<string> ConsoleOutput { get; } = Channel.CreateUnbounded<string>();
    private CancellationTokenSource? ConsoleCancellation { get; set; } = new CancellationTokenSource();

    #endregion

    #region Management Interface

    private TcpClientWrapper? TcpClientWrapper { get; set; }
    private StreamReader? StreamReader { get; set; }
    private StreamWriter? StreamWriter { get; set; }

    private Channel<string> ManagementOutput { get; } = Channel.CreateUnbounded<string>();
    private CancellationTokenSource? ManagementCancellation { get; set; } = new CancellationTokenSource();
    private TaskCompletionSource<bool> AuthenticationCompletion { get; } = new TaskCompletionSource<bool>();

    #endregion

    #endregion

    #region Events

    public event EventHandler<string?>? ConsoleLineReceived;
    public event EventHandler<string?>? ManagementLineReceived;

    public event EventHandler<string>? ConsoleLineSent;
    public event EventHandler<string>? ManagementLineSent;

    private void OnConsoleLineReceived(string? value)
    {
        ConsoleLineReceived?.Invoke(this, value);
    }

    private void OnManagementLineReceived(string? value)
    {
        ManagementLineReceived?.Invoke(this, value);
    }

    private void OnConsoleLineSent(string value)
    {
        ConsoleLineSent?.Invoke(this, value);
    }

    private void OnManagementLineSent(string value)
    {
        ManagementLineSent?.Invoke(this, value);
    }

    #endregion

    #region Methods

    public override async Task StartAsync(VPNConnectionInfo connectionInfo)
    {
        VpnType = LibVpnType.OpenVPN;

        if (connectionInfo?.OpenVPNServiceInfo == null)
        {
            throw new ArgumentNullException();
        }

        string? adapterName = connectionInfo.AdapterName;
        string? config = connectionInfo.ConfigContent;
        string? username = connectionInfo.OpenVPNServiceInfo.UserName;
        string? password = connectionInfo.OpenVPNServiceInfo.Password;

        ConfigPath = Path.GetTempFileName();
        File.WriteAllText(ConfigPath, config);

        var path = connectionInfo.OpenVPNServiceInfo.BinaryServicePath;
        var port = NetworkUtilities.GetFreeTcpPort();

        Process = Process.Start(new ProcessStartInfo(path,
            $"--config \"{ConfigPath}\" " +
            $"--management 127.0.0.1 {port} " +
            $"--dev-node \"{adapterName}\" " +
            $"--windows-driver wintun " +
            "--verb 3 ")
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            Verb = "runas"
        });

        TcpClientWrapper = new TcpClientWrapper(new TimeSpan(0, 0, 5));
        TcpClientWrapper.Connect(IPAddress.Loopback, port);

        var stream = TcpClientWrapper.TcpClient.GetStream();
        StreamReader = new StreamReader(stream);
        StreamWriter = new StreamWriter(stream);

        {
            var _ = ListenManagementAsync(username, password);
        }
        {
            var _ = ListenConsoleAsync();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ManagementCancellation?.Cancel();
            ConsoleCancellation?.Cancel();

            try
            {
                Process?.Kill();
            }
            catch (InvalidOperationException)
            {
                // ignored
            }

            TcpClientWrapper?.Dispose();
            TcpClientWrapper = null;
            Process?.Dispose();
            Process = null;
            ManagementCancellation?.Dispose();
            ManagementCancellation = null;
            ConsoleCancellation?.Dispose();
            ConsoleCancellation = null;

            try
            {
                if (ConfigPath != null && File.Exists(ConfigPath))
                {
                    File.Delete(ConfigPath);
                    ConfigPath = null;
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    public async Task SendSignalAsync(Signal signal, CancellationToken cancellationToken = default)
    {
        await WriteLineToManagementAsync($"signal {signal:G}", cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseManagementInterfaceAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineToManagementAsync("exit", cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> GetPidAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineToManagementAsync("pid", cancellationToken).ConfigureAwait(false);

        while (await ManagementOutput.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (ManagementOutput.Reader.TryRead(out var message))
            {
                var regExpPattern = new Regex(@".*pid=(\d+)", RegexOptions.Compiled);

                var match = regExpPattern.Match(message);
                if (match.Success)
                {
                    return long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                }

                return 0;
            }
        }

        return 0;
    }

    public async Task<ICollection<State>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineToManagementAsync("state all", cancellationToken).ConfigureAwait(false);

        var states = new List<State>();

        while (await ManagementOutput.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (ManagementOutput.Reader.TryRead(out var message))
            {
                if (message == "END")
                {
                    return states;
                }

                states.Add(State.Parse(message));
            }
        }

        return states;
    }

    public async Task<string> GetLogsAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineToManagementAsync("log all", cancellationToken).ConfigureAwait(false);

        var logs = "";

        while (await ManagementOutput.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (ManagementOutput.Reader.TryRead(out var message))
            {
                if (message == "END")
                {
                    return logs;
                }

                logs += message + Environment.NewLine;
            }
        }

        return logs;
    }

    public async Task SubscribeStateAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineToManagementAsync("state on", cancellationToken).ConfigureAwait(false);
    }

    public async Task SubscribeByteCountAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineToManagementAsync("bytecount 1", cancellationToken).ConfigureAwait(false);
    }

    public async Task SubscribeLogAsync(CancellationToken cancellationToken = default)
    {
        await WriteLineToManagementAsync("log on", cancellationToken).ConfigureAwait(false);
    }

    public async Task WaitAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        await AuthenticationCompletion.Task
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);
    }

    public void WaitForExit(TimeSpan timeout)
    {
        Process?.WaitForExit((int)timeout.TotalMilliseconds);
    }

    #endregion

    #region Private methods

    private async Task<string?> ReadLineFromConsoleAsync(CancellationToken cancellationToken = default)
    {
        Process = Process ?? throw new InvalidOperationException("Process is null");

        var line = await Process.StandardOutput
            .ReadLineAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        var message = line?.Trim('\r', '\n');
        OnConsoleLineReceived(message);

        return message;
    }

    private async Task<string?> ReadLineFromManagementAsync(CancellationToken cancellationToken = default)
    {
        StreamReader = StreamReader ?? throw new InvalidOperationException("StreamReader is null");

        var line = await StreamReader.ReadLineAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        var message = line?.Trim('\r', '\n');
        OnManagementLineReceived(message);

        return message;
    }

    private async Task WriteLineToConsoleAsync(string line, CancellationToken cancellationToken = default)
    {
        Process = Process ?? throw new InvalidOperationException("Process is null");

        await Process.StandardInput
            .WriteLineAsync(line)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);
        await Process.StandardInput
            .FlushAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        OnConsoleLineSent(line);
    }

    private async Task WriteLineToManagementAsync(string line, CancellationToken cancellationToken = default)
    {
        StreamWriter = StreamWriter ?? throw new InvalidOperationException("StreamWriter is null");

        await StreamWriter
            .WriteLineAsync(line)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);
        await StreamWriter
            .FlushAsync()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);

        OnManagementLineSent(line);
    }

    private async Task ListenConsoleAsync()
    {
        while (ConsoleCancellation != null && !ConsoleCancellation.IsCancellationRequested)
        {
            try
            {
                string? line;
                try
                {
                    line = await ReadLineFromConsoleAsync(ConsoleCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ConsoleCancellation?.Dispose();
                    break;
                }

                if (line == null)
                {
                    break;
                }

                if (line.Contains("link remote: [AF_INET]"))
                {
                    var ipIndex = line.LastIndexOf(']') + 1;
                    var portIndex = line.LastIndexOf(':') + 1;
                    RemoteIpAddress = line.Substring(ipIndex, portIndex - ipIndex - 1);
                    RemoteIpPort = line.Substring(portIndex);
                    continue;
                }

                await ConsoleOutput.Writer.WriteAsync(line).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        }
    }

    private async Task ListenManagementAsync(string? username, string? password)
    {
        while (ManagementCancellation != null && !ManagementCancellation.IsCancellationRequested)
        {
            try
            {
                string? line;
                try
                {
                    line = await ReadLineFromManagementAsync(ManagementCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ManagementCancellation?.Dispose();
                    break;
                }

                if (line == null)
                {
                    break;
                }

                if (line.StartsWith(">PASSWORD:Need 'Auth' username/password", StringComparison.Ordinal))
                {
                    await WriteLineToManagementAsync($"username \"Auth\" {username}").ConfigureAwait(false);
                    await ReadLineFromManagementAsync().ConfigureAwait(false);

                    await WriteLineToManagementAsync($"password \"Auth\" {password}").ConfigureAwait(false);
                    await ReadLineFromManagementAsync().ConfigureAwait(false);

                    AuthenticationCompletion.TrySetResult(true);
                    continue;
                }

                if (line.StartsWith(">PASSWORD:Auth-Token", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith(">INFO:", StringComparison.Ordinal))
                {
                    // ignore all INFO response
                    continue;
                }

                if (line.StartsWith(">BYTECOUNT:", StringComparison.Ordinal))
                {
                    // split the string without prefix ">BYTECOUNT:"
                    var nums = line.Substring(11).Split(',');

                    if (nums.Length > 1)
                    {
                        BytesInCount = long.Parse(nums[0], CultureInfo.InvariantCulture);
                        BytesOutCount = long.Parse(nums[1], CultureInfo.InvariantCulture);
                        OnBytesInOutCountChanged(new InOutBytes(BytesInCount, BytesOutCount));
                    }
                    continue;
                }

                if (line.StartsWith(">STATE:", StringComparison.Ordinal))
                {
                    var state = State.Parse(line.Substring(7));
                    switch (state.Name)
                    {
                        case "RECONNECTING":
                        case "WAIT":
                            VpnState = VpnState.Connecting;
                            break;

                        case "TCP_CONNECT":
                            VpnState = VpnState.Reconnecting;
                            break;

                        case "RESOLVE":
                        case "AUTH":
                        case "GET_CONFIG":
                            break;

                        case "ASSIGN_IP":
                            LocalInterfaceAddress = state.LocalIp;
                            break;

                        case "CONNECTED":
                            if (!string.IsNullOrWhiteSpace(state.RemoteIp))
                            {
                                RemoteIpAddress = state.RemoteIp;
                            }
                            VpnState = VpnState.Connected;
                            break;

                        case "EXITING":
                            VpnState = VpnState.Exiting;
                            break;
                    }

                    OnInternalStateObtained(state);
                    continue;
                }

                if (line.StartsWith(">FATAL:", StringComparison.Ordinal))
                {
                    // string without prefix ">FATAL:"
                    VpnState = VpnState.Failed;
                    OnLogObtained(line.Substring(5));
                    continue;
                }

                if (line.StartsWith(">LOG:", StringComparison.Ordinal))
                {
                    // string without prefix ">LOG:"
                    OnLogObtained(line.Substring(5));
                    continue;
                }

                await ManagementOutput.Writer.WriteAsync(line).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        }
    }

    #endregion
}
