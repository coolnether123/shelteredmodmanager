using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Manager.Core.Games.Models;
using Manager.Core.Models;

namespace Manager.Core.Games.Saves
{
    public sealed class ShelteredSaveDiscoveryStrategy : ISaveDiscoveryStrategy
    {
        private static readonly byte[] XorKey = { 172, 242, 115, 58, 254, 222, 170, 33, 48, 13, 167, 21, 139, 109, 74, 186, 171 };
        private static readonly byte[] XorOrder = { 0, 2, 4, 1, 6, 15, 13, 16, 8, 3, 12, 10, 5, 9, 11, 7, 14 };

        public List<SaveSlotInfo> DiscoverSaves(GameProfile profile, string gamePath)
        {
            List<SaveSlotInfo> results = new List<SaveSlotInfo>();
            if (string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath))
                return results;

            string gameDir = Path.GetDirectoryName(gamePath);
            DiscoverVanillaSaves(results, gameDir);
            DiscoverModApiSaves(results, gameDir);

            results.Sort((a, b) => a.AbsoluteSlot.CompareTo(b.AbsoluteSlot));
            return results;
        }

        private void DiscoverVanillaSaves(List<SaveSlotInfo> results, string gameDir)
        {
            string vanillaSavesDir = Path.Combine(gameDir, "saves");
            for (int i = 1; i <= 3; i++)
            {
                string fileName = "savedata_0" + i + ".dat";
                string fullPath = Path.Combine(vanillaSavesDir, fileName);
                if (!File.Exists(fullPath))
                    continue;

                SaveSlotInfo info = ReadVanillaSave(fullPath, i);
                if (info != null)
                    results.Add(info);
            }
        }

        private void DiscoverModApiSaves(List<SaveSlotInfo> results, string gameDir)
        {
            string customSavesRoot = Path.Combine(gameDir, Path.Combine("mods", Path.Combine("ModAPI", "Saves")));
            string standardSavesDir = Path.Combine(customSavesRoot, "Standard");
            if (!Directory.Exists(standardSavesDir))
                return;

            string[] slotDirs = Directory.GetDirectories(standardSavesDir, "Slot_*");
            for (int i = 0; i < slotDirs.Length; i++)
            {
                string slotDir = slotDirs[i];
                string dirName = Path.GetFileName(slotDir);
                int absoluteSlot;
                if (string.IsNullOrEmpty(dirName) || dirName.Length <= 5 || !int.TryParse(dirName.Substring(5), out absoluteSlot))
                    continue;

                if (absoluteSlot < 4)
                    continue;

                string xmlPath = Path.Combine(slotDir, "SaveData.xml");
                if (!File.Exists(xmlPath))
                    continue;

                SaveSlotInfo info = ReadCustomSave(xmlPath, absoluteSlot);
                if (info != null)
                    results.Add(info);
            }
        }

        private SaveSlotInfo ReadVanillaSave(string path, int slot)
        {
            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] decrypted = Decrypt(encrypted);
                string xmlContent = Encoding.UTF8.GetString(decrypted);
                return ParseSaveXml(xmlContent, slot, false);
            }
            catch
            {
                return null;
            }
        }

        private SaveSlotInfo ReadCustomSave(string path, int slot)
        {
            try
            {
                string xmlContent = File.ReadAllText(path);
                return ParseSaveXml(xmlContent, slot, true);
            }
            catch
            {
                return null;
            }
        }

        private SaveSlotInfo ParseSaveXml(string xmlContent, int slot, bool isCustom)
        {
            try
            {
                SaveSlotInfo info = new SaveSlotInfo { AbsoluteSlot = slot, IsCustom = isCustom };

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmlContent);

                XmlNode infoNode = doc.SelectSingleNode("/root/SaveInfo");
                if (infoNode != null)
                {
                    XmlNode familyNode = infoNode.SelectSingleNode("familyName");
                    info.FamilyName = familyNode != null ? familyNode.InnerText : "Unknown";

                    XmlNode daysNode = infoNode.SelectSingleNode("daysSurvived");
                    int days;
                    if (daysNode != null && int.TryParse(daysNode.InnerText, out days))
                        info.DaysSurvived = days;

                    XmlNode timestampNode = infoNode.SelectSingleNode("timestamp");
                    info.SaveTime = timestampNode != null ? timestampNode.InnerText : "Unknown";
                }

                return info;
            }
            catch
            {
                return new SaveSlotInfo { AbsoluteSlot = slot, IsCustom = isCustom, FamilyName = "Corrupt/Unreadable" };
            }
        }

        private byte[] Decrypt(byte[] data)
        {
            byte[] decrypted = new byte[data.Length];
            int keyIndex = 0;
            for (int i = 0; i < data.Length; i++)
            {
                decrypted[i] = (byte)(data[i] ^ XorKey[XorOrder[keyIndex++]]);
                if (keyIndex >= XorOrder.Length)
                    keyIndex = 0;
            }

            return decrypted;
        }
    }
}
