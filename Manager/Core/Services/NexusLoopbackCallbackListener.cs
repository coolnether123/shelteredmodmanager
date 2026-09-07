using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Manager.Core.Services
{
    /// <summary>
    /// Receives one OAuth redirect on a TCP listener bound exclusively to the
    /// IPv4 loopback address. This avoids HttpListener URL ACL requirements.
    /// </summary>
    internal sealed class NexusLoopbackCallbackListener
    {
        private const int MaximumRequestLineLength = 8192;
        private const int MaximumHeaderLength = 32768;
        private readonly Action<Uri> _openBrowser;
        private readonly TimeSpan _peerTimeout;

        internal NexusLoopbackCallbackListener()
            : this(delegate(Uri uri) { Process.Start(uri.AbsoluteUri); })
        {
        }

        internal NexusLoopbackCallbackListener(Action<Uri> openBrowser)
            : this(openBrowser, TimeSpan.FromSeconds(5))
        {
        }

        internal NexusLoopbackCallbackListener(Action<Uri> openBrowser, TimeSpan peerTimeout)
        {
            if (openBrowser == null)
                throw new ArgumentNullException("openBrowser");
            if (peerTimeout <= TimeSpan.Zero || peerTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException("peerTimeout");
            _openBrowser = openBrowser;
            _peerTimeout = peerTimeout;
        }

        internal NexusOAuthCallbackResult WaitForCallback(
            NexusOAuthAuthorizationRequest authorization,
            TimeSpan timeout,
            out string errorMessage)
        {
            errorMessage = null;
            if (authorization == null || authorization.AuthorizationUri == null)
            {
                errorMessage = "The Nexus authorization request was not initialized.";
                return null;
            }

            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, NexusOAuthConfiguration.CallbackPort);
                listener.Start(1);
                var elapsed = Stopwatch.StartNew();
                _openBrowser(authorization.AuthorizationUri);

                while (true)
                {
                    Remaining(elapsed, timeout);
                    IAsyncResult pending = listener.BeginAcceptTcpClient(null, null);
                    using (WaitHandle completed = pending.AsyncWaitHandle)
                    {
                        if (!completed.WaitOne(Remaining(elapsed, timeout), false))
                            throw new TimeoutException();
                    }

                    using (TcpClient client = listener.EndAcceptTcpClient(pending))
                    {
                        try
                        {
                            NexusOAuthCallbackResult result = ReadCallback(client, authorization.State, elapsed, timeout, _peerTimeout);
                            WriteResponse(client, result, elapsed, timeout, _peerTimeout);
                            if (result != null && result.Success)
                                return result;

                            if (result != null &&
                                result.ErrorMessage != "The OAuth callback path was not recognized.")
                            {
                                errorMessage = result.ErrorMessage;
                                return result;
                            }
                        }
                        catch (IOException) { }
                        catch (SocketException) { }
                        catch (ObjectDisposedException) { }
                    }
                }
            }
            catch (TimeoutException)
            {
                errorMessage = "Nexus sign-in timed out before the browser returned.";
                return null;
            }
            catch (SocketException ex)
            {
                errorMessage = "The local OAuth callback could not start on " +
                    NexusOAuthConfiguration.RedirectUri + ": " + ex.Message;
                return null;
            }
            catch (Exception ex)
            {
                errorMessage = "Nexus sign-in could not be completed: " + ex.Message;
                return null;
            }
            finally
            {
                if (listener != null)
                    listener.Stop();
            }
        }

        private static NexusOAuthCallbackResult ReadCallback(TcpClient client, string expectedState,
            Stopwatch elapsed, TimeSpan timeout, TimeSpan peerTimeout)
        {
            if (client == null || client.Client == null ||
                client.Client.RemoteEndPoint == null ||
                !IPAddress.IsLoopback(((IPEndPoint)client.Client.RemoteEndPoint).Address))
            {
                return Failure("The OAuth callback did not originate from this computer.");
            }

            var reader = new CallbackReader(client.GetStream(), elapsed, timeout, peerTimeout);
            int requestBudget = MaximumRequestLineLength;
            bool tooLarge;
            string requestLine = reader.ReadLine(ref requestBudget, out tooLarge);
            if (tooLarge)
                return Failure("The OAuth callback request was too large.");

            string header;
            int headerBudget = MaximumHeaderLength;
            do
            {
                header = reader.ReadLine(ref headerBudget, out tooLarge);
                if (tooLarge)
                    return Failure("The OAuth callback headers were too large.");
            }
            while (!string.IsNullOrEmpty(header));

            if (string.IsNullOrEmpty(requestLine))
                return Failure("The OAuth callback request was empty.");

            string[] parts = requestLine.Split(' ');
            if (parts.Length < 3 || !string.Equals(parts[0], "GET", StringComparison.Ordinal))
                return Failure("The OAuth callback used an unsupported HTTP request.");

            return NexusOAuthProtocol.ParseCallback(parts[1], expectedState);
        }

        private static void WriteResponse(TcpClient client, NexusOAuthCallbackResult result,
            Stopwatch elapsed, TimeSpan timeout, TimeSpan peerTimeout)
        {
            bool success = result != null && result.Success;
            string title = success ? "Nexus sign-in complete" : "Nexus sign-in was not completed";
            string detail = success
                ? "You can close this browser tab and return to Sheltered Mod Manager."
                : "Return to Sheltered Mod Manager for details.";
            string body = "<!doctype html><html><head><meta charset=\"utf-8\"><title>" +
                title + "</title></head><body><h1>" + title + "</h1><p>" + detail + "</p></body></html>";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string status = success ? "200 OK" : "400 Bad Request";
            string headers = "HTTP/1.1 " + status + "\r\n" +
                "Content-Type: text/html; charset=utf-8\r\n" +
                "Content-Length: " + bodyBytes.Length + "\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n\r\n";

            NetworkStream stream = client.GetStream();
            byte[] responseBytes = Encoding.UTF8.GetBytes(headers + body);
            Remaining(elapsed, timeout);
            IAsyncResult pending = stream.BeginWrite(responseBytes, 0, responseBytes.Length, null, null);
            bool waitCompleted = false;
            try
            {
                WaitForIo(pending, stream, elapsed, timeout, peerTimeout);
                waitCompleted = true;
            }
            finally
            {
                try { stream.EndWrite(pending); }
                catch (IOException) { if (waitCompleted) throw; }
                catch (ObjectDisposedException) { if (waitCompleted) throw; }
            }
            Remaining(elapsed, timeout);
        }

        private static TimeSpan Remaining(Stopwatch elapsed, TimeSpan timeout)
        {
            TimeSpan remaining = timeout - elapsed.Elapsed;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException();
            return remaining;
        }

        private static void WaitForIo(IAsyncResult pending, NetworkStream stream,
            Stopwatch elapsed, TimeSpan timeout, TimeSpan peerTimeout)
        {
            try
            {
                using (WaitHandle completed = pending.AsyncWaitHandle)
                {
                    TimeSpan remaining = Remaining(elapsed, timeout);
                    TimeSpan wait = remaining < peerTimeout ? remaining : peerTimeout;
                    if (!completed.WaitOne((int)Math.Ceiling(wait.TotalMilliseconds), false))
                    {
                        if (wait == remaining)
                            throw new TimeoutException();
                        throw new IOException("The OAuth callback connection timed out.");
                    }
                }
            }
            catch
            {
                stream.Close();
                throw;
            }
        }

        private sealed class CallbackReader
        {
            private readonly NetworkStream _stream;
            private readonly Stopwatch _elapsed;
            private readonly TimeSpan _timeout;
            private readonly TimeSpan _peerTimeout;
            private readonly byte[] _buffer = new byte[1024];
            private int _position;
            private int _length;
            private bool _skipLineFeed;

            internal CallbackReader(NetworkStream stream, Stopwatch elapsed, TimeSpan timeout, TimeSpan peerTimeout)
            {
                _stream = stream;
                _elapsed = elapsed;
                _timeout = timeout;
                _peerTimeout = peerTimeout;
            }

            internal string ReadLine(ref int budget, out bool tooLarge)
            {
                var line = new StringBuilder();
                tooLarge = false;
                while (true)
                {
                    int value = ReadByte();
                    if (_skipLineFeed)
                    {
                        _skipLineFeed = false;
                        if (value == '\n')
                            continue;
                    }
                    if (value == '\r' || value == '\n')
                    {
                        _skipLineFeed = value == '\r';
                        return line.ToString();
                    }
                    if (budget == 0)
                    {
                        tooLarge = true;
                        return null;
                    }
                    budget--;
                    line.Append(value < 128 ? (char)value : '?');
                }
            }

            private int ReadByte()
            {
                Remaining(_elapsed, _timeout);
                if (_position == _length)
                {
                    IAsyncResult pending = _stream.BeginRead(_buffer, 0, _buffer.Length, null, null);
                    bool waitCompleted = false;
                    try
                    {
                        WaitForIo(pending, _stream, _elapsed, _timeout, _peerTimeout);
                        waitCompleted = true;
                    }
                    finally
                    {
                        try { _length = _stream.EndRead(pending); }
                        catch (IOException) { if (waitCompleted) throw; }
                        catch (ObjectDisposedException) { if (waitCompleted) throw; }
                    }
                    Remaining(_elapsed, _timeout);
                    _position = 0;
                    if (_length == 0)
                        throw new EndOfStreamException();
                }
                return _buffer[_position++];
            }
        }

        private static NexusOAuthCallbackResult Failure(string message)
        {
            return new NexusOAuthCallbackResult { ErrorMessage = message };
        }
    }
}
