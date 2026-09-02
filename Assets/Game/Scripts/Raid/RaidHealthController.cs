using OfflineExtraction.Core;
using UnityEngine;

namespace OfflineExtraction.Raid
{
    public sealed class RaidHealthController : MonoBehaviour
    {
        private float nextBleedTick;

        private void Update()
        {
            PlayerVitals vitals = RaidContext.Loadout?.vitals;
            if (vitals == null || RaidBootstrap.IsPaused || Time.time < nextBleedTick) return;
            nextBleedTick = Time.time + 5f;
            if (vitals.bleedingParts == null || vitals.bleedingParts.Count == 0) return;
            vitals.destroyedParts ??= new System.Collections.Generic.List<string>();
            foreach (string part in new System.Collections.Generic.List<string>(vitals.bleedingParts))
            {
                ApplyRawDamage(vitals, part, Mathf.Max(1, Mathf.RoundToInt(MaximumFor(part) * .10f)));
                if (ValueFor(vitals, part) <= 0)
                {
                    if (!vitals.destroyedParts.Contains(part)) vitals.destroyedParts.Add(part);
                    vitals.bleedingParts.Remove(part);
                }
            }
            CheckDeath(vitals);
        }

        public void ApplyDamage(string bodyPart, int amount, bool causesBleeding = false, bool causesFracture = false)
        {
            PlayerVitals vitals = RaidContext.Loadout?.vitals;
            if (vitals == null || amount <= 0) return;
            ApplyRawDamage(vitals, bodyPart, amount);
            vitals.bleedingParts ??= new System.Collections.Generic.List<string>();
            vitals.fracturedParts ??= new System.Collections.Generic.List<string>();
            if (causesBleeding && !vitals.bleedingParts.Contains(bodyPart)) vitals.bleedingParts.Add(bodyPart);
            if (causesFracture && !vitals.fracturedParts.Contains(bodyPart)) vitals.fracturedParts.Add(bodyPart);
            CheckDeath(vitals);
        }

        private static void ApplyRawDamage(PlayerVitals v, string part, int amount)
        {
            switch (part)
            {
                case "head": v.head = Mathf.Max(0, v.head - amount); break;
                case "chest": v.chest = Mathf.Max(0, v.chest - amount); break;
                case "abdomen": v.abdomen = Mathf.Max(0, v.abdomen - amount); break;
                case "rightArm": v.rightArm = Mathf.Max(0, v.rightArm - amount); break;
                case "leftArm": v.leftArm = Mathf.Max(0, v.leftArm - amount); break;
                case "rightLeg": v.rightLeg = Mathf.Max(0, v.rightLeg - amount); break;
                case "leftLeg": v.leftLeg = Mathf.Max(0, v.leftLeg - amount); break;
            }
        }

        private static int MaximumFor(string part) => part switch
        {
            "head" => 35, "chest" => 85, "abdomen" => 70,
            "rightArm" or "leftArm" => 60,
            "rightLeg" or "leftLeg" => 65,
            _ => 1
        };

        private static int ValueFor(PlayerVitals v, string part) => part switch
        {
            "head" => v.head, "chest" => v.chest, "abdomen" => v.abdomen,
            "rightArm" => v.rightArm, "leftArm" => v.leftArm,
            "rightLeg" => v.rightLeg, "leftLeg" => v.leftLeg,
            _ => 0
        };

        private static void CheckDeath(PlayerVitals vitals)
        {
            if (vitals.head > 0 && vitals.chest > 0) return;
            RaidBootstrap bootstrap = FindFirstObjectByType<RaidBootstrap>();
            if (bootstrap != null) bootstrap.FailRaid();
        }
    }
}
