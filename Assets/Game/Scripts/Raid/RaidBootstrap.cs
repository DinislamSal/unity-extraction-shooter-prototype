using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using OfflineExtraction.Core;

namespace OfflineExtraction.Raid
{
    public sealed class RaidBootstrap : MonoBehaviour
    {
        private const float ExtractionDuration = 10f;
        private const float ExtractionRadius = 2.5f;
        private const float RaidDuration = 600f;
        public static bool IsPaused { get; private set; }
        public static bool IsDeploymentLocked { get; private set; }
        private RaidInventoryUI inventory;
        private RaidLootContainer highlighted;
        private RaidDroppedItem highlightedItem;
        private bool settingsOpen;
        private GUIStyle prompt, menuTitle;
        private float raidStartedAt;
        private float raidEndsAt;
        private bool raidTimerStarted;
        private float deploymentEndsAt;
        private Transform extractionPoint;
        private float extractionProgress;
        private bool raidFinished;
        private bool raidSurvived;
        private string raidResult;

        private void Awake()
        {
            IsPaused = false;
            IsDeploymentLocked = true;
            deploymentEndsAt = Time.unscaledTime + 3.2f;
            raidStartedAt = Time.time;
            RaidBodyFigure.Initialize();
            inventory = gameObject.AddComponent<RaidInventoryUI>();
            RaidPlayerController player = FindFirstObjectByType<RaidPlayerController>();
            if (player != null && player.GetComponent<RaidWeaponController>() == null) player.gameObject.AddComponent<RaidWeaponController>();
            if (player != null && player.GetComponent<RaidHealthController>() == null) player.gameObject.AddComponent<RaidHealthController>();
            CreateTestContainer(new Vector3(0f, .6f, -9.5f), "ТЕХНИЧЕСКИЙ ЯЩИК");
            CreateTestContainer(new Vector3(4f, .6f, -5f), "АРМЕЙСКИЙ КОНТЕЙНЕР");
            CreateTestBot(new Vector3(-14f, .05f, -4f));
            // Свободный северо-восточный угол: вне Telecenter B и других тестовых блоков.
            CreateExtractionPoint(new Vector3(16.5f, .06f, 16.5f));
        }

        private void OnDestroy() { IsPaused = false; IsDeploymentLocked = false; Time.timeScale = 1f; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (IsDeploymentLocked && Time.unscaledTime >= deploymentEndsAt)
            {
                IsDeploymentLocked = false;
                raidTimerStarted = true;
                raidStartedAt = Time.time;
                raidEndsAt = raidStartedAt + RaidDuration;
            }
            if (raidFinished) return;
            if (raidTimerStarted && Time.time >= raidEndsAt)
            {
                FinishRaid(false, "ВРЕМЯ РЕЙДА ИСТЕКЛО");
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (RaidInventoryUI.IsOpen) inventory.Close(); else SetPaused(!IsPaused);
                return;
            }
            UpdateInteraction();
            UpdateExtraction();
            if (!IsPaused && !RaidInventoryUI.IsOpen && highlighted != null && keyboard.fKey.wasPressedThisFrame) inventory.Toggle(highlighted);
            if (!IsPaused && !RaidInventoryUI.IsOpen && highlightedItem != null && keyboard.fKey.wasPressedThisFrame)
            {
                RaidDroppedItem picked = highlightedItem;
                if (inventory.PickUp(picked.item))
                {
                    highlightedItem = null;
                    Destroy(picked.gameObject);
                }
            }
        }

        private void UpdateInteraction()
        {
            RaidLootContainer target = null;
            RaidDroppedItem itemTarget = null;
            if (!IsPaused && !RaidInventoryUI.IsOpen)
            {
                Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
                if (camera != null)
                {
                    RaycastHit[] hits = Physics.SphereCastAll(camera.transform.position, .18f, camera.transform.forward, 2.625f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (RaycastHit hit in hits)
                    {
                        target = hit.collider.GetComponentInParent<RaidLootContainer>();
                        itemTarget = hit.collider.GetComponentInParent<RaidDroppedItem>();
                        if (target != null || itemTarget != null) break;
                    }
                }
            }
            if (target != highlighted)
            {
                if (highlighted != null) highlighted.SetHighlighted(false);
                highlighted = target;
                if (highlighted != null) highlighted.SetHighlighted(true);
            }
            if (itemTarget != highlightedItem)
            {
                if (highlightedItem != null) highlightedItem.SetHighlighted(false);
                highlightedItem = itemTarget;
                if (highlightedItem != null) highlightedItem.SetHighlighted(true);
            }
        }

        private void SetPaused(bool paused)
        {
            IsPaused = paused; settingsOpen = false; Time.timeScale = paused ? 0f : 1f;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = paused;
        }

        private void LeaveRaid() { FinishRaid(false); }

        private void FinishRaid(bool survived, string reason = null)
        {
            if (raidFinished) return;
            raidFinished = true; raidSurvived = survived; IsPaused = false; Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var session = new GameSession(); session.Initialize();
            raidResult = session.CompleteRaid(RaidContext.Loadout, survived, Mathf.Max(0f, Time.time - raidStartedAt) / 60f);
            if (!string.IsNullOrEmpty(reason)) raidResult = reason + " · " + raidResult;
        }

        public void FailRaid() => FinishRaid(false);

        private void ReturnToLobby()
        {
            RaidContext.Clear(); SceneManager.LoadScene("SampleScene");
        }

        private void CreateExtractionPoint(Vector3 position)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "ЭВАКУАЦИЯ · СЛУЖЕБНЫЙ ДВОР"; marker.transform.position = position; marker.transform.localScale = new Vector3(2.2f, .03f, 2.2f);
            Renderer renderer = marker.GetComponent<Renderer>(); if (renderer != null) renderer.material.color = new Color(.38f, .035f, .025f, .7f);
            Collider collider = marker.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            extractionPoint = marker.transform;
            GameObject smokeObject = new("КРАСНЫЙ ДЫМ ЭВАКУАЦИИ"); smokeObject.transform.position = position + Vector3.up * .1f;
            ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = smoke.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f); main.startSpeed = new ParticleSystem.MinMaxCurve(.45f, 1.05f); main.startSize = new ParticleSystem.MinMaxCurve(.8f, 1.65f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(.72f, .025f, .018f, .72f), new Color(.32f, .01f, .008f, .38f)); main.maxParticles = 220; main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = smoke.emission; emission.rateOverTime = 24f;
            ParticleSystem.ShapeModule shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = .42f;
            ParticleSystem.ColorOverLifetimeModule color = smoke.colorOverLifetime; color.enabled = true; Gradient gradient = new(); gradient.SetKeys(new[] { new GradientColorKey(new Color(.8f,.02f,.015f), 0f), new GradientColorKey(new Color(.28f,.015f,.01f), 1f) }, new[] { new GradientAlphaKey(.72f, 0f), new GradientAlphaKey(.42f, .55f), new GradientAlphaKey(0f, 1f) }); color.color = gradient;
            ParticleSystemRenderer smokeRenderer = smoke.GetComponent<ParticleSystemRenderer>(); smokeRenderer.renderMode = ParticleSystemRenderMode.Billboard; smokeRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        private void UpdateExtraction()
        {
            if (IsPaused || RaidInventoryUI.IsOpen || IsDeploymentLocked || extractionPoint == null) { extractionProgress = 0f; return; }
            RaidPlayerController player = FindFirstObjectByType<RaidPlayerController>();
            if (player == null || HorizontalDistance(player.transform.position, extractionPoint.position) > ExtractionRadius)
            {
                extractionProgress = 0f;
                return;
            }
            extractionProgress += Time.deltaTime;
            if (extractionProgress >= ExtractionDuration) FinishRaid(true);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static void CreateTestContainer(Vector3 position, string title)
        {
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = title; crate.transform.position = position; crate.transform.localScale = new Vector3(1.5f, 1.2f, 1f);
            crate.GetComponent<Renderer>().material.color = new Color(.22f, .16f, .08f);
            RaidLootContainer container = crate.AddComponent<RaidLootContainer>(); container.displayName = title; container.FillTestLoot();
        }

        private static void CreateTestBot(Vector3 position)
        {
            GameObject bot = new("БОТ · ОХРАННИК ТЕЛЕЦЕНТРА"); bot.transform.position = position;
            bot.AddComponent<CharacterController>(); bot.AddComponent<RaidBotController>();
        }

        private void OnGUI()
        {
            prompt ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            menuTitle ??= new GUIStyle(prompt) { fontSize = 28 };
            if (!IsPaused && !RaidInventoryUI.IsOpen) DrawRaidHud();
            if (raidFinished) { DrawRaidResult(); return; }
            if (!IsPaused && !RaidInventoryUI.IsOpen) GUI.Label(new Rect(Screen.width * .5f - 12, Screen.height * .5f - 16, 24, 32), "+", prompt);
            if (!IsPaused && !RaidInventoryUI.IsOpen && highlighted != null)
                GUI.Label(new Rect(Screen.width * .5f - 220, Screen.height * .5f + 62, 440, 34), highlighted.isCorpse ? "F — ОБЫСКАТЬ ТЕЛО" : $"F — ОБЫСКАТЬ · {highlighted.displayName}", prompt);
            if (!IsPaused && !RaidInventoryUI.IsOpen && highlightedItem != null)
                GUI.Label(new Rect(Screen.width * .5f - 190, Screen.height * .5f + 62, 380, 34), $"F — ПОДОБРАТЬ · {ItemCatalog.Get(highlightedItem.item.definitionId).name}", prompt);
            if (!IsPaused) return;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            Rect menu = new(Screen.width * .5f - 210, Screen.height * .5f - 220, 420, 440); GUI.Box(menu, GUIContent.none);
            GUI.Label(new Rect(menu.x + 30, menu.y + 28, menu.width - 60, 48), "РЕЙД ПРИОСТАНОВЛЕН", menuTitle);
            if (!settingsOpen)
            {
                if (GUI.Button(new Rect(menu.x + 55, menu.y + 105, 310, 58), "ПРОДОЛЖИТЬ")) SetPaused(false);
                if (GUI.Button(new Rect(menu.x + 55, menu.y + 180, 310, 58), "НАСТРОЙКИ")) settingsOpen = true;
                if (GUI.Button(new Rect(menu.x + 55, menu.y + 285, 310, 58), "ПОКИНУТЬ РЕЙД")) LeaveRaid();
            }
            else
            {
                RaidPlayerController controller = FindFirstObjectByType<RaidPlayerController>();
                GUI.Label(new Rect(menu.x + 55, menu.y + 120, 310, 30), "ЧУВСТВИТЕЛЬНОСТЬ МЫШИ", prompt);
                if (controller != null) controller.mouseSensitivity = GUI.HorizontalSlider(new Rect(menu.x + 65, menu.y + 175, 290, 24), controller.mouseSensitivity, .03f, .35f);
                if (GUI.Button(new Rect(menu.x + 55, menu.y + 285, 310, 58), "НАЗАД")) settingsOpen = false;
            }
        }

        private void DrawRaidResult()
        {
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            Rect result = new(Screen.width * .5f - 320, Screen.height * .5f - 170, 640, 340); GUI.Box(result, GUIContent.none);
            GUI.Label(new Rect(result.x + 30, result.y + 35, result.width - 60, 52), raidSurvived ? "ЭВАКУАЦИЯ УСПЕШНА" : "РЕЙД ЗАВЕРШЁН", menuTitle);
            GUI.Label(new Rect(result.x + 35, result.y + 115, result.width - 70, 70), raidResult, new GUIStyle(prompt) { fontSize = 16, wordWrap = true });
            if (GUI.Button(new Rect(result.x + 150, result.y + 235, 340, 58), "ВЕРНУТЬСЯ В ШТАБ")) ReturnToLobby();
        }

        private void DrawRaidHud()
        {
            RaidPlayerController player = FindFirstObjectByType<RaidPlayerController>();
            float yaw = player == null ? 0f : player.transform.eulerAngles.y;
            string[] directions = { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
            string direction = directions[Mathf.RoundToInt(yaw / 45f) % 8];
            GUIStyle small = new(prompt) { fontSize = 13 };
            GUI.Label(new Rect(Screen.width * .5f - 150, 10, 300, 26), $"{direction}   {Mathf.RoundToInt(yaw):000}°", small);
            int remaining = raidTimerStarted ? Mathf.Max(0, Mathf.CeilToInt(raidEndsAt - Time.time)) : Mathf.CeilToInt(RaidDuration);
            GUI.Label(new Rect(Screen.width - 190, 12, 170, 24), $"РЕЙД  {remaining / 60:00}:{remaining % 60:00}", new GUIStyle(small) { alignment = TextAnchor.MiddleRight });

            if (extractionPoint != null && player != null && HorizontalDistance(player.transform.position, extractionPoint.position) <= ExtractionRadius)
            {
                float ratio = Mathf.Clamp01(extractionProgress / ExtractionDuration);
                GUI.Label(new Rect(Screen.width * .5f - 180, 38, 360, 24), $"ЭВАКУАЦИЯ · {Mathf.CeilToInt(ExtractionDuration - extractionProgress)} С", small);
                Color old = GUI.color; GUI.color = new Color(.10f, .12f, .12f, .9f); GUI.DrawTexture(new Rect(Screen.width * .5f - 150, 66, 300, 6), Texture2D.whiteTexture);
                GUI.color = new Color(.15f, .78f, .34f); GUI.DrawTexture(new Rect(Screen.width * .5f - 150, 66, 300 * ratio, 6), Texture2D.whiteTexture); GUI.color = old;
            }

            PlayerVitals v = RaidContext.Loadout?.vitals ?? new PlayerVitals();
            RaidBodyFigure.Draw(new Rect(18, Screen.height - 178, 82, 150), v);
            bool hasDamage = v.head < 35 || v.chest < 85 || v.abdomen < 70 ||
                             v.rightArm < 60 || v.leftArm < 60 || v.rightLeg < 65 || v.leftLeg < 65;
            float injuryY = Screen.height - 162;
            if (hasDamage)
            {
                DrawInjuryIcon(new Rect(104, injuryY, 25, 25), "+", new Color(.55f, .92f, .62f));
                injuryY += 30f;
            }
            if (v.bleedingParts != null && v.bleedingParts.Count > 0)
                DrawInjuryIcon(new Rect(104, injuryY, 25, 25), "♦", new Color(.92f, .18f, .16f));
            if (player != null)
            {
                Rect sprint = new(Screen.width * .5f - 110, Screen.height - 38, 220, 5);
                Rect breath = new(Screen.width * .5f - 110, Screen.height - 25, 220, 5);
                GUI.Label(new Rect(sprint.x - 76, sprint.y - 7, 70, 16), "БЕГ", new GUIStyle(small) { fontSize = 9, alignment = TextAnchor.MiddleRight });
                GUI.Label(new Rect(breath.x - 76, breath.y - 7, 70, 16), "ДЫХАНИЕ", new GUIStyle(small) { fontSize = 9, alignment = TextAnchor.MiddleRight });
                Color old = GUI.color; GUI.color = new Color(.12f, .15f, .15f); GUI.DrawTexture(sprint, Texture2D.whiteTexture); GUI.DrawTexture(breath, Texture2D.whiteTexture);
                GUI.color = new Color(.82f, .78f, .60f); GUI.DrawTexture(new Rect(sprint.x, sprint.y, sprint.width * player.SprintStamina / 100f, sprint.height), Texture2D.whiteTexture);
                GUI.color = new Color(.42f, .72f, .9f); GUI.DrawTexture(new Rect(breath.x, breath.y, breath.width * player.BreathStamina / 100f, breath.height), Texture2D.whiteTexture); GUI.color = old;
            }

            if (!IsDeploymentLocked) return;
            float deploymentRemaining = Mathf.Max(0f, deploymentEndsAt - Time.unscaledTime);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.Label(new Rect(Screen.width * .5f - 260, Screen.height * .5f - 76, 520, 42), "ПОДГОТОВКА К ОПЕРАЦИИ", menuTitle);
            GUI.Label(new Rect(Screen.width * .5f - 90, Screen.height * .5f - 18, 180, 72), Mathf.CeilToInt(deploymentRemaining).ToString(), new GUIStyle(menuTitle) { fontSize = 54 });
        }

        private static void DrawInjuryIcon(Rect rect, string symbol, Color color)
        {
            Color old = GUI.color;
            GUI.color = new Color(.04f, .055f, .055f, .9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.Label(rect, symbol, new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            });
            GUI.color = old;
        }

    }
}
