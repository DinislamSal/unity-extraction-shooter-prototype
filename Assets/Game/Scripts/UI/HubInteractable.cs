using UnityEngine;

namespace OfflineExtraction.UI
{
    public enum HubAction { Storage, Armory, Bunker, Raid, Chair, Radio, Door, Workbench }

    public sealed class HubInteractable : MonoBehaviour
    {
        public HubAction action;
        public string prompt;
        public Transform seatPoint;
        private bool doorOpen;

        public void Use(HubInteraction interaction)
        {
            LobbyPrototype lobby = FindFirstObjectByType<LobbyPrototype>();
            switch (action)
            {
                case HubAction.Storage: lobby?.OpenFromShelter(1); break;
                case HubAction.Armory: lobby?.OpenFromShelter(2); break;
                case HubAction.Bunker: lobby?.OpenFromShelter(5); break;
                case HubAction.Raid: lobby?.BeginRaidFromShelter(); break;
                case HubAction.Chair: interaction.ToggleSeat(seatPoint); break;
                case HubAction.Radio: interaction.ToggleRadio(); break;
                case HubAction.Door:
                    transform.Rotate(0f, doorOpen ? -100f : 100f, 0f, Space.World);
                    doorOpen = !doorOpen;
                    prompt = doorOpen ? "закрыть дверь" : "открыть дверь";
                    break;
                case HubAction.Workbench: interaction.OpenWorkbench(); break;
            }
        }
    }
}
