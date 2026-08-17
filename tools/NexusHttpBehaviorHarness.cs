using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    internal static class NexusHttpBehaviorHarness
    {
        private static int _failures;

        private static void Main()
        {
            ServicePointManager.Expect100Continue = false;
            TestApplicationHeaders();
            TestCredentialIsolationAndUtcReset();
            TestConcurrentReservationAndStaleResponses();
            TestDefinitelyUnsentReservationRollback();
            TestRateLimitResponses();
            TestV3ApplicationHeaders();
            TestLegacyV1OAuthAuthentication();
            TestUnauthorizedOAuthInvalidation();
            TestOAuthAuthorizationCodeExchange();
            TestOAuthRateLimitHandling();
            TestPresignedRequestIsolation();

            if (_failures > 0)
            {
                Console.Error.WriteLine("Nexus HTTP behavior checks failed: " + _failures + ".");
                Environment.Exit(1);
            }

            Console.WriteLine("Nexus HTTP behavior checks passed.");
        }

        private static void TestApplicationHeaders()
        {
            using (var server = new ScriptedServer(Response.Ok(5, 50)))
            {
                NexusGraphQlResponse result = CreateGraphQl("oauth-a", new NexusRateLimitTracker(), server.Url).Execute("query { test }", null);
                server.WaitForRequests(1);
                Assert(result != null && string.IsNullOrEmpty(result.ErrorMessage), "GraphQL request should succeed.");
                Assert(server.Header(0, "Application-Name") == Manager.Core.AppVersionInfo.ApplicationName, "Application-Name value is incorrect.");
                Assert(server.Header(0, "Application-Version") == Manager.Core.AppVersionInfo.NexusHeader, "Application-Version value is incorrect.");
                Assert(server.Header(0, "User-Agent") == Manager.Core.AppVersionInfo.UserAgent, "User-Agent value is incorrect.");
                Assert(server.Header(0, "Authorization") == "Bearer oauth-a", "OAuth bearer header was not sent to the first-party API client.");
                Assert(string.IsNullOrEmpty(server.Header(0, "APIKEY")), "A personal API-key header was emitted by the first-party API client.");
            }
        }

        private static void TestCredentialIsolationAndUtcReset()
        {
            DateTime nowUtc = new DateTime(2026, 8, 5, 0, 59, 0, DateTimeKind.Utc);
            var tracker = new NexusRateLimitTracker(delegate { return nowUtc; });

            using (var serverA = new ScriptedServer(Response.Ok(0, 10, nowUtc)))
            {
                NexusGraphQlClient clientA = CreateGraphQl("oauth-a", tracker, serverA.Url);
                clientA.Execute("query { test }", null);
                NexusGraphQlResponse blocked = clientA.Execute("query { test }", null);
                Assert(blocked.ErrorMessage != null && blocked.ErrorMessage.IndexOf("hourly rate limit", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The exhausted credential should be blocked.");
                Assert(serverA.AcceptedCount == 1, "A blocked request reached the server.");
            }

            using (var serverB = new ScriptedServer(Response.Ok(4, 10, nowUtc)))
            {
                NexusGraphQlResponse otherAccount = CreateGraphQl("oauth-b", tracker, serverB.Url).Execute("query { test }", null);
                Assert(string.IsNullOrEmpty(otherAccount.ErrorMessage), "One credential's quota blocked another credential.");
            }

            nowUtc = nowUtc.AddMinutes(1);
            using (var serverAfterReset = new ScriptedServer(Response.Ok(4, 10, nowUtc)))
            {
                NexusGraphQlResponse afterReset = CreateGraphQl("oauth-a", tracker, serverAfterReset.Url).Execute("query { test }", null);
                Assert(string.IsNullOrEmpty(afterReset.ErrorMessage), "The hourly quota did not reopen at the next UTC hour.");
            }

            nowUtc = new DateTime(2026, 8, 5, 23, 59, 0, DateTimeKind.Utc);
            var dailyTracker = new NexusRateLimitTracker(delegate { return nowUtc; });
            using (var dailyServer = new ScriptedServer(Response.Ok(10, 0, nowUtc)))
            {
                NexusGraphQlClient dailyClient = CreateGraphQl("daily-oauth", dailyTracker, dailyServer.Url);
                dailyClient.Execute("query { test }", null);
                NexusGraphQlResponse blocked = dailyClient.Execute("query { test }", null);
                Assert(blocked.ErrorMessage != null && blocked.ErrorMessage.IndexOf("daily rate limit", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The exhausted daily quota should be blocked.");
            }

            nowUtc = nowUtc.AddMinutes(1);
            using (var nextDayServer = new ScriptedServer(Response.Ok(10, 20, nowUtc)))
            {
                NexusGraphQlResponse nextDay = CreateGraphQl("daily-oauth", dailyTracker, nextDayServer.Url).Execute("query { test }", null);
                Assert(string.IsNullOrEmpty(nextDay.ErrorMessage), "The daily quota did not reopen at UTC midnight.");
            }
        }

        private static void TestConcurrentReservationAndStaleResponses()
        {
            DateTime nowUtc = new DateTime(2026, 8, 5, 2, 0, 0, DateTimeKind.Utc);
            var responses = new List<Response>();
            responses.Add(Response.Ok(10, 100, nowUtc));
            responses.Add(Response.Ok(9, 99, nowUtc, 250));
            responses.Add(Response.Ok(8, 98, nowUtc));
            for (int i = 0; i < 9; i++)
                responses.Add(Response.OkWithoutQuota(nowUtc));

            using (var server = new ScriptedServer(responses.ToArray()))
            {
                var tracker = new NexusRateLimitTracker(delegate { return nowUtc; });
                NexusGraphQlClient client = CreateGraphQl("concurrent-oauth", tracker, server.Url);
                client.Execute("query { seed }", null);

                var first = new Thread(new ThreadStart(delegate { client.Execute("query { first }", null); }));
                var second = new Thread(new ThreadStart(delegate { client.Execute("query { second }", null); }));
                first.Start();
                second.Start();
                first.Join();
                second.Join();

                for (int i = 0; i < 8; i++)
                {
                    NexusGraphQlResponse allowed = client.Execute("query { allowed }", null);
                    Assert(string.IsNullOrEmpty(allowed.ErrorMessage), "A reserved request was blocked too early.");
                }

                NexusGraphQlResponse blocked = client.Execute("query { blocked }", null);
                Assert(blocked.ErrorMessage != null && blocked.ErrorMessage.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Concurrent reservations or stale responses restored exhausted capacity.");
                server.WaitForRequests(11);
                Assert(server.AcceptedCount == 11, "The blocked concurrent-capacity request reached the server.");
            }
        }

        private static void TestRateLimitResponses()
        {
            DateTime nowUtc = new DateTime(2026, 8, 5, 3, 0, 0, DateTimeKind.Utc);
            using (var withHeaders = new ScriptedServer(Response.RateLimited(0, 20, nowUtc)))
            {
                NexusGraphQlResponse result = CreateGraphQl("429-with", new NexusRateLimitTracker(delegate { return nowUtc; }), withHeaders.Url)
                    .Execute("query { test }", null);
                Assert(result.ErrorMessage != null && result.ErrorMessage.IndexOf("hourly rate limit", StringComparison.OrdinalIgnoreCase) >= 0,
                    "A 429 with quota headers did not return the quota reset message.");
            }

            using (var withoutHeaders = new ScriptedServer(Response.RateLimitedWithoutQuota(nowUtc)))
            {
                NexusGraphQlResponse result = CreateGraphQl("429-without", new NexusRateLimitTracker(delegate { return nowUtc; }), withoutHeaders.Url)
                    .Execute("query { test }", null);
                Assert(!string.IsNullOrEmpty(result.ErrorMessage), "A 429 without quota headers did not return an error.");
            }
        }

        private static void TestDefinitelyUnsentReservationRollback()
        {
            DateTime nowUtc = new DateTime(2026, 8, 5, 4, 0, 0, DateTimeKind.Utc);
            var tracker = new NexusRateLimitTracker(delegate { return nowUtc; });
            using (var seedServer = new ScriptedServer(Response.Ok(1, 10, nowUtc)))
            {
                CreateGraphQl("rollback-oauth", tracker, seedServer.Url).Execute("query { seed }", null);
            }

            var unused = new TcpListener(IPAddress.Loopback, 0);
            unused.Start();
            int unusedPort = ((IPEndPoint)unused.LocalEndpoint).Port;
            unused.Stop();
            CreateGraphQl("rollback-oauth", tracker, "http://127.0.0.1:" + unusedPort + "/")
                .Execute("query { unsent }", null);

            using (var recoveryServer = new ScriptedServer(Response.Ok(0, 9, nowUtc)))
            {
                NexusGraphQlResponse recovered = CreateGraphQl("rollback-oauth", tracker, recoveryServer.Url).Execute("query { recovered }", null);
                Assert(string.IsNullOrEmpty(recovered.ErrorMessage), "A definitely unsent request was not returned to local quota.");
            }
        }

        private static void TestV3ApplicationHeaders()
        {
            using (var server = new ScriptedServer(Response.V3Ok()))
            {
                var client = new NexusV3RestClient(
                    new TestOAuthCredentialProvider("v3-oauth"),
                    new NexusRateLimitTracker(),
                    server.Url.TrimEnd('/'));
                NexusV3RestResult result = client.Get("/test");
                server.WaitForRequests(1);
                Assert(result != null && string.IsNullOrEmpty(result.ErrorMessage), "Nexus v3 request should succeed.");
                Assert(server.Header(0, "Application-Name") == Manager.Core.AppVersionInfo.ApplicationName, "v3 Application-Name value is incorrect.");
                Assert(server.Header(0, "Application-Version") == Manager.Core.AppVersionInfo.NexusHeader, "v3 Application-Version value is incorrect.");
                Assert(server.Header(0, "Authorization") == "Bearer v3-oauth", "v3 OAuth bearer header is incorrect.");
                Assert(string.IsNullOrEmpty(server.Header(0, "APIKEY")), "v3 emitted a personal API-key header.");
            }
        }

        private static void TestUnauthorizedOAuthInvalidation()
        {
            using (var server = new ScriptedServer(Response.Unauthorized()))
            {
                var provider = new TestOAuthCredentialProvider("revoked-graphql");
                NexusGraphQlResponse result = new NexusGraphQlClient(provider, new NexusRateLimitTracker(), server.Url)
                    .Execute("query { viewer { name } }", null);
                Assert(provider.Invalidations == 1 && !provider.HasConfiguredCredential,
                    "GraphQL 401 did not invalidate the OAuth session.");
                Assert(result.ErrorMessage != null && result.ErrorMessage.IndexOf("Sign in again", StringComparison.OrdinalIgnoreCase) >= 0,
                    "GraphQL 401 did not require OAuth reconnection.");
            }

            using (var server = new ScriptedServer(Response.Unauthorized()))
            {
                var provider = new TestOAuthCredentialProvider("revoked-v3");
                NexusV3RestResult result = new NexusV3RestClient(provider, new NexusRateLimitTracker(), server.Url.TrimEnd('/')).Get("/test");
                Assert(provider.Invalidations == 1 && !provider.HasConfiguredCredential,
                    "v3 401 did not invalidate the OAuth session.");
                Assert(result.ErrorMessage != null && result.ErrorMessage.IndexOf("Sign in again", StringComparison.OrdinalIgnoreCase) >= 0,
                    "v3 401 did not require OAuth reconnection.");
            }
        }

        private static void TestLegacyV1OAuthAuthentication()
        {
            using (var server = new ScriptedServer(Response.LegacyUserOk()))
            {
                var provider = new TestOAuthCredentialProvider("v1-oauth");
                var service = new NexusModsService(provider, new NexusRateLimitTracker(), server.Url.TrimEnd('/'));
                string error;
                NexusAccountStatus status = service.GetAccountStatus(out error);
                server.WaitForRequests(1);
                Assert(string.IsNullOrEmpty(error) && status != null && status.IsConnected,
                    "Nexus v1 account request should succeed with OAuth.");
                Assert(server.Header(0, "Authorization") == "Bearer v1-oauth",
                    "Nexus v1 did not emit the OAuth bearer header.");
                Assert(string.IsNullOrEmpty(server.Header(0, "APIKEY")),
                    "Nexus v1 emitted a personal API-key header.");
            }

            using (var server = new ScriptedServer(Response.Unauthorized()))
            {
                var provider = new TestOAuthCredentialProvider("revoked-v1");
                var service = new NexusModsService(provider, new NexusRateLimitTracker(), server.Url.TrimEnd('/'));
                string error;
                service.GetAccountStatus(out error);
                Assert(provider.Invalidations == 1 && !provider.HasConfiguredCredential,
                    "Nexus v1 401 did not invalidate the OAuth session.");
                Assert(error != null && error.IndexOf("Sign in again", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Nexus v1 401 did not require OAuth reconnection.");
            }
        }

        private static void TestOAuthRateLimitHandling()
        {
            using (var server = new ScriptedServer(Response.OAuthRateLimited("120")))
            {
                var client = new NexusOAuthClient(server.Url + "token");
                string errorMessage;
                client.Refresh("refresh-token", out errorMessage);
                server.WaitForRequests(1);
                Assert(errorMessage != null && errorMessage.IndexOf("rate limited", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    errorMessage.IndexOf("120", StringComparison.Ordinal) >= 0,
                    "OAuth 429 handling did not preserve Retry-After guidance.");
                Assert(server.Header(0, "Application-Name") == Manager.Core.AppVersionInfo.ApplicationName, "OAuth Application-Name value is incorrect.");
                Assert(server.Header(0, "Application-Version") == Manager.Core.AppVersionInfo.NexusHeader, "OAuth Application-Version value is incorrect.");
                Assert(string.IsNullOrEmpty(server.Header(0, "APIKEY")), "An API key leaked to the OAuth token endpoint.");
                Assert(string.IsNullOrEmpty(server.Header(0, "Authorization")), "Authorization leaked to the OAuth token endpoint.");
            }
        }

        private static void TestOAuthAuthorizationCodeExchange()
        {
            using (var server = new ScriptedServer(Response.OAuthTokens()))
            {
                var client = new NexusOAuthClient(server.Url + "token");
                string errorMessage;
                NexusOAuthTokenSet tokens = client.ExchangeAuthorizationCode("authorization-code", "pkce-verifier", out errorMessage);
                server.WaitForRequests(1);
                Assert(string.IsNullOrEmpty(errorMessage) && tokens != null &&
                    tokens.AccessToken == "issued-access" && tokens.RefreshToken == "issued-refresh" &&
                    tokens.ExpiresAtUtc > DateTime.UtcNow,
                    "OAuth authorization-code exchange did not establish a usable token set.");
                Assert(string.IsNullOrEmpty(server.Header(0, "APIKEY")),
                    "A personal API-key header leaked into OAuth sign-in.");
                Assert(string.IsNullOrEmpty(server.Header(0, "Authorization")),
                    "An API bearer token leaked into the OAuth token exchange.");
            }
        }

        private static void TestPresignedRequestIsolation()
        {
            using (var server = new ScriptedServer(Response.UploadOk()))
            {
                var client = new NexusV3RestClient(
                    new TestOAuthCredentialProvider("presigned-oauth"),
                    new NexusRateLimitTracker(),
                    server.Url.TrimEnd('/'));
                NexusV3UploadResult result = client.PutBytes(server.Url + "upload", new byte[] { 1, 2, 3 });
                server.WaitForRequests(1);
                Assert(result.Success, "Presigned byte upload should succeed.");
                Assert(string.IsNullOrEmpty(server.Header(0, "Application-Name")), "Application-Name leaked to a presigned URL.");
                Assert(string.IsNullOrEmpty(server.Header(0, "Application-Version")), "Application-Version leaked to a presigned URL.");
                Assert(string.IsNullOrEmpty(server.Header(0, "APIKEY")), "API key leaked to a presigned URL.");
                Assert(string.IsNullOrEmpty(server.Header(0, "Authorization")), "Authorization leaked to a presigned URL.");
            }

            using (var server = new ScriptedServer(Response.UploadOk()))
            {
                var client = new NexusV3RestClient(
                    new TestOAuthCredentialProvider("presigned-oauth"),
                    new NexusRateLimitTracker(),
                    server.Url.TrimEnd('/'));
                NexusV3UploadResult result = client.PostXml(server.Url + "complete", "<CompleteMultipartUpload />");
                server.WaitForRequests(1);
                Assert(result.Success, "Presigned XML completion should succeed.");
                Assert(string.IsNullOrEmpty(server.Header(0, "Application-Name")), "Application-Name leaked to a presigned XML URL.");
                Assert(string.IsNullOrEmpty(server.Header(0, "Application-Version")), "Application-Version leaked to a presigned XML URL.");
                Assert(string.IsNullOrEmpty(server.Header(0, "APIKEY")), "API key leaked to a presigned XML URL.");
                Assert(string.IsNullOrEmpty(server.Header(0, "Authorization")), "Authorization leaked to a presigned XML URL.");
            }
        }

        private static NexusGraphQlClient CreateGraphQl(string bearerToken, NexusRateLimitTracker tracker, string endpoint)
        {
            return new NexusGraphQlClient(new TestOAuthCredentialProvider(bearerToken), tracker, endpoint);
        }

        private sealed class TestOAuthCredentialProvider : INexusCredentialProvider
        {
            private string _bearerToken;
            internal int Invalidations;

            internal TestOAuthCredentialProvider(string bearerToken)
            {
                _bearerToken = bearerToken ?? string.Empty;
            }

            public NexusRequestCredential GetCredential(out string errorMessage)
            {
                errorMessage = null;
                return new NexusRequestCredential
                {
                    BearerToken = _bearerToken,
                    RateLimitScope = "test-oauth:" + _bearerToken
                };
            }

            public bool HasConfiguredCredential
            {
                get { return !string.IsNullOrEmpty(_bearerToken); }
            }

            public void InvalidateCredential()
            {
                Invalidations++;
                _bearerToken = string.Empty;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (condition)
                return;
            _failures++;
            Console.Error.WriteLine(message);
        }

        private sealed class Response
        {
            internal int StatusCode;
            internal string Reason;
            internal string Body;
            internal string ETag;
            internal string RetryAfter;
            internal int? HourlyRemaining;
            internal int? DailyRemaining;
            internal DateTime DateUtc;
            internal int DelayMilliseconds;

            internal static Response Ok(int hourly, int daily)
            {
                return Ok(hourly, daily, DateTime.UtcNow);
            }

            internal static Response Ok(int hourly, int daily, DateTime dateUtc, int delayMilliseconds)
            {
                return new Response { StatusCode = 200, Reason = "OK", Body = "{\"data\":{}}", HourlyRemaining = hourly, DailyRemaining = daily, DateUtc = dateUtc, DelayMilliseconds = delayMilliseconds };
            }

            internal static Response Ok(int hourly, int daily, DateTime dateUtc)
            {
                return Ok(hourly, daily, dateUtc, 0);
            }

            internal static Response OkWithoutQuota(DateTime dateUtc)
            {
                return new Response { StatusCode = 200, Reason = "OK", Body = "{\"data\":{}}", DateUtc = dateUtc };
            }

            internal static Response RateLimited(int hourly, int daily, DateTime dateUtc)
            {
                return new Response { StatusCode = 429, Reason = "Too Many Requests", Body = "{\"message\":\"rate limited\"}", HourlyRemaining = hourly, DailyRemaining = daily, DateUtc = dateUtc };
            }

            internal static Response RateLimitedWithoutQuota(DateTime dateUtc)
            {
                return new Response { StatusCode = 429, Reason = "Too Many Requests", Body = "{\"message\":\"rate limited\"}", DateUtc = dateUtc };
            }

            internal static Response UploadOk()
            {
                return new Response { StatusCode = 200, Reason = "OK", Body = string.Empty, ETag = "test-etag", DateUtc = DateTime.UtcNow };
            }

            internal static Response V3Ok()
            {
                return new Response { StatusCode = 200, Reason = "OK", Body = "{\"data\":{\"id\":1}}", HourlyRemaining = 5, DailyRemaining = 50, DateUtc = DateTime.UtcNow };
            }

            internal static Response LegacyUserOk()
            {
                return new Response
                {
                    StatusCode = 200,
                    Reason = "OK",
                    Body = "{\"user_id\":7,\"name\":\"OAuth User\",\"is_premium\":true,\"is_supporter\":true}",
                    HourlyRemaining = 5,
                    DailyRemaining = 50,
                    DateUtc = DateTime.UtcNow
                };
            }

            internal static Response Unauthorized()
            {
                return new Response { StatusCode = 401, Reason = "Unauthorized", Body = "{\"message\":\"revoked\"}", DateUtc = DateTime.UtcNow };
            }

            internal static Response OAuthRateLimited(string retryAfter)
            {
                return new Response { StatusCode = 429, Reason = "Too Many Requests", Body = "{\"error\":\"slow_down\"}", RetryAfter = retryAfter, DateUtc = DateTime.UtcNow };
            }

            internal static Response OAuthTokens()
            {
                return new Response
                {
                    StatusCode = 200,
                    Reason = "OK",
                    Body = "{\"access_token\":\"issued-access\",\"refresh_token\":\"issued-refresh\",\"expires_in\":3600}",
                    DateUtc = DateTime.UtcNow
                };
            }
        }

        private sealed class ScriptedServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly Queue<Response> _responses;
            private readonly List<Dictionary<string, string>> _headers = new List<Dictionary<string, string>>();
            private readonly List<Thread> _workers = new List<Thread>();
            private readonly Thread _acceptThread;
            private bool _stopping;

            internal ScriptedServer(params Response[] responses)
            {
                _responses = new Queue<Response>(responses);
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                int port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                Url = "http://127.0.0.1:" + port + "/";
                _acceptThread = new Thread(AcceptLoop);
                _acceptThread.IsBackground = true;
                _acceptThread.Start();
            }

            internal string Url { get; private set; }

            internal int AcceptedCount
            {
                get { lock (_headers) return _headers.Count; }
            }

            internal string Header(int requestIndex, string name)
            {
                lock (_headers)
                {
                    string value;
                    return requestIndex < _headers.Count && _headers[requestIndex].TryGetValue(name, out value) ? value : string.Empty;
                }
            }

            internal void WaitForRequests(int count)
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                while (AcceptedCount < count && DateTime.UtcNow < deadline)
                    Thread.Sleep(10);
            }

            private void AcceptLoop()
            {
                while (!_stopping)
                {
                    try
                    {
                        TcpClient client = _listener.AcceptTcpClient();
                        Response response;
                        lock (_responses)
                        {
                            if (_responses.Count == 0)
                            {
                                client.Close();
                                continue;
                            }
                            response = _responses.Dequeue();
                        }

                        var worker = new Thread(new ThreadStart(delegate { Handle(client, response); }));
                        worker.IsBackground = true;
                        lock (_workers) _workers.Add(worker);
                        worker.Start();
                    }
                    catch (SocketException)
                    {
                        if (!_stopping)
                            throw;
                    }
                }
            }

            private void Handle(TcpClient client, Response response)
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    Dictionary<string, string> requestHeaders = ReadRequest(stream);
                    lock (_headers) _headers.Add(requestHeaders);
                    if (response.DelayMilliseconds > 0)
                        Thread.Sleep(response.DelayMilliseconds);

                    byte[] body = Encoding.UTF8.GetBytes(response.Body ?? string.Empty);
                    var builder = new StringBuilder();
                    builder.Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(response.Reason).Append("\r\n");
                    builder.Append("Date: ").Append(response.DateUtc.ToUniversalTime().ToString("R")).Append("\r\n");
                    builder.Append("Content-Type: application/json\r\n");
                    builder.Append("Content-Length: ").Append(body.Length).Append("\r\n");
                    builder.Append("Connection: close\r\n");
                    if (response.HourlyRemaining.HasValue)
                        builder.Append("X-RL-Hourly-Remaining: ").Append(response.HourlyRemaining.Value).Append("\r\n");
                    if (response.DailyRemaining.HasValue)
                        builder.Append("X-RL-Daily-Remaining: ").Append(response.DailyRemaining.Value).Append("\r\n");
                    if (!string.IsNullOrEmpty(response.ETag))
                        builder.Append("ETag: \"").Append(response.ETag).Append("\"\r\n");
                    if (!string.IsNullOrEmpty(response.RetryAfter))
                        builder.Append("Retry-After: ").Append(response.RetryAfter).Append("\r\n");
                    builder.Append("\r\n");
                    byte[] headerBytes = Encoding.ASCII.GetBytes(builder.ToString());
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(body, 0, body.Length);
                }
            }

            private static Dictionary<string, string> ReadRequest(NetworkStream stream)
            {
                var bytes = new List<byte>();
                int matched = 0;
                while (matched < 4)
                {
                    int value = stream.ReadByte();
                    if (value < 0)
                        break;
                    bytes.Add((byte)value);
                    byte expected = matched == 0 || matched == 2 ? (byte)'\r' : (byte)'\n';
                    matched = value == expected ? matched + 1 : (value == '\r' ? 1 : 0);
                }

                string headerText = Encoding.ASCII.GetString(bytes.ToArray());
                string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int contentLength = 0;
                for (int i = 1; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf(':');
                    if (separator <= 0)
                        continue;
                    string name = lines[i].Substring(0, separator).Trim();
                    string value = lines[i].Substring(separator + 1).Trim();
                    headers[name] = value;
                    if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(value, out contentLength);
                }

                var body = new byte[contentLength];
                int offset = 0;
                while (offset < body.Length)
                {
                    int read = stream.Read(body, offset, body.Length - offset);
                    if (read <= 0)
                        break;
                    offset += read;
                }
                return headers;
            }

            public void Dispose()
            {
                _stopping = true;
                _listener.Stop();
                _acceptThread.Join(1000);
                lock (_workers)
                {
                    for (int i = 0; i < _workers.Count; i++)
                        _workers[i].Join(1000);
                }
            }
        }
    }
}
