using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Manager.Core.Services
{
    internal static class NexusOAuthCallbackHarness
    {
        private const string ValidRequest = "GET /callback?code=test-code&state=test-state HTTP/1.1";
        private const string TimeoutError = "Nexus sign-in timed out before the browser returned.";
        private static int _checks;

        internal static void Run()
        {
            TestResponse(ValidRequest + "\r\nHost: localhost\r\n\r\n", true, null);
            TestResponse("POST /callback?code=test-code&state=test-state HTTP/1.1\r\n\r\n",
                false, "The OAuth callback used an unsupported HTTP request.");
            TestResponse("GET /callback?code=test-code&state=wrong HTTP/1.1\r\n\r\n",
                false, "The OAuth callback state did not match the sign-in request.");
            TestResponse("GET /callback?error=access_denied&state=test-state HTTP/1.1\r\n\r\n",
                false, "Nexus authorization was not completed: access_denied");
            TestUnrecognizedPathThenCallback();
            TestResponse(ValidRequest + new string(' ', 8192 - ValidRequest.Length) + "\r\n\r\n", true, null);
            TestResponse(ValidRequest + "\r\nX:" + new string('a', 32766) + "\r\n\r\n", true, null);
            TestResponse(new string('a', 8193), false, "The OAuth callback request was too large.");
            TestResponse(ValidRequest + "\r\nX:" + new string('a', 32767),
                false, "The OAuth callback headers were too large.");
            TestResponse(ValidRequest + "\r\nX:" + new string('a', 16382) + "\r\nY:" + new string('b', 16383),
                false, "The OAuth callback headers were too large.");
            TestDeadline(null, null);
            TestDeadline("G", null);
            TestDeadline(ValidRequest + "\r\nX:", null);
            TestDeadline("G", "a");
            TestDeadline(ValidRequest + "\r\nX:", "a");
            TestDeadline(ValidRequest + "\r\n", "X: a\r\n");
            TestAcceptAndReadShareDeadline();
            TestStalledPeerThenCallback("G");
            TestStalledPeerThenCallback(ValidRequest + "\r\nX:");
            TestAbortedPeerThenCallback();
            TestPeerFailuresDoNotResetDeadline();
            TestClosedPeerThenCallback(string.Empty);
            TestClosedPeerThenCallback("GET");
            TestClosedPeerThenCallback(ValidRequest);
            TestClosedPeerThenCallback(ValidRequest + "\r\n");
            TestClosedPeerThenCallback(ValidRequest + "\r\nX: incomplete");
            TestClosedPeerThenCallback(ValidRequest + "\r\nX: complete\r\n");
            TestImmediateCloseThenCallback();
            Console.WriteLine("Nexus OAuth callback checks passed: " + _checks + ".");
        }

        private static void TestResponse(string request, bool success, string error)
        {
            using (var session = new CallbackSession(TimeSpan.FromSeconds(4)))
            using (TcpClient client = Connect())
            {
                Send(client, request);
                string response = ReadResponse(client);
                session.Finish(2000);
                Check(session.Result != null && session.Result.Success == success, "Unexpected callback result.");
                Check(session.Error == error, "Unexpected callback error: " + session.Error);
                Check(response.StartsWith(success ? "HTTP/1.1 200 OK" : "HTTP/1.1 400 Bad Request"),
                    "Unexpected callback HTTP status.");
                Check(response.Contains("Cache-Control: no-store"), "Callback response permits caching.");
                Check(!response.Contains("test-code") && !response.Contains("test-state"), "Response leaked callback secrets.");
                if (success)
                    Check(session.Result.AuthorizationCode == "test-code", "Callback code was not preserved.");
            }
        }

        private static void TestUnrecognizedPathThenCallback()
        {
            using (var session = new CallbackSession(TimeSpan.FromSeconds(4)))
            {
                using (TcpClient client = Connect())
                {
                    Send(client, "GET /callback/extra?code=test-code&state=test-state HTTP/1.1\r\n\r\n");
                    Check(ReadResponse(client).StartsWith("HTTP/1.1 400 Bad Request"), "A non-callback path was accepted.");
                }
                using (TcpClient client = Connect())
                {
                    Send(client, ValidRequest + "\r\n\r\n");
                    Check(ReadResponse(client).StartsWith("HTTP/1.1 200 OK"), "Listener stopped after an unrelated path.");
                }
                session.Finish(2000);
                Check(session.Result != null && session.Result.Success, "Callback after unrelated path failed.");
            }
        }

        private static void TestDeadline(string initial, string drip)
        {
            using (var session = new CallbackSession(TimeSpan.FromMilliseconds(350)))
            using (TcpClient client = initial == null ? null : Connect())
            {
                if (client != null)
                    Send(client, initial);
                int sent = 0;
                using (var timer = new Timer(delegate
                {
                    if (drip == null)
                        return;
                    try { Send(client, drip); Interlocked.Increment(ref sent); }
                    catch (IOException) { }
                    catch (ObjectDisposedException) { }
                }, null, 20, 20))
                {
                    session.Finish(1500);
                }
                Check(session.Result == null && session.Error == TimeoutError,
                    "A stalled or dripping callback did not report the overall timeout: " + session.Error);
                Check(session.Elapsed.ElapsedMilliseconds < 1500, "Socket activity extended the callback deadline.");
                if (drip != null)
                    Check(sent >= 2, "The adversarial client did not send repeated bytes before the deadline.");
            }
        }

        private static void TestAcceptAndReadShareDeadline()
        {
            using (var session = new CallbackSession(TimeSpan.FromMilliseconds(600)))
            using (var connected = new ManualResetEvent(false))
            {
                TcpClient client = null;
                Exception connectError = null;
                using (var timer = new Timer(delegate
                {
                    try { client = Connect(); Send(client, "G"); }
                    catch (Exception ex) { connectError = ex; }
                    finally { connected.Set(); }
                }, null, 400, Timeout.Infinite))
                {
                    Check(connected.WaitOne(2000, false), "Delayed callback did not connect.");
                    try
                    {
                        Check(connectError == null, "Delayed callback connection failed: " + connectError);
                        session.Finish(450);
                        Check(session.Result == null && session.Error == TimeoutError,
                            "Accept and request reads did not share the callback deadline.");
                        Check(session.Elapsed.ElapsedMilliseconds < 900, "Accept time was added to the read deadline.");
                    }
                    finally
                    {
                        if (client != null)
                            client.Close();
                    }
                }
            }
        }

        private static void TestStalledPeerThenCallback(string partialRequest)
        {
            using (var session = new CallbackSession(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(100)))
            {
                using (TcpClient peer = Connect())
                {
                    Send(peer, partialRequest);
                    Check(peer.GetStream().ReadByte() == -1, "The stalled peer was not closed after its inactivity timeout.");
                }
                using (TcpClient client = Connect())
                {
                    Send(client, ValidRequest + "\r\n\r\n");
                    Check(ReadResponse(client).StartsWith("HTTP/1.1 200 OK"), "A stalled peer prevented the valid callback response.");
                }
                session.Finish(1000);
                Check(session.Result != null && session.Result.Success && session.Error == null,
                    "A stalled peer terminated the OAuth attempt: " + session.Error);
            }
        }

        private static void TestAbortedPeerThenCallback()
        {
            using (var session = new CallbackSession(TimeSpan.FromSeconds(3)))
            {
                for (int i = 0; i < 8; i++)
                {
                    using (TcpClient peer = Connect())
                    {
                        peer.LingerState = new LingerOption(true, 0);
                        Send(peer, "GET /unrelated HTTP/1.1\r\n\r\n");
                    }
                }
                using (TcpClient client = Connect())
                {
                    Send(client, ValidRequest + "\r\n\r\n");
                    Check(ReadResponse(client).StartsWith("HTTP/1.1 200 OK"), "An aborted peer prevented the valid callback response.");
                }
                session.Finish(1000);
                Check(session.Result != null && session.Result.Success && session.Error == null,
                    "A peer unable to receive a response terminated the OAuth attempt: " + session.Error);
            }
        }

        private static void TestPeerFailuresDoNotResetDeadline()
        {
            using (var session = new CallbackSession(TimeSpan.FromMilliseconds(450), TimeSpan.FromMilliseconds(75)))
            {
                for (int i = 0; i < 3; i++)
                {
                    using (TcpClient peer = Connect())
                    {
                        Send(peer, "G");
                        Check(peer.GetStream().ReadByte() == -1, "An inactive peer remained open.");
                    }
                }
                session.Finish(400);
                Check(session.Result == null && session.Error == TimeoutError,
                    "Recovering from peer failures changed the overall timeout.");
                Check(session.Elapsed.ElapsedMilliseconds < 650, "Peer failures restarted the overall deadline.");
            }
        }

        private static void TestClosedPeerThenCallback(string partialRequest)
        {
            using (var session = new CallbackSession(TimeSpan.FromSeconds(2)))
            {
                using (TcpClient peer = Connect())
                {
                    Send(peer, partialRequest);
                    NetworkStream stream = peer.GetStream();
                    peer.Client.Shutdown(SocketShutdown.Send);
                    Check(stream.ReadByte() == -1, "An incomplete request received an OAuth response.");
                }
                using (TcpClient client = Connect())
                {
                    Send(client, ValidRequest + "\r\n\r\n");
                    Check(ReadResponse(client).StartsWith("HTTP/1.1 200 OK"), "A clean EOF prevented the valid callback response.");
                }
                session.Finish(1000);
                Check(session.Result != null && session.Result.Success && session.Error == null,
                    "A clean EOF terminated the OAuth attempt: " + session.Error);
                Check(session.Elapsed.ElapsedMilliseconds < 1500, "An incomplete request consumed the overall deadline.");
            }
        }

        private static void TestImmediateCloseThenCallback()
        {
            using (var session = new CallbackSession(TimeSpan.FromSeconds(2)))
            {
                using (TcpClient peer = Connect()) { }
                using (TcpClient client = Connect())
                {
                    Send(client, ValidRequest + "\r\n\r\n");
                    Check(ReadResponse(client).StartsWith("HTTP/1.1 200 OK"), "An immediately closed peer prevented the valid callback response.");
                }
                session.Finish(1000);
                Check(session.Result != null && session.Result.Success && session.Error == null,
                    "An immediately closed peer terminated the OAuth attempt: " + session.Error);
            }
        }

        private static TcpClient Connect()
        {
            var client = new TcpClient();
            client.Connect(IPAddress.Loopback, NexusOAuthConfiguration.CallbackPort);
            client.ReceiveTimeout = 2000;
            client.SendTimeout = 2000;
            client.NoDelay = true;
            return client;
        }

        private static void Send(TcpClient client, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            client.GetStream().Write(bytes, 0, bytes.Length);
        }

        private static string ReadResponse(TcpClient client)
        {
            var response = new StringBuilder();
            var buffer = new byte[1024];
            NetworkStream stream = client.GetStream();
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                response.Append(Encoding.ASCII.GetString(buffer, 0, count));
            return response.ToString();
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            _checks++;
        }

        private sealed class CallbackSession : IDisposable
        {
            private readonly Thread _thread;
            private readonly ManualResetEvent _ready = new ManualResetEvent(false);
            internal readonly Stopwatch Elapsed = Stopwatch.StartNew();
            internal NexusOAuthCallbackResult Result;
            internal string Error;

            internal CallbackSession(TimeSpan timeout)
                : this(timeout, TimeSpan.FromSeconds(5))
            {
            }

            internal CallbackSession(TimeSpan timeout, TimeSpan peerTimeout)
            {
                _thread = new Thread(delegate()
                {
                    var listener = new NexusLoopbackCallbackListener(delegate(Uri uri) { _ready.Set(); }, peerTimeout);
                    var authorization = new NexusOAuthAuthorizationRequest
                    {
                        AuthorizationUri = new Uri("https://example.invalid/authorize"),
                        State = "test-state"
                    };
                    Result = listener.WaitForCallback(authorization, timeout, out Error);
                    _ready.Set();
                });
                _thread.IsBackground = true;
                _thread.Start();
                Check(_ready.WaitOne(2000, false), "Callback listener did not start.");
                Check(Error == null, "Callback listener startup failed: " + Error);
            }

            internal void Finish(int milliseconds)
            {
                Check(_thread.Join(milliseconds), "Callback listener exceeded its test deadline.");
            }

            public void Dispose()
            {
                if (_thread.Join(2500))
                    _ready.Close();
            }
        }
    }
}
