using UnityEngine;
using UnityEngine.InputSystem;
using OfflineExtraction.Core;

namespace OfflineExtraction.UI
{
    public sealed class HubInteraction : MonoBehaviour
    {
        public float distance = 2.4f;
        private Camera view;
        private HubInteractable target;
        private CharacterController controller;
        private bool seated;
        private Vector3 standingPosition;
        private Quaternion standingRotation;
        private Vector3 standingCameraLocalPosition;
        private bool radioOn;
        private float targetSeenUntil;
        private HubWorkbenchUI workbench;
        private GUIStyle promptStyle;
        private HumanoidMotionDriver body;

        public bool IsSeated => seated;

        private void Awake()
        {
            view = GetComponentInChildren<Camera>();
            controller = GetComponent<CharacterController>();
            workbench = GetComponent<HubWorkbenchUI>();
            body = GetComponent<HumanoidMotionDriver>();
        }

        private void Update()
        {
            LobbyPrototype panel = FindFirstObjectByType<LobbyPrototype>();
            if ((panel != null && panel.IsVisible) || HubWorkbenchUI.IsOpen) { target = null; return; }
            if (seated)
            {
                if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ToggleSeat(null);
                return;
            }
            HubInteractable found = null;
            float nearest = float.MaxValue;
            if (view != null)
            {
                // Объёмный луч не теряет кресло и другие крупные объекты при
                // небольшом движении камеры. Перебираем все попадания, включая триггеры.
                foreach (RaycastHit hit in Physics.SphereCastAll(view.transform.position, .22f, view.transform.forward, distance, ~0, QueryTriggerInteraction.Collide))
                {
                    HubInteractable candidate = hit.collider.GetComponentInParent<HubInteractable>();
                    if (candidate == null || hit.distance >= nearest) continue;
                    found = candidate; nearest = hit.distance;
                }
            }
            if (found != null) { target = found; targetSeenUntil = Time.unscaledTime + .18f; }
            else if (Time.unscaledTime > targetSeenUntil) target = null;
            if (target != null && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) target.Use(this);
        }

        public void ToggleSeat(Transform point)
        {
            if (!seated && point != null)
            {
                standingPosition = transform.position; standingRotation = transform.rotation;
                controller.enabled = false; transform.SetPositionAndRotation(point.position, point.rotation);
                if (view != null)
                {
                    standingCameraLocalPosition = view.transform.localPosition;
                    // Камера закрепляется на головном якоре модели.
                    view.transform.localPosition = Vector3.zero;
                }
                seated = true;
                if (body != null) body.seated = true;
            }
            else if (seated)
            {
                transform.SetPositionAndRotation(standingPosition, standingRotation);
                if (view != null) view.transform.localPosition = standingCameraLocalPosition;
                controller.enabled = true; seated = false;
                if (body != null) body.seated = false;
            }
        }

        public void ToggleRadio() => radioOn = !radioOn;
        public void OpenWorkbench()
        {
            workbench ??= GetComponent<HubWorkbenchUI>();
            workbench?.Open();
        }

        private void OnGUI()
        {
            LobbyPrototype panel = FindFirstObjectByType<LobbyPrototype>();
            if (panel != null && panel.IsVisible) return;
            promptStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, normal = { textColor = Color.white } };
            GUI.Label(new Rect(Screen.width * .5f - 3, Screen.height * .5f - 4, 6, 8), "+", promptStyle);
            string text = seated ? "F — встать" : target != null ? $"F — {target.prompt}" : radioOn ? "Радио включено" : "";
            if (!string.IsNullOrEmpty(text)) GUI.Label(new Rect(Screen.width * .5f - 220, Screen.height - 105, 440, 34), text, promptStyle);
        }
    }
}
