using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using ModAPI.Networking.Protocol;
using ModAPI.Networking.Sessions;

namespace ModAPI.Networking.Tests
{
    internal static class NetworkTestUtilities
    {
        public const int DefaultPumpTimeoutMilliseconds = 3000;
        private const string LocalhostHarnessHostArgument = "--localhost-harness-host";
        private const string LocalhostHarnessClientArgument = "--localhost-harness-client";
        private const string LocalhostHarnessApplicationId = "ModAPI.Networking.LocalhostHarness";
        private const ushort LocalhostHarnessPingMessageType = SessionMessageTypes.FirstApplicationMessageType + 500;
        private const ushort LocalhostHarnessPongMessageType = SessionMessageTypes.FirstApplicationMessageType + 501;

        public static NetworkConfig CreateLoopbackConfig()
        {
            NetworkConfig config = NetworkConfig.CreateDefault();
            config.Port = 0;
            config.ConnectionTimeoutMilliseconds = 500;
            config.HandshakeRetryMilliseconds = 50;
            config.HandshakeTimeoutMilliseconds = 1000;
            config.HeartbeatIntervalMilliseconds = 100;
            return config;
        }

        public static NetworkConfig CreateFastTimeoutConfig()
        {
            NetworkConfig config = CreateLoopbackConfig();
            config.ConnectionTimeoutMilliseconds = 150;
            config.HeartbeatIntervalMilliseconds = 1000;
            config.HandshakeTimeoutMilliseconds = 300;
            return config;
        }

        public static NetworkSessionOptions CreateOptions(string applicationId)
        {
            NetworkSessionOptions options = NetworkSessionOptions.CreateDefault();
            options.ApplicationId = applicationId;
            options.SessionId = "loopback";
            options.DisplayName = "test";
            options.MaxPeers = 4;
            return options;
        }

        public static bool IsLocalhostHarnessCommand(string[] args)
        {
            return args != null
                && args.Length > 0
                && (string.Equals(args[0], LocalhostHarnessHostArgument, StringComparison.Ordinal)
                    || string.Equals(args[0], LocalhostHarnessClientArgument, StringComparison.Ordinal));
        }

        public static int RunLocalhostHarnessProcess(string[] args)
        {
            try
            {
                if (args == null || args.Length < 4)
                {
                    Console.Error.WriteLine("Localhost harness requires role, port, ready path, and result path.");
                    return 90;
                }

                int port = int.Parse(args[1], CultureInfo.InvariantCulture);
                if (string.Equals(args[0], LocalhostHarnessHostArgument, StringComparison.Ordinal))
                    return RunLocalhostHarnessHost(port, args[2], args[3]);
                if (string.Equals(args[0], LocalhostHarnessClientArgument, StringComparison.Ordinal))
                    return RunLocalhostHarnessClient(port, args[2], args[3]);

                Console.Error.WriteLine("Unknown localhost harness role: " + args[0]);
                return 91;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 99;
            }
        }

        public static void RunTwoProcessLocalhostHarness()
        {
            int port = FindFreeUdpPort();
            string runId = Guid.NewGuid().ToString("N");
            string directory = Path.Combine(Path.GetTempPath(), "ModAPI.Networking.Tests");
            Directory.CreateDirectory(directory);
            string readyPath = Path.Combine(directory, runId + ".ready");
            string hostResultPath = Path.Combine(directory, runId + ".host.txt");
            string clientResultPath = Path.Combine(directory, runId + ".client.txt");
            DeleteIfExists(readyPath);
            DeleteIfExists(hostResultPath);
            DeleteIfExists(clientResultPath);

            Process host = null;
            Process client = null;
            try
            {
                string executable = Assembly.GetExecutingAssembly().Location;
                host = StartLocalhostHarnessProcess(executable, LocalhostHarnessHostArgument, port, readyPath, hostResultPath);
                WaitForFile(readyPath, 3000, "Two-process localhost host did not signal readiness.");

                client = StartLocalhostHarnessProcess(executable, LocalhostHarnessClientArgument, port, readyPath, clientResultPath);
                WaitForProcessSuccess(client, 6000, "client", clientResultPath);
                WaitForProcessSuccess(host, 6000, "host", hostResultPath);
            }
            finally
            {
                KillIfRunning(client);
                KillIfRunning(host);
                DeleteIfExists(readyPath);
                DeleteIfExists(hostResultPath);
                DeleteIfExists(clientResultPath);
            }
        }

        public static void Connect(NetworkSession host, NetworkSession client, string applicationId)
        {
            int hostConnected = 0;
            int clientConnected = 0;
            host.PeerConnected += delegate { hostConnected++; };
            client.PeerConnected += delegate { clientConnected++; };

            host.StartHost(CreateOptions(applicationId));
            client.Join(new IPEndPoint(IPAddress.Loopback, host.LocalEndPoint.Port), CreateOptions(applicationId));

            PumpUntil(new NetworkSession[] { host, client }, delegate
            {
                return hostConnected == 1
                    && clientConnected == 1
                    && client.State == NetworkSessionState.Connected;
            }, "Client and host did not complete the localhost handshake.");
        }

        public static void PumpOnce(params NetworkSession[] sessions)
        {
            for (int i = 0; i < sessions.Length; i++)
            {
                if (sessions[i] != null)
                    sessions[i].Update();
            }
        }

        public static void PumpUntil(NetworkSession session, Func<bool> condition, string failureMessage)
        {
            PumpUntil(new NetworkSession[] { session }, condition, failureMessage, DefaultPumpTimeoutMilliseconds);
        }

        public static void PumpUntil(NetworkSession first, NetworkSession second, Func<bool> condition, string failureMessage)
        {
            PumpUntil(new NetworkSession[] { first, second }, condition, failureMessage, DefaultPumpTimeoutMilliseconds);
        }

        public static void PumpUntil(NetworkSession[] sessions, Func<bool> condition, string failureMessage)
        {
            PumpUntil(sessions, condition, failureMessage, DefaultPumpTimeoutMilliseconds);
        }

        public static void PumpUntil(NetworkSession[] sessions, Func<bool> condition, string failureMessage, int timeoutMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                PumpOnce(sessions);
                if (condition())
                    return;
                Thread.Sleep(10);
            }

            PumpOnce(sessions);
            if (!condition())
                throw new InvalidOperationException(failureMessage);
        }

        public static byte[] CopyPayload(NetworkMessageReceivedEventArgs e, ushort expectedMessageType)
        {
            if (e.MessageType != expectedMessageType)
                return null;

            byte[] copy = new byte[e.Payload.Length];
            if (copy.Length > 0)
                Buffer.BlockCopy(e.Payload, 0, copy, 0, copy.Length);
            return copy;
        }

        public static byte[] ReceiveUdp(UdpClient udp, int timeoutMilliseconds, string failureMessage)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                if (udp.Client.Available > 0)
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    return udp.Receive(ref remote);
                }

                Thread.Sleep(10);
            }

            throw new InvalidOperationException(failureMessage);
        }

        private static int RunLocalhostHarnessHost(int port, string readyPath, string resultPath)
        {
            NetworkSession host = null;
            try
            {
                NetworkConfig config = CreateHarnessConfig(port);
                host = new NetworkSession(config);
                bool receivedPing = false;
                bool sentPong = false;
                host.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType != LocalhostHarnessPingMessageType)
                        return;
                    if (e.Payload.Length != 1 || e.Payload[0] != 42)
                        return;

                    receivedPing = true;
                    sentPong = host.SendToPeer(e.Peer.PeerId, LocalhostHarnessPongMessageType,
                        NetworkChannel.Reliable, new byte[] { 24 });
                };

                host.StartHost(CreateOptions(LocalhostHarnessApplicationId));
                File.WriteAllText(readyPath, "ready");

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(5000);
                while (DateTime.UtcNow < deadline)
                {
                    host.Update();
                    if (receivedPing && sentPong)
                    {
                        PumpFor(host, 500);
                        File.WriteAllText(resultPath, "Host received reliable ping and sent reliable pong.");
                        return 0;
                    }

                    Thread.Sleep(10);
                }

                File.WriteAllText(resultPath, "Host timed out waiting for reliable ping.");
                return 2;
            }
            finally
            {
                if (host != null)
                    host.Dispose();
            }
        }

        private static int RunLocalhostHarnessClient(int port, string readyPath, string resultPath)
        {
            NetworkSession client = null;
            try
            {
                NetworkConfig config = CreateHarnessConfig(0);
                client = new NetworkSession(config);
                bool sentPing = false;
                bool receivedPong = false;
                client.MessageReceived += delegate(object sender, NetworkMessageReceivedEventArgs e)
                {
                    if (e.MessageType == LocalhostHarnessPongMessageType
                        && e.Payload.Length == 1
                        && e.Payload[0] == 24)
                    {
                        receivedPong = true;
                    }
                };

                client.Join(new IPEndPoint(IPAddress.Loopback, port), CreateOptions(LocalhostHarnessApplicationId));
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(5000);
                while (DateTime.UtcNow < deadline)
                {
                    client.Update();
                    if (client.State == NetworkSessionState.Connected && !sentPing)
                    {
                        sentPing = client.SendToHost(LocalhostHarnessPingMessageType,
                            NetworkChannel.Reliable, new byte[] { 42 });
                    }

                    if (receivedPong)
                    {
                        PumpFor(client, 250);
                        File.WriteAllText(resultPath, "Client received reliable pong.");
                        return 0;
                    }

                    if (client.State == NetworkSessionState.Failed)
                        break;

                    Thread.Sleep(10);
                }

                File.WriteAllText(resultPath, "Client failed to complete reliable ping/pong. State=" + client.State
                    + " SentPing=" + sentPing + " ReceivedPong=" + receivedPong);
                return 3;
            }
            finally
            {
                if (client != null)
                    client.Dispose();
            }
        }

        private static NetworkConfig CreateHarnessConfig(int port)
        {
            NetworkConfig config = CreateLoopbackConfig();
            config.Port = port;
            config.FlushIntervalMilliseconds = 10;
            config.AckFlushMilliseconds = 10;
            config.ReliableResendMilliseconds = 50;
            config.ConnectionTimeoutMilliseconds = 2000;
            config.HandshakeTimeoutMilliseconds = 2000;
            config.HeartbeatIntervalMilliseconds = 100;
            return config;
        }

        private static void PumpFor(NetworkSession session, int milliseconds)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < until)
            {
                session.Update();
                Thread.Sleep(10);
            }
        }

        private static Process StartLocalhostHarnessProcess(
            string executable,
            string role,
            int port,
            string readyPath,
            string resultPath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = executable;
            startInfo.Arguments = role + " " + port.ToString(CultureInfo.InvariantCulture)
                + " " + QuoteArgument(readyPath)
                + " " + QuoteArgument(resultPath);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            return Process.Start(startInfo);
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void WaitForFile(string path, int timeoutMilliseconds, string failureMessage)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(path))
                    return;
                Thread.Sleep(10);
            }

            throw new InvalidOperationException(failureMessage);
        }

        private static void WaitForProcessSuccess(Process process, int timeoutMilliseconds, string role, string resultPath)
        {
            if (process == null)
                throw new InvalidOperationException("Two-process localhost " + role + " process did not start.");

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                KillIfRunning(process);
                throw new InvalidOperationException("Two-process localhost " + role + " process timed out. "
                    + ReadProcessDetails(process, resultPath));
            }

            string details = ReadProcessDetails(process, resultPath);
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Two-process localhost " + role + " process exited with code "
                    + process.ExitCode + ". " + details);
        }

        private static string ReadProcessDetails(Process process, string resultPath)
        {
            string output = string.Empty;
            string error = string.Empty;
            try { output = process.StandardOutput.ReadToEnd(); } catch { }
            try { error = process.StandardError.ReadToEnd(); } catch { }

            string result = string.Empty;
            try
            {
                if (File.Exists(resultPath))
                    result = File.ReadAllText(resultPath);
            }
            catch
            {
            }

            return "Result='" + result + "' StdOut='" + output + "' StdErr='" + error + "'";
        }

        private static void KillIfRunning(Process process)
        {
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
            }
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static int FindFreeUdpPort()
        {
            UdpClient udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            try
            {
                return ((IPEndPoint)udp.Client.LocalEndPoint).Port;
            }
            finally
            {
                udp.Close();
            }
        }
    }

    internal sealed class TestSessionSet : IDisposable
    {
        private readonly NetworkSession[] _sessions;

        public TestSessionSet(params NetworkSession[] sessions)
        {
            _sessions = sessions;
        }

        public NetworkSession[] Sessions
        {
            get { return _sessions; }
        }

        public void Dispose()
        {
            for (int i = _sessions.Length - 1; i >= 0; i--)
            {
                if (_sessions[i] != null)
                    _sessions[i].Dispose();
            }
        }
    }
}
