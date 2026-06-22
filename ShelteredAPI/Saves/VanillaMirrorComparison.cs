using System;

namespace ShelteredAPI.Saves
{
    internal enum VanillaMirrorComparisonStatus
    {
        MissingVanilla,
        MissingMirror,
        InSync,
        Diverged
    }

    internal sealed class VanillaMirrorComparisonResult
    {
        public VanillaMirrorComparisonStatus Status;
        public int SlotNumber;
        public SaveManager.SaveType SaveType;
        public string VanillaPath;
        public string MirrorPath;
        public byte[] VanillaXmlBytes;
        public byte[] MirrorXmlBytes;
        public uint SourceVanillaCrc32;
        public DateTime SourceVanillaLastWriteUtc;
        public string Error;

        public bool HasVanillaXml
        {
            get { return VanillaXmlBytes != null && VanillaXmlBytes.Length > 0; }
        }

        public bool HasMirrorXml
        {
            get { return MirrorXmlBytes != null && MirrorXmlBytes.Length > 0; }
        }
    }
}
