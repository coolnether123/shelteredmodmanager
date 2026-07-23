using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
                Process.Start(authorization.AuthorizationUri.AbsoluteUri);

                DateTime deadlineUtc = DateTime.UtcNow.Add(timeout);
                while (DateTime.UtcNow < deadlineUtc)
                {
                    IAsyncResult pending = listener.BeginAcceptTcpClient(null, null);
                    TimeSpan remaining = deadlineUtc - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero || !pending.AsyncWaitHandle.WaitOne(remaining, false))
                    {
                        errorMessage = "Nexus sign-in timed out before the browser returned.";
                        return null;
                    }

                    using (TcpClient client = listener.EndAcceptTcpClient(pending))
                    {
                        NexusOAuthCallbackResult result = ReadCallback(client, authorization.State);
                        WriteResponse(client, result);
                        if (result != null && result.Success)
                            return result;

                        if (result != null &&
                            result.ErrorMessage != "The OAuth callback path was not recognized.")
                        {
                            errorMessage = result.ErrorMessage;
                            return result;
                        }
                    }
                }

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

        private static NexusOAuthCallbackResult ReadCallback(TcpClient client, string expectedState)
        {
            if (client == null || client.Client == null ||
                client.Client.RemoteEndPoint == null ||
                !IPAddress.IsLoopback(((IPEndPoint)client.Client.RemoteEndPoint).Address))
            {
                return Failure("The OAuth callback did not originate from this computer.");
            }

            client.ReceiveTimeout = 5000;
            NetworkStream stream = client.GetStream();
            stream.ReadTimeout = 5000;

            string requestLine;
            var reader = new StreamReader(stream, Encoding.ASCII, false, 1024);
            requestLine = reader.ReadLine();
            if (requestLine != null && requestLine.Length > MaximumRequestLineLength)
                return Failure("The OAuth callback request was too large.");

            string header;
            int headerLength = 0;
            do
            {
                header = reader.ReadLine();
                headerLength += header != null ? header.Length : 0;
                if (headerLength > MaximumHeaderLength)
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

        private static void WriteResponse(TcpClient client, NexusOAuthCallbackResult result)
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
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }

        private static NexusOAuthCallbackResult Failure(string message)
        {
            return new NexusOAuthCallbackResult { ErrorMessage = message };
        }
    }
}
