using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    public class SaveDiscoveryService
    {
        private static readonly byte[] XorKey = { 172, 242, 115, 58, 254, 222, 170, 33, 48, 13, 167, 21, 139, 109, 74, 186, 171 };
        private static readonly byte[] XorOrder = { 0, 2, 4, 1, 6, 15, 13, 16, 8, 3, 12, 10, 5, 9, 11, 7, 14 };

        public List<SaveSlotInfo> DiscoverSaves(string gamePath)
        {
            var results = new List<SaveSlotInfo>();
            if (string.IsNullOrEmpty(gamePath) || !File.Exists(gamePath))
                return results;

            string gameDir = Path.GetDirectoryName(gamePath);
            
            // 1. Discover Vanilla Saves (Slots 1-3)
            string vanillaSavesDir = Path.Combine(gameDir, "saves");
            for (int i = 1; i <= 3; i++)
            {
                string fileName = $"savedata_0{i}.dat";
                string fullPath = Path.Combine(vanillaSavesDir, fileName);
                if (File.Exists(fullPath))
                {
                    var info = ReadVanillaSave(fullPath, i);
                    if (info != null) results.Add(info);
                }
            }

            // 2. Discover Custom Saves (Slots 4+)
            // Path: <GameRoot>/mods/ModAPI/Saves/Standard/Slot_X/SaveData.xml
            string customSavesRoot = Path.Combine(gameDir, Path.Combine("mods", Path.Combine("ModAPI", "Saves")));
            string standardSavesDir = Path.Combine(customSavesRoot, "Standard");
            
            if (Directory.Exists(standardSavesDir))
            {
                var slotDirs = Directory.GetDirectories(standardSavesDir, "Slot_*");
                foreach (var slotDir in slotDirs)
                {
                    string dirName = Path.GetFileName(slotDir);
                    if (int.TryParse(dirName.Substring(5), out int absoluteSlot))
                    {
                        if (absoluteSlot < 4) continue; // Should be handled by vanilla if they were there, but API starts custom at 4

                        string xmlPath = Path.Combine(slotDir, "SaveData.xml");
                        if (File.Exists(xmlPath))
                        {
                            var info = ReadCustomSave(xmlPath, absoluteSlot);
                            if (info != null) results.Add(info);
                        }
                    }
                }
            }

            // Sort by absolute slot
            results.Sort((a, b) => a.AbsoluteSlot.CompareTo(b.AbsoluteSlot));
            return results;
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
            catch { return null; }
        }

        private SaveSlotInfo ReadCustomSave(string path, int slot)
        {
            try
            {
                string xmlContent = File.ReadAllText(path);
                return ParseSaveXml(xmlContent, slot, true);
            }
            catch { return null; }
        }

        private SaveSlotInfo ParseSaveXml(string xmlContent, int slot, bool isCustom)
        {
            try
            {
                // We use a simplified XML parsing or regex to avoid issues with fragmented XML if any
                // But SaveData.xml should be valid XML.
                var info = new SaveSlotInfo { AbsoluteSlot = slot, IsCustom = isCustom };
                
                XmlDocument doc = new XmlDocument();
                // Sheltered XML usually has a root element like <SaveData>
                doc.LoadXml(xmlContent);

                // Replicating SaveData.cs extraction logic
                // The fields are under root/SaveInfo
                XmlNode infoNode = doc.SelectSingleNode("/root/SaveInfo");
                if (infoNode != null)
                {
                    info.FamilyName = infoNode.SelectSingleNode("familyName")?.InnerText ?? "Unknown";
                    
                    string daysStr = infoNode.SelectSingleNode("daysSurvived")?.InnerText;
                    if (int.TryParse(daysStr, out int days)) info.DaysSurvived = days;
                    
                    info.SaveTime = infoNode.SelectSingleNode("timestamp")?.InnerText ?? "Unknown";
                }

                return info;
            }
            catch 
            {
                // Fallback for corrupt or weird XML
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
                if (keyIndex >= XorOrder.Length) keyIndex = 0;
            }
            return decrypted;
        }
    }
}
