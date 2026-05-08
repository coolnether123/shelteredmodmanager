using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShelteredAPI.Networking.Locations
{
    internal static class ShelteredLocationLootDiagnostics
    {
        public static string ToLootSummaryJson(IList<LootItemRecord> records)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\"items\":[");
            for (int i = 0; records != null && i < records.Count; i++)
            {
                LootItemRecord record = records[i];
                if (record == null)
                    continue;

                if (builder[builder.Length - 1] != '[')
                    builder.Append(",");

                builder.Append("{");
                if (record.VanillaItemTypeInt.HasValue)
                    builder.Append("\"vanillaItemTypeInt\":").Append(record.VanillaItemTypeInt.Value.ToString(CultureInfo.InvariantCulture));
                else
                    builder.Append("\"customItemId\":\"").Append(EscapeJson(record.CustomItemId)).Append("\"");
                builder.Append(",\"count\":").Append(record.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"source\":\"").Append(EscapeJson(record.Source)).Append("\"");
                builder.Append("}");
            }

            builder.Append("]}");
            return builder.ToString();
        }

        internal static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\')
                    builder.Append("\\\\");
                else if (c == '"')
                    builder.Append("\\\"");
                else if (c == '\r')
                    builder.Append("\\r");
                else if (c == '\n')
                    builder.Append("\\n");
                else if (c == '\t')
                    builder.Append("\\t");
                else
                    builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
