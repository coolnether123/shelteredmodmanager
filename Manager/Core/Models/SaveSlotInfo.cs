using System;

namespace Manager.Core.Models
{
    public class SaveSlotInfo
    {
        public int AbsoluteSlot { get; set; }
        public string FamilyName { get; set; }
        public int DaysSurvived { get; set; }
        public string SaveTime { get; set; }
        public bool IsCustom { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(FamilyName))
                return $"Slot {AbsoluteSlot} (Empty)";
            
            return $"Slot {AbsoluteSlot}: {FamilyName} (Day {DaysSurvived}) - {SaveTime}";
        }
    }
}
