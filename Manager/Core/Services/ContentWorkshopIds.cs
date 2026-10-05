using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Manager.Core.Services
{
    /// <summary>
    /// Builds the IDs and folder names the Content Workshop fills in from what
    /// people type. Everything here is a pure function of its input and uses
    /// invariant casing, so the same names give the same IDs on every PC.
    /// </summary>
    public static class ContentWorkshopIds
    {
        /// <summary>Owner used in a pack ID until an author is entered.</summary>
        public const string PlaceholderOwner = "yourname";

        private const int MaximumFolderNameLength = 80;

        private static readonly string[] ReservedDeviceNames = new string[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Lowercase ASCII letters and digits; accents are folded ("é" becomes
        /// "e") and any other run of characters becomes <paramref name="separator"/>.
        /// </summary>
        public static string Slug(string text, string separator)
        {
            StringBuilder builder = new StringBuilder();
            bool gap = false;
            string value = (text ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
            for (int i = 0; i < value.Length; i++)
            {
                char raw = value[i];
                if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
                    continue;
                string letters = Fold(char.ToLowerInvariant(raw));
                foreach (char c in letters)
                {
                    if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    {
                        if (gap && builder.Length > 0)
                            builder.Append(separator);
                        builder.Append(c);
                        gap = false;
                    }
                    else
                    {
                        gap = true;
                    }
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Pack IDs look like "author.packname", matching existing SMM mods,
        /// with a placeholder owner until an author is entered.
        /// </summary>
        public static string ModIdFor(string packName, string authors)
        {
            string firstAuthor = string.Empty;
            foreach (string person in (authors ?? string.Empty).Split(','))
            {
                if (person.Trim().Length > 0)
                {
                    firstAuthor = person;
                    break;
                }
            }
            string owner = Slug(firstAuthor, string.Empty);
            string pack = Slug(packName, string.Empty);
            return (owner.Length > 0 ? owner : PlaceholderOwner) + "." + (pack.Length > 0 ? pack : "contentpack");
        }

        /// <summary>
        /// <paramref name="baseId"/>, or baseId_2, baseId_3... the first one not
        /// in <paramref name="taken"/> (compared without regard to case).
        /// </summary>
        public static string Unique(string baseId, IEnumerable<string> taken)
        {
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (taken != null)
                foreach (string id in taken)
                    if (id != null)
                        used.Add(id);
            if (!used.Contains(baseId))
                return baseId;
            for (int n = 2; ; n++)
            {
                string candidate = baseId + "_" + n.ToString(CultureInfo.InvariantCulture);
                if (!used.Contains(candidate))
                    return candidate;
            }
        }

        /// <summary>
        /// A folder name Windows accepts, based on the pack name: invalid
        /// characters become spaces, trailing dots go, reserved device names get
        /// " pack" added before any dot, and long names are shortened.
        /// </summary>
        public static string FolderNameFor(string packName)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder clean = new StringBuilder();
            string value = (packName ?? string.Empty).Trim();
            for (int i = 0; i < value.Length; i++)
                clean.Append(Array.IndexOf(invalid, value[i]) >= 0 || char.IsControl(value[i]) ? ' ' : value[i]);
            string name = clean.ToString().Trim().TrimEnd('.', ' ');
            if (name.Length > MaximumFolderNameLength)
                name = name.Substring(0, MaximumFolderNameLength).TrimEnd('.', ' ');
            if (name.Length == 0)
                name = "Content Pack";
            // Windows reads "nul.old" as the NUL device too, so the word goes before the first dot.
            int dot = name.IndexOf('.');
            string device = (dot < 0 ? name : name.Substring(0, dot)).TrimEnd();
            if (Array.IndexOf(ReservedDeviceNames, device.ToUpperInvariant()) >= 0)
                name = device + " pack" + (dot < 0 ? string.Empty : name.Substring(dot));
            return name;
        }

        /// <summary>Letters that do not decompose into a base letter and an accent.</summary>
        private static string Fold(char c)
        {
            switch (c)
            {
                case 'ß': return "ss";
                case 'æ': return "ae";
                case 'œ': return "oe";
                case 'ø': return "o";
                case 'đ': return "d";
                case 'ł': return "l";
                case 'þ': return "th";
                case 'ı': return "i";
                default: return c.ToString();
            }
        }
    }
}
