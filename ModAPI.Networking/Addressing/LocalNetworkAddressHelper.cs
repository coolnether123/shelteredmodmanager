using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ModAPI.Networking.Addressing
{
    public static class LocalNetworkAddressHelper
    {
        public static LocalNetworkInterfaceInfo[] GetLocalInterfaces()
        {
            List<LocalNetworkInterfaceInfo> interfaces = new List<LocalNetworkInterfaceInfo>();
            NetworkInterface[] systemInterfaces;
            try
            {
                systemInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch
            {
                return interfaces.ToArray();
            }

            for (int i = 0; i < systemInterfaces.Length; i++)
            {
                LocalNetworkInterfaceInfo info = CreateInterfaceInfo(systemInterfaces[i]);
                if (info != null)
                    interfaces.Add(info);
            }

            return interfaces.ToArray();
        }

        public static LocalNetworkAddressInfo[] GetLocalIPv4Addresses()
        {
            List<LocalNetworkAddressInfo> addresses = new List<LocalNetworkAddressInfo>();
            LocalNetworkInterfaceInfo[] interfaces = GetLocalInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                LocalNetworkAddressInfo[] interfaceAddresses = interfaces[i].Addresses;
                for (int j = 0; j < interfaceAddresses.Length; j++)
                    addresses.Add(interfaceAddresses[j]);
            }

            return addresses.ToArray();
        }

        public static LocalNetworkAddressSelection SelectBestLanAddress()
        {
            LocalNetworkAddressInfo[] addresses = GetLocalIPv4Addresses();
            LocalNetworkAddressInfo best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < addresses.Length; i++)
            {
                int score = Score(addresses[i]);
                if (best == null || score > bestScore)
                {
                    best = addresses[i];
                    bestScore = score;
                }
            }

            if (best == null)
                return LocalNetworkAddressSelection.Failed("No local IPv4 addresses were found.");

            if (best.IsLoopback)
                return LocalNetworkAddressSelection.Failed("Only loopback IPv4 addresses were found; no LAN address is available.");

            return LocalNetworkAddressSelection.Succeeded(best);
        }

        public static bool TrySelectBestLanAddress(out LocalNetworkAddressInfo address)
        {
            LocalNetworkAddressSelection selection = SelectBestLanAddress();
            address = selection.Address;
            return selection.Success;
        }

        public static bool IsPrivateIPv4(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168);
        }

        public static bool IsLinkLocalIPv4(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;
        }

        private static LocalNetworkInterfaceInfo CreateInterfaceInfo(NetworkInterface networkInterface)
        {
            if (networkInterface == null)
                return null;

            string id = SafeString(delegate { return networkInterface.Id; });
            string name = SafeString(delegate { return networkInterface.Name; });
            string description = SafeString(delegate { return networkInterface.Description; });
            string type = SafeString(delegate { return networkInterface.NetworkInterfaceType.ToString(); });
            string physicalAddress = SafeString(delegate { return networkInterface.GetPhysicalAddress().ToString(); });
            bool isOperational = SafeBool(delegate { return networkInterface.OperationalStatus == OperationalStatus.Up; });
            bool supportsMulticast = SafeBool(delegate { return networkInterface.SupportsMulticast; });
            long speed = SafeLong(delegate { return networkInterface.Speed; });

            List<LocalNetworkAddressInfo> addresses = new List<LocalNetworkAddressInfo>();
            IPInterfaceProperties properties = null;
            try
            {
                properties = networkInterface.GetIPProperties();
            }
            catch
            {
                // GuardrailAllow: SilentCatch - interface property probing is best-effort; missing details produce an interface without addresses.
            }

            if (properties != null)
            {
                UnicastIPAddressInformationCollection unicastAddresses = properties.UnicastAddresses;
                foreach (UnicastIPAddressInformation unicast in unicastAddresses)
                {
                    if (unicast == null || unicast.Address == null || unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    IPAddress mask = null;
                    try { mask = unicast.IPv4Mask; }
                    catch
                    {
                        // GuardrailAllow: SilentCatch - some platforms do not expose IPv4Mask; null mask is an accepted fallback.
                    }
                    addresses.Add(new LocalNetworkAddressInfo(
                        unicast.Address,
                        mask,
                        name,
                        id,
                        description,
                        type,
                        isOperational,
                        supportsMulticast,
                        speed,
                        physicalAddress));
                }
            }

            return new LocalNetworkInterfaceInfo(
                id,
                name,
                description,
                type,
                isOperational,
                supportsMulticast,
                speed,
                physicalAddress,
                addresses.ToArray());
        }

        private static int Score(LocalNetworkAddressInfo address)
        {
            if (address == null || address.Address == null)
                return int.MinValue;

            int score = 0;
            if (address.IsOperational)
                score += 1000;
            if (!address.IsLoopback)
                score += 500;
            if (address.IsPrivate)
                score += 250;
            if (address.IsLinkLocal)
                score -= 100;
            if (address.SupportsMulticast)
                score += 25;
            if (address.InterfaceType == "Ethernet")
                score += 20;
            if (address.InterfaceType == "Wireless80211")
                score += 15;
            if (address.Speed > 0)
                score += (int)Math.Min(50, address.Speed / 10000000L);
            return score;
        }

        private delegate string StringGetter();
        private delegate bool BoolGetter();
        private delegate long LongGetter();

        private static string SafeString(StringGetter getter)
        {
            try { return getter() ?? string.Empty; } catch { return string.Empty; }
        }

        private static bool SafeBool(BoolGetter getter)
        {
            try { return getter(); } catch { return false; }
        }

        private static long SafeLong(LongGetter getter)
        {
            try { return getter(); } catch { return 0; }
        }
    }
}
