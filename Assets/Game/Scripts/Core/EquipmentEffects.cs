namespace OfflineExtraction.Core
{
    public readonly struct HearingProfile
    {
        public readonly float distanceMultiplier;
        public readonly float ambientNoiseReduction;
        public readonly float gunshotProtection;

        public HearingProfile(float distanceMultiplier, float ambientNoiseReduction, float gunshotProtection)
        {
            this.distanceMultiplier = distanceMultiplier;
            this.ambientNoiseReduction = ambientNoiseReduction;
            this.gunshotProtection = gunshotProtection;
        }
    }

    public static class EquipmentEffects
    {
        public static HearingProfile GetHearingProfile(PlayerData player)
        {
            ItemInstance equipped = player?.stash?.Find(item => item.equippedSlot == "headset");
            if (equipped == null) return new HearingProfile(1f, 0f, 0f);

            HeadsetData data = ItemCatalog.Get(equipped.definitionId).headset;
            return data == null
                ? new HearingProfile(1f, 0f, 0f)
                : new HearingProfile(data.hearingDistanceMultiplier, data.ambientNoiseReduction, data.gunshotProtection);
        }
    }
}
