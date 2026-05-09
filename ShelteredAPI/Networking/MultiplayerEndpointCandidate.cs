using System;
using System.Collections.Generic;
using ModAPI.Networking.Addressing;

namespace ShelteredAPI.Networking
{
    internal sealed class MultiplayerEndpointCandidate
    {
        public MultiplayerEndpointCandidate(
            string label,
            string endpointText,
            string description,
            bool recommended)
        {
            Label = label ?? string.Empty;
            EndpointText = endpointText ?? string.Empty;
            Description = description ?? string.Empty;
            Recommended = recommended;
        }

        public string Label { get; private set; }
        public string EndpointText { get; private set; }
        public string Description { get; private set; }
        public bool Recommended { get; private set; }
    }

    internal sealed class MultiplayerEndpointCandidateList
    {
        public MultiplayerEndpointCandidate[] Candidates = new MultiplayerEndpointCandidate[0];
        public string StatusText = string.Empty;
    }

    internal static class MultiplayerEndpointCandidateBuilder
    {
        public static MultiplayerEndpointCandidateList Build(int port)
        {
            MultiplayerEndpointCandidateList result = new MultiplayerEndpointCandidateList();
            MultiplayerPortValidationResult validation = MultiplayerConnectionInputValidator.ValidatePort(port);
            if (!validation.IsValid)
            {
                result.StatusText = validation.ErrorText;
                return result;
            }

            LocalNetworkAddressSelection selection = LocalNetworkAddressHelper.SelectBestLanAddress();
            LocalNetworkAddressInfo[] addresses = LocalNetworkAddressHelper.GetLocalIPv4Addresses();
            result.Candidates = BuildCandidates(validation.Port, addresses, selection != null ? selection.Address : null);
            if (selection != null && !selection.Success && !string.IsNullOrEmpty(selection.Message))
                result.StatusText = selection.Message + " Manual endpoint entry and local loopback testing remain available.";

            return result;
        }

        internal static MultiplayerEndpointCandidate[] BuildCandidates(
            int port,
            LocalNetworkAddressInfo[] addresses,
            LocalNetworkAddressInfo recommendedAddress)
        {
            List<MultiplayerEndpointCandidate> candidates = new List<MultiplayerEndpointCandidate>();
            HashSet<string> endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (recommendedAddress != null)
                AddCandidate(candidates, endpoints, recommendedAddress, port, true);

            if (addresses != null)
            {
                for (int i = 0; i < addresses.Length; i++)
                    AddCandidate(candidates, endpoints, addresses[i], port, false);
            }

            AddLoopbackCandidate(candidates, endpoints, port);
            return candidates.ToArray();
        }

        internal static string ClassifyForText(
            string interfaceName,
            string interfaceDescription,
            string interfaceType,
            bool isLoopback,
            bool isPrivate,
            bool isLinkLocal)
        {
            if (isLoopback)
                return "Loopback";
            if (IsLikelyVpn(interfaceName, interfaceDescription, interfaceType))
                return "VPN";
            if (isLinkLocal)
                return "Link-local";
            if (isPrivate)
                return "LAN";

            return "Local IPv4";
        }

        private static void AddCandidate(
            List<MultiplayerEndpointCandidate> candidates,
            HashSet<string> endpoints,
            LocalNetworkAddressInfo address,
            int port,
            bool recommended)
        {
            if (address == null || address.Address == null)
                return;

            string endpoint = address.Address + ":" + port;
            if (endpoints.Contains(endpoint))
                return;

            string label = ClassifyForText(
                address.InterfaceName,
                address.InterfaceDescription,
                address.InterfaceType,
                address.IsLoopback,
                address.IsPrivate,
                address.IsLinkLocal);
            string description = BuildDescription(label, address, recommended);
            candidates.Add(new MultiplayerEndpointCandidate(label, endpoint, description, recommended));
            endpoints.Add(endpoint);
        }

        private static void AddLoopbackCandidate(
            List<MultiplayerEndpointCandidate> candidates,
            HashSet<string> endpoints,
            int port)
        {
            string endpoint = "127.0.0.1:" + port;
            if (endpoints.Contains(endpoint))
                return;

            candidates.Add(new MultiplayerEndpointCandidate(
                "Loopback",
                endpoint,
                "Same PC only; useful for a second local test client.",
                false));
            endpoints.Add(endpoint);
        }

        private static string BuildDescription(
            string label,
            LocalNetworkAddressInfo address,
            bool recommended)
        {
            string prefix = recommended ? "Recommended. " : string.Empty;
            string adapter = BuildAdapterText(address);
            if (label == "VPN")
                return prefix + "Likely VPN adapter" + adapter + "; use this for VPN-hosted games.";
            if (label == "LAN")
                return prefix + "Likely LAN address" + adapter + "; share with players on the same LAN or VPN route.";
            if (label == "Loopback")
                return prefix + "Same PC only; useful for a second local test client.";
            if (label == "Link-local")
                return prefix + "Fallback adapter address" + adapter + "; manual endpoint entry may be more reliable.";

            return prefix + "Local IPv4 address" + adapter + "; confirm firewall, router, or VPN routing.";
        }

        private static string BuildAdapterText(LocalNetworkAddressInfo address)
        {
            if (address == null)
                return string.Empty;

            string name = !string.IsNullOrEmpty(address.InterfaceName)
                ? address.InterfaceName
                : address.InterfaceDescription;
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            return " (" + name + ")";
        }

        private static bool IsLikelyVpn(
            string interfaceName,
            string interfaceDescription,
            string interfaceType)
        {
            string value = ((interfaceName ?? string.Empty) + " "
                + (interfaceDescription ?? string.Empty) + " "
                + (interfaceType ?? string.Empty)).ToLowerInvariant();

            return Contains(value, "vpn")
                || Contains(value, "hamachi")
                || Contains(value, "radmin")
                || Contains(value, "tailscale")
                || Contains(value, "zerotier")
                || Contains(value, "wireguard")
                || Contains(value, "openvpn")
                || Contains(value, "wintun")
                || Contains(value, "tap")
                || Contains(value, "tun ")
                || Contains(value, "ppp")
                || Contains(value, "mullvad")
                || Contains(value, "nord")
                || Contains(value, "proton");
        }

        private static bool Contains(string value, string needle)
        {
            return value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
