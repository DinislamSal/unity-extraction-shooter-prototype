using System;
using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OfflineExtraction.UI
{
    public sealed class LobbyPrototype : MonoBehaviour
    {
        private const float W = 1600f, H = 900f;
        private const float EquipmentViewportX = 58f;
        private const float EquipmentViewportY = 294f;
        private const float EquipmentPanelWidth = 890f;
        private const int StashColumns = 12;
        private const int StashRows = 11;
        private readonly string[] tabs = { "ШТАБ", "ХРАНИЛИЩЕ", "МАГАЗИН", "НАВЫКИ", "ПРОФИЛЬ", "БУНКЕР" };
        private readonly AbilityDef[] abilities =
        {
            new("strength", "СИЛА", "Допустимый вес экипировки", "+3%"),
            new("endurance", "ВЫНОСЛИВОСТЬ", "Запас энергии и восстановление", "+3%"),
            new("weapons", "ОРУЖЕЙНАЯ ПОДГОТОВКА", "Контроль отдачи", "+2%"),
            new("medicine", "ПОЛЕВАЯ МЕДИЦИНА", "Скорость использования медикаментов", "+3%"),
            new("search", "ПОИСК", "Скорость осмотра контейнеров", "+4%"),
            new("stealth", "СКРЫТНОСТЬ", "Снижение громкости движения", "-2%")
        };

        private readonly Color orange = new(0.95f, 0.47f, 0.08f);
        private readonly Color panel = new(0.06f, 0.073f, 0.078f, 0.97f);
        private readonly Color panel2 = new(0.085f, 0.10f, 0.105f, 0.97f);
        private readonly Color line = new(0.24f, 0.27f, 0.27f, 0.8f);
        private GameSession session;
        private int tab;
        private Vector2 scroll;
        private Texture2D pixel, background, operatorImage, healthImage, itemAtlas;
        private Texture2D headMask, chestMask, abdomenMask, rightArmMask, leftArmMask, rightLegMask, leftLegMask;
        private GUIStyle logo, h1, h2, body, small, nav, navOn, action, danger;
        private ItemInstance draggedItem;
        private bool equipmentDragActive;
        private ItemInstance dragSnapshotItem;
        private int dragSnapshotX, dragSnapshotY;
        private bool dragSnapshotRotation, dragSnapshotFolded;
        private string dragSnapshotParent, dragSnapshotEquipmentSlot;
        private ItemInstance selectedItem;
        private ItemInstance openContainer;
        private Rect containerWindow = new(875, 155, 650, 650);
        private bool movingContainerWindow;
        private Vector2 containerWindowOffset;
        private Vector2 dragOffset;
        private Vector2 uiMouse;
        private string inventoryMessage = "F — свернуть · двойной щелчок — открыть";
        private bool unfoldingPreview;
        private int unfoldOriginalX;
        private int unfoldOriginalY;
        private string unfoldOriginalParent;
        private int characterPanelTab;
        private Vector2 equipmentScroll;
        private int shopCategory;
        private int shopQuantity = 1;
        private string selectedShopItemId = "rifle_mk1";
        private string shopMessage = "Выберите товар для покупки";
        private string shopNotice;
        private float shopNoticeUntil;
        private Vector2 shopScroll;
        private ItemInstance contextItem;
        private ItemInstance pendingSaleItem;
        private ItemInstance detailsItem;
        private bool showCharacteristics;
        private ItemInstance gunsmithItem;
        private List<string> gunsmithDraft = new();
        private string gunsmithMessage = "Выберите узел оружия для установки модификации";
        private Vector2 contextMenuPosition;
        private const float ContextMenuWidth = 250f;
        private string selectedBunkerModuleId = "generator";
        private int bunkerSection;
        public bool IsVisible { get; private set; }
        public PlayerData ShelterPlayer => session?.Player;

        private void Awake()
        {
            session = new GameSession();
            session.Initialize();
            pixel = Solid(Color.white);
            background = Resources.Load<Texture2D>("UI/lobby_background") ?? Backdrop();
            operatorImage = Resources.Load<Texture2D>("UI/operator_equipment");
            itemAtlas = Resources.Load<Texture2D>("UI/item_atlas");
            Texture2D dynamicBody = Resources.Load<Texture2D>("UI/body_health_dynamic");
            healthImage = dynamicBody != null ? ExtractBody(dynamicBody) : Resources.Load<Texture2D>("UI/body_health");
            CreateBodyMasks();
        }

        private void OnApplicationQuit() => session?.Save();
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "SampleScene") Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (!IsVisible) return;
            Styles();
            float s = Mathf.Min(Screen.width / W, Screen.height / H);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * s) / 2, (Screen.height - H * s) / 2), Quaternion.identity, Vector3.one * s);
            // IMGUI already reports the event position in the current GUI coordinate space.
            // Applying the inverse matrix here offset hit-testing from the visible items.
            uiMouse = Event.current.mousePosition;
            GUI.DrawTexture(new Rect(0, 0, W, H), background, ScaleMode.ScaleAndCrop);
            if (tab == 0)
            {
                CinematicHeader();
                CinematicLobby();
            }
            else
            {
                Fill(new Rect(0, 0, W, H), new Color(0.015f, 0.02f, 0.022f, 0.72f));
                Grid(); Header(); BottomNavigation(); Content();
            }
            GUI.matrix = old;
        }

        public void OpenFromShelter(int targetTab)
        {
            tab = Mathf.Clamp(targetTab, 0, tabs.Length - 1);
            IsVisible = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseToShelter()
        {
            IsVisible = false;
            contextItem = pendingSaleItem = detailsItem = gunsmithItem = null;
            draggedItem = selectedItem = openContainer = null;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void BeginRaidFromShelter() => session?.BeginRaid();
        public void SaveShelterProgress() => session?.Save();

        private void Update()
        {
            if (!IsVisible || UnityEngine.InputSystem.Keyboard.current == null) return;
            if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) CloseToShelter();
        }

        private void CinematicHeader()
        {
            PlayerData p = session.Player;
            Fill(new Rect(0, 0, W, 82), new Color(0.01f, 0.018f, 0.022f, 0.86f));
            Fill(new Rect(0, 81, W, 1), new Color(0.45f, 0.52f, 0.53f, 0.28f));
            GUI.Label(new Rect(38, 16, 360, 48), "EXTRACTION // OFFLINE", logo);
            string[] top = { "ЗАДАНИЯ", "КАРТА", "ПЕРСОНАЛИЗАЦИЯ", "ДОСТИЖЕНИЯ" };
            for (int i = 0; i < top.Length; i++)
            {
                Rect r = new(405 + i * 190, 20, 175, 42);
                Fill(r, new Color(0.10f, 0.13f, 0.14f, 0.52f));
                GUI.Label(r, top[i], Center(small));
            }
            GUI.Label(new Rect(1190, 16, 350, 28), $"{p.playerName.ToUpperInvariant()}   ·   УР. {p.level}", Right(h2));
            GUI.Label(new Rect(1190, 48, 350, 22), $"{p.money:N0} ₽   ·   {p.abilityPoints} ОЧК. НАВЫКОВ", Right(small));
        }

        private void CinematicLobby()
        {
            Fill(new Rect(0, 82, 430, 818), new Color(0.01f, 0.02f, 0.024f, 0.34f));
            Fill(new Rect(1190, 82, 410, 818), new Color(0.01f, 0.02f, 0.024f, 0.32f));
            GUI.Label(new Rect(650, 430, 380, 28), "ОСНОВНОЙ ОПЕРАТОР", Center(small));
            Fill(new Rect(715, 466, 250, 1), new Color(0.75f, 0.82f, 0.82f, 0.45f));

            string[] menu = { "ХРАНИЛИЩЕ", "ОРУЖЕЙНИК", "МАГАЗИН", "НАВЫКИ", "ПРОФИЛЬ", "БУНКЕР" };
            int[] targets = { 1, 2, 2, 3, 4, 5 };
            GUI.Label(new Rect(55, 500, 330, 24), "ПОДГОТОВКА", small);
            for (int i = 0; i < menu.Length; i++)
            {
                Rect r = new(48, 530 + i * 45, 340, 40);
                Fill(r, new Color(0.035f, 0.055f, 0.06f, 0.68f));
                Fill(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0.5f, 0.58f, 0.58f, 0.25f));
                if (GUI.Button(r, menu[i], nav)) tab = targets[i];
            }
            if (GUI.Button(new Rect(48, 815, 340, 62), "ВОЙТИ В РЕЙД", action)) session.BeginRaid();

            Rect profile = new(1220, 125, 330, 112); Panel(profile, new Color(0.035f, 0.055f, 0.06f, 0.78f));
            Fill(new Rect(profile.x, profile.y, 5, profile.height), orange);
            GUI.Label(new Rect(profile.x + 24, profile.y + 18, 280, 28), session.Player.playerName.ToUpperInvariant(), h2);
            GUI.Label(new Rect(profile.x + 24, profile.y + 52, 280, 22), $"УРОВЕНЬ {session.Player.level}   ·   ВЫЖИВАЕМОСТЬ {session.Player.statistics.SurvivalRate:0.0}%", small);
            GUI.Label(new Rect(profile.x + 24, profile.y + 79, 280, 22), $"РЕЙДОВ: {session.Player.statistics.raids}   УБИЙСТВ: {session.Player.statistics.kills}", small);

            Rect operation = new(1220, 260, 330, 150); Panel(operation, new Color(0.035f, 0.055f, 0.06f, 0.76f));
            GUI.Label(new Rect(operation.x + 22, operation.y + 18, 285, 22), "АКТИВНАЯ ОПЕРАЦИЯ", small);
            GUI.Label(new Rect(operation.x + 22, operation.y + 48, 285, 30), "ТЕЛЕЦЕНТР", h2);
            GUI.Label(new Rect(operation.x + 22, operation.y + 83, 285, 45), "Закрытая зона · высокий риск\nТолько локальные противники", body);

            Rect reward = new(1220, 700, 330, 177); Panel(reward, new Color(0.035f, 0.055f, 0.06f, 0.78f));
            GUI.Label(new Rect(reward.x + 22, reward.y + 18, 285, 22), "СЛЕДУЮЩАЯ НАГРАДА", small);
            int next = Mathf.Min(Progression.MaxLevel, ((session.Player.level / 5) + 1) * 5);
            GUI.Label(new Rect(reward.x + 22, reward.y + 50, 285, 32), $"СКИН ОПЕРАТОРА · УР. {next}", h2);
            Fill(new Rect(reward.x + 22, reward.y + 104, 286, 4), line);
            float progress = session.Player.ExperienceForNextLevel == 0 ? 0 : (float)session.Player.experience / session.Player.ExperienceForNextLevel;
            Fill(new Rect(reward.x + 22, reward.y + 104, 286 * progress, 4), orange);
            GUI.Label(new Rect(reward.x + 22, reward.y + 123, 286, 22), $"{session.Player.experience:N0} / {session.Player.ExperienceForNextLevel:N0} XP", small);
        }

        private void Grid()
        {
            Color g = new(0.3f, 0.36f, 0.36f, 0.06f);
            for (int x = 0; x < W; x += 64) Fill(new Rect(x, 74, 1, 758), g);
            for (int y = 74; y < 832; y += 64) Fill(new Rect(0, y, W, 1), g);
        }

        private void Header()
        {
            PlayerData p = session.Player;
            Fill(new Rect(0, 0, W, 74), new Color(0.02f, 0.025f, 0.028f, 0.99f));
            Fill(new Rect(0, 73, W, 1), line); Fill(new Rect(0, 0, 7, 74), orange);
            GUI.Label(new Rect(30, 14, 440, 48), "EXTRACTION // OFFLINE", logo);
            GUI.Label(new Rect(490, 25, 500, 25), "ОПЕРАТИВНЫЙ ЦЕНТР   /   СЕКТОР 05", small);
            GUI.Label(new Rect(1110, 13, 440, 27), $"{p.playerName.ToUpperInvariant()}   ·   УРОВЕНЬ {p.level}", h2);
            GUI.Label(new Rect(1110, 42, 220, 21), $"{p.experience:N0} / {p.ExperienceForNextLevel:N0} XP", small);
            GUI.Label(new Rect(1340, 42, 210, 21), $"{p.money:N0} ₽", Right(small));
            float r = p.ExperienceForNextLevel == 0 ? 0 : (float)p.experience / p.ExperienceForNextLevel;
            Fill(new Rect(1110, 66, 440, 3), line); Fill(new Rect(1110, 66, 440 * Mathf.Clamp01(r), 3), orange);
        }

        private void Sidebar()
        {
            Fill(new Rect(0, 74, 310, 826), new Color(0.028f, 0.035f, 0.038f, 0.98f));
            Fill(new Rect(309, 74, 1, 826), line);
            GUI.Label(new Rect(28, 105, 250, 24), "ГЛАВНОЕ МЕНЮ", small);
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect r = new(22, 150 + i * 65, 266, 50);
                if (i == tab) Fill(new Rect(r.x, r.y, 4, r.height), orange);
                if (GUI.Button(r, $"0{i + 1}    {tabs[i]}", i == tab ? navOn : nav)) tab = i;
            }
            Fill(new Rect(22, 570, 266, 1), line);
            GUI.Label(new Rect(28, 590, 250, 22), "ТЕКУЩИЙ СТАТУС", small);
            GUI.Label(new Rect(28, 620, 250, 75), session.LastNotification, body);
            GUI.Label(new Rect(28, 760, 250, 22), "ЛОКАЛЬНЫЙ ПРОФИЛЬ", small);
            if (GUI.Button(new Rect(22, 800, 266, 48), "СОХРАНИТЬ ПРОГРЕСС", nav)) session.Save();
            GUI.Label(new Rect(28, 860, 250, 20), "BUILD 0.2  ·  OFFLINE", small);
        }

        private void BottomNavigation()
        {
            Fill(new Rect(0, 832, W, 68), new Color(.018f, .025f, .028f, .98f));
            Fill(new Rect(0, 831, W, 1), line);
            float buttonWidth = 220f;
            float gap = 8f;
            float total = tabs.Length * buttonWidth + (tabs.Length - 1) * gap;
            float start = (W - total) * .5f;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect r = new(start + i * (buttonWidth + gap), 842, buttonWidth, 46);
                if (GUI.Button(r, $"0{i + 1}  {tabs[i]}", i == tab ? navOn : nav)) tab = i;
            }
        }

        private void Content()
        {
            Rect a = tab == 0 ? new Rect(350, 110, 1205, 740) : new Rect(40, 100, 1520, 720);
            if (tab == 0) Lobby(a);
            else if (tab == 1) Stash(a);
            else if (tab == 2) Shop(a);
            else if (tab == 3) Abilities(a);
            else if (tab == 4) Profile(a);
            else Bunker(a);
        }

        private void Lobby(Rect a)
        {
            GUI.Label(new Rect(a.x, a.y, 700, 30), "ПОДГОТОВКА К ОПЕРАЦИИ", small);
            GUI.Label(new Rect(a.x, a.y + 29, 750, 58), "ТЕЛЕЦЕНТР", h1);
            GUI.Label(new Rect(a.x, a.y + 90, 700, 25), "ЗАКРЫТАЯ ЗОНА  ·  ВЫСОКИЙ РИСК  ·  35 МИНУТ", small);

            Rect raid = new(a.x, a.y + 145, 790, 315); Panel(raid, panel); Fill(new Rect(raid.x, raid.y, 7, raid.height), orange);
            GUI.Label(new Rect(raid.x + 34, raid.y + 26, 600, 34), "ОПЕРАЦИЯ: МЁРТВЫЙ ЭФИР", h2);
            GUI.Label(new Rect(raid.x + 34, raid.y + 70, 700, 55), "Проникните в комплекс, соберите разведданные и покиньте сектор через доступную точку эвакуации.", body);
            Tag(new Rect(raid.x + 34, raid.y + 142, 150, 34), "ТОЛЬКО БОТЫ");
            Tag(new Rect(raid.x + 196, raid.y + 142, 174, 34), "ЛОКАЛЬНЫЙ РЕЙД");
            Tag(new Rect(raid.x + 382, raid.y + 142, 138, 34), "СЛОЖНО");
            Fill(new Rect(raid.x + 34, raid.y + 202, raid.width - 68, 1), line);
            Pair(raid.x + 34, raid.y + 221, "ТОЧКА ВХОДА", "СЛУЖЕБНЫЙ ДВОР");
            Pair(raid.x + 420, raid.y + 221, "УСЛОВИЕ", "ЭВАКУИРОВАТЬСЯ");

            Rect kit = new(a.x + 820, a.y + 145, 385, 315); Panel(kit, panel2);
            GUI.Label(new Rect(kit.x + 26, kit.y + 23, 330, 29), "КОМПЛЕКТ ОПЕРАТОРА", h2);
            Fill(new Rect(kit.x + 26, kit.y + 65, 333, 1), line);
            Loadout(kit.x + 26, kit.y + 85, "ОСНОВНОЕ ОРУЖИЕ"); Loadout(kit.x + 26, kit.y + 143, "БРОНЯ"); Loadout(kit.x + 26, kit.y + 201, "РЮКЗАК");
            GUI.Label(new Rect(kit.x + 26, kit.y + 270, 160, 22), "СТОИМОСТЬ", small);
            GUI.Label(new Rect(kit.x + 180, kit.y + 263, 178, 32), "0 ₽", Right(h2));

            GUI.Label(new Rect(a.x, a.y + 495, 600, 24), "ТЕСТИРОВАНИЕ ПРОГРЕССИИ", small);
            if (GUI.Button(new Rect(a.x, a.y + 535, 380, 74), "ЗАВЕРШИТЬ РЕЙД: ЭВАКУАЦИЯ", action)) session.SimulateRaid(true);
            if (GUI.Button(new Rect(a.x + 400, a.y + 535, 300, 74), "ЗАВЕРШИТЬ: ГИБЕЛЬ", danger)) session.SimulateRaid(false);
            Rect note = new(a.x + 730, a.y + 515, 475, 125); Panel(note, new Color(0.04f, 0.05f, 0.052f, 0.96f));
            GUI.Label(new Rect(note.x + 24, note.y + 18, 420, 22), "ПРИМЕЧАНИЕ РАЗРАБОТЧИКА", small);
            GUI.Label(new Rect(note.x + 24, note.y + 49, 420, 58), "Кнопки временно имитируют исход рейда и проверяют экономику, опыт и статистику.", body);
        }

        private void DrawGunsmith(Rect a)
        {
            ItemSO weapon = ItemCatalog.Get(gunsmithItem.definitionId);
            GUI.Label(new Rect(a.x, a.y, 500, 28), "ОРУЖЕЙНАЯ МАСТЕРСКАЯ", small);
            GUI.Label(new Rect(a.x, a.y + 28, 700, 54), "СБОРКА ОРУЖИЯ", h1);
            if (GUI.Button(new Rect(a.x + 1260, a.y + 18, 250, 48), "← ВЕРНУТЬСЯ", nav)) { gunsmithItem = null; return; }
            GUI.Label(new Rect(a.x, a.y + 78, 700, 32), weapon.name, h2);

            Rect workbench = new(a.x + 285, a.y + 122, 930, 440);
            Panel(workbench, new Color(.025f, .032f, .034f, .97f));
            Fill(new Rect(workbench.x + 115, workbench.y + 212, 700, 42), new Color(.22f, .25f, .24f));
            Fill(new Rect(workbench.x + 205, workbench.y + 182, 370, 80), weapon.color);
            Fill(new Rect(workbench.x + 575, workbench.y + 196, 195, 58), new Color(.18f, .20f, .195f));
            Fill(new Rect(workbench.x + 45, workbench.y + 218, 175, 26), new Color(.16f, .18f, .175f));
            Fill(new Rect(workbench.x + 400, workbench.y + 258, 34, 112), new Color(.13f, .15f, .145f));
            Fill(new Rect(workbench.x + 478, workbench.y + 252, 28, 76), new Color(.15f, .17f, .165f));
            Stroke(new Rect(workbench.x + 205, workbench.y + 182, 370, 80), new Color(.54f, .61f, .60f));

            GunsmithNode(workbench, new Rect(32, 52, 170, 64), "muzzle", "ДУЛЬНОЕ УСТРОЙСТВО", 12500, new Vector2(105, 212));
            GunsmithNode(workbench, new Rect(270, 35, 150, 64), "optic", "ПРИЦЕЛ", 28000, new Vector2(370, 182));
            GunsmithNode(workbench, new Rect(735, 54, 160, 64), "stock", "ПРИКЛАД", 18500, new Vector2(690, 196));
            GunsmithNode(workbench, new Rect(32, 330, 170, 64), "laser", "ЛЦУ", 9200, new Vector2(245, 254));
            GunsmithMagazineSocket(workbench, new Rect(280, 350, 160, 64), new Vector2(417, 326));
            GunsmithNode(workbench, new Rect(735, 330, 160, 64), "grip", "РУКОЯТКА", 11600, new Vector2(492, 294));

            Rect statsPanel = new(a.x, a.y + 122, 255, 440);
            Panel(statsPanel, panel);
            GUI.Label(new Rect(statsPanel.x + 18, statsPanel.y + 18, 220, 22), "ПАРАМЕТРЫ СБОРКИ", small);
            int mods = gunsmithDraft.Count;
            int recoil = weapon.weapon.recoilControl;
            int ergonomics = weapon.weapon.ergonomics;
            foreach (string mount in gunsmithDraft)
            {
                ItemSO modification = ItemCatalog.Get(ModificationId(mount));
                recoil += modification.modification.recoilModifier;
                ergonomics += modification.modification.ergonomicsModifier;
            }
            WeaponStat(statsPanel, 62, "КОНТРОЛЬ ОТДАЧИ", recoil);
            WeaponStat(statsPanel, 112, "ЭРГОНОМИКА", ergonomics);
            WeaponStat(statsPanel, 162, "ТОЧНОСТЬ", 58 + (gunsmithDraft.Contains("optic") ? 18 : 0));
            WeaponStat(statsPanel, 212, "ДАЛЬНОСТЬ", 52 + (gunsmithDraft.Contains("optic") ? 15 : 0));
            WeaponStat(statsPanel, 262, "МОБИЛЬНОСТЬ", Mathf.Max(25, 76 - mods * 4));
            ItemInstance installedMagazine = FindInstalledMagazine(gunsmithItem);
            int capacity = installedMagazine == null ? 0 : AmmunitionService.MagazineCapacity(installedMagazine);
            GUI.Label(new Rect(statsPanel.x + 18, statsPanel.y + 320, 220, 22), installedMagazine == null
                ? "МАГАЗИН: НЕ УСТАНОВЛЕН"
                : $"МАГАЗИН: {installedMagazine.loadedAmmoCount}/{capacity}", small);
            GUI.Label(new Rect(statsPanel.x + 18, statsPanel.y + 344, 220, 22), $"УСТАНОВЛЕНО МОДУЛЕЙ: {mods}", small);
            GUI.Label(new Rect(statsPanel.x + 18, statsPanel.y + 368, 220, 28), gunsmithMessage, new GUIStyle(small) { wordWrap = true });

            Rect total = new(a.x + 1235, a.y + 122, 285, 440);
            Panel(total, panel2);
            GUI.Label(new Rect(total.x + 20, total.y + 20, 245, 22), "КОМПЛЕКТ МОДИФИКАЦИЙ", small);
            int buildPrice = GunsmithBuildPrice();
            GUI.Label(new Rect(total.x + 20, total.y + 72, 245, 30), $"{buildPrice:N0} ₽", h2);
            GUI.Label(new Rect(total.x + 20, total.y + 118, 245, 150), "Модули из хранилища устанавливаются бесплатно. Цена учитывает только отсутствующие детали, которые купит оружейник.", new GUIStyle(body) { wordWrap = true });
            int repairPrice = Mathf.Max(0, 100 - gunsmithItem.condition) * 500;
            bool repairAvailable = BunkerService.CanRepairWeapons(session.Player) && gunsmithItem.condition < 100 && session.Player.money >= repairPrice;
            GUI.enabled = repairAvailable;
            if (GUI.Button(new Rect(total.x + 20, total.y + 278, 245, 48), $"ПОЧИНИТЬ · {repairPrice:N0} ₽", nav)) RepairGunsmithWeapon(repairPrice);
            GUI.enabled = true;
            if (!BunkerService.IsPowered(session.Player)) GUI.Label(new Rect(total.x + 20, total.y + 329, 245, 18), "РЕМОНТ: НЕТ ЭНЕРГИИ", danger);
            else if (BunkerService.GetLevel(session.Player, "workbench") <= 0) GUI.Label(new Rect(total.x + 20, total.y + 329, 245, 18), "ПОСТРОЙТЕ ВЕРСТАК", danger);
            bool canAfford = session.Player.money >= buildPrice;
            GUI.enabled = canAfford;
            if (GUI.Button(new Rect(total.x + 20, total.y + 350, 245, 58), buildPrice > 0 ? "КУПИТЬ И СОБРАТЬ" : "СОХРАНИТЬ СБОРКУ", action)) ApplyGunsmithBuild();
            GUI.enabled = true;
            if (!canAfford) GUI.Label(new Rect(total.x + 20, total.y + 416, 245, 20), "НЕДОСТАТОЧНО СРЕДСТВ", danger);
        }

        private void RepairGunsmithWeapon(int price)
        {
            if (!BunkerService.CanRepairWeapons(session.Player)) { gunsmithMessage = "Ремонт невозможен: в бункере нет энергии"; return; }
            if (session.Player.money < price) { gunsmithMessage = "Недостаточно средств для ремонта"; return; }
            session.Player.money -= price;
            gunsmithItem.condition = 100;
            gunsmithMessage = "Оружие полностью отремонтировано";
            session.Save();
        }

        private void GunsmithNode(Rect workbench, Rect localRect, string id, string label, int price, Vector2 anchor)
        {
            Rect node = new(workbench.x + localRect.x, workbench.y + localRect.y, localRect.width, localRect.height);
            Vector2 nodeCenter = node.center;
            Color connector = gunsmithDraft.Contains(id) ? new Color(.25f, .78f, .32f, .9f) : new Color(.38f, .43f, .43f, .7f);
            Fill(new Rect(Mathf.Min(nodeCenter.x, workbench.x + anchor.x), workbench.y + anchor.y, Mathf.Abs(nodeCenter.x - (workbench.x + anchor.x)), 1), connector);
            Fill(node, gunsmithDraft.Contains(id) ? new Color(.10f, .23f, .13f, .98f) : new Color(.07f, .083f, .086f, .98f));
            Stroke(node, connector);
            GUI.Label(new Rect(node.x + 8, node.y + 7, node.width - 16, 22), label, small);
            bool originallyInstalled = gunsmithItem.attachmentIds.Contains(id);
            bool available = FindStoredModification(id) != null;
            string status = gunsmithDraft.Contains(id)
                ? (originallyInstalled ? "УСТАНОВЛЕНО" : available ? "ЕСТЬ В ХРАНИЛИЩЕ" : $"КУПИТЬ · {price:N0} ₽")
                : originallyInstalled ? "БУДЕТ СНЯТО" : $"{price:N0} ₽";
            GUI.Label(new Rect(node.x + 8, node.y + 34, node.width - 16, 20), status, small);
            if (GUI.Button(node, GUIContent.none, GUIStyle.none))
            {
                if (gunsmithDraft.Contains(id)) { gunsmithDraft.Remove(id); gunsmithMessage = $"Модуль снят: {label}"; }
                else { gunsmithDraft.Add(id); gunsmithMessage = $"Модуль установлен: {label}"; }
            }
        }

        private void GunsmithMagazineSocket(Rect workbench, Rect localRect, Vector2 anchor)
        {
            Rect node = new(workbench.x + localRect.x, workbench.y + localRect.y, localRect.width, localRect.height);
            ItemInstance magazine = FindInstalledMagazine(gunsmithItem);
            Color connector = magazine == null ? new Color(.38f, .43f, .43f, .7f) : new Color(.25f, .78f, .32f, .9f);
            Fill(new Rect(Mathf.Min(node.center.x, workbench.x + anchor.x), workbench.y + anchor.y,
                Mathf.Abs(node.center.x - (workbench.x + anchor.x)), 1), connector);
            Fill(node, magazine == null ? new Color(.07f, .083f, .086f, .98f) : new Color(.10f, .23f, .13f, .98f));
            Stroke(node, connector);
            GUI.Label(new Rect(node.x + 8, node.y + 7, node.width - 16, 22), "МАГАЗИН", small);
            string status = magazine == null
                ? $"НУЖЕН · {ItemCatalog.Get(ModificationId("magazine")).name}"
                : $"{magazine.loadedAmmoCount}/{AmmunitionService.MagazineCapacity(magazine)}";
            GUI.Label(new Rect(node.x + 8, node.y + 34, node.width - 16, 20), status, small);
        }

        private ItemInstance FindInstalledMagazine(ItemInstance weapon)
        {
            if (weapon == null || string.IsNullOrEmpty(weapon.installedMagazineInstanceId)) return null;
            return session.Player.stash.Find(item => item.instanceId == weapon.installedMagazineInstanceId);
        }

        private void WeaponStat(Rect panelRect, float y, string label, int value)
        {
            value = Mathf.Clamp(value, 0, 100);
            GUI.Label(new Rect(panelRect.x + 18, panelRect.y + y, 218, 18), $"{label}   {value}", small);
            Fill(new Rect(panelRect.x + 18, panelRect.y + y + 24, 218, 5), line);
            Fill(new Rect(panelRect.x + 18, panelRect.y + y + 24, 218 * value / 100f, 5), orange);
        }

        private int GunsmithBuildPrice()
        {
            int total = 0;
            foreach (string mount in gunsmithDraft)
            {
                if (gunsmithItem.attachmentIds.Contains(mount) || FindStoredModification(mount) != null) continue;
                total += ItemCatalog.Get(ModificationId(mount)).price;
            }
            return total;
        }

        private string ModificationId(string mount)
            => mount == "magazine" && gunsmithItem != null && gunsmithItem.definitionId == "smg_c9" ? "mod_magazine_smg" : "mod_" + mount;

        private ItemInstance FindStoredModification(string mount)
        {
            string definitionId = ModificationId(mount);
            return session.Player.stash.Find(item => item != gunsmithItem && item.definitionId == definitionId && string.IsNullOrEmpty(item.equippedSlot));
        }

        private void ApplyGunsmithBuild()
        {
            int purchasePrice = GunsmithBuildPrice();
            if (session.Player.money < purchasePrice) { gunsmithMessage = "Недостаточно средств для покупки модулей"; return; }

            var returnedItems = new List<ItemInstance>();
            foreach (string oldMount in gunsmithItem.attachmentIds)
            {
                if (gunsmithDraft.Contains(oldMount)) continue;
                var returned = new ItemInstance { instanceId = Guid.NewGuid().ToString("N"), definitionId = ModificationId(oldMount) };
                if (!FindSpaceInRoot(returned, out int x, out int y))
                {
                    foreach (ItemInstance temporary in returnedItems) session.Player.stash.Remove(temporary);
                    gunsmithMessage = "В хранилище нет места для снятых модификаций";
                    return;
                }
                returned.x = x; returned.y = y;
                session.Player.stash.Add(returned);
                returnedItems.Add(returned);
            }

            foreach (string newMount in gunsmithDraft)
            {
                if (gunsmithItem.attachmentIds.Contains(newMount)) continue;
                ItemInstance stored = FindStoredModification(newMount);
                if (stored != null) session.Player.stash.Remove(stored);
            }
            session.Player.money -= purchasePrice;
            gunsmithItem.attachmentIds = new List<string>(gunsmithDraft);
            session.Save();
            inventoryMessage = purchasePrice > 0
                ? $"Сборка завершена · куплено модулей на {purchasePrice:N0} ₽"
                : "Сборка завершена из модулей в хранилище";
            gunsmithItem = null;
        }

        private void Shop(Rect a)
        {
            string[] categoryNames = { "ВСЕ", "ОРУЖИЕ", "БРОНЯ", "ПАТРОНЫ", "МЕДИЦИНА", "РЮКЗАКИ", "МОДУЛИ" };
            string[] products = { "rifle_mk1", "smg_c9", "armor_t3", "helmet_t3", "headset_m32", "face_shield_t2", "rig_16", "ammo_556", "ammo_9x19", "medkit_field", "bandage", "backpack_20", "backpack_35", "backpack_54", "mod_muzzle", "mod_optic", "mod_stock", "mod_laser", "mod_magazine", "mod_magazine_smg", "mod_grip" };
            GUI.Label(new Rect(a.x, a.y, 500, 28), "ТОРГОВАЯ ПЛОЩАДКА", small);
            GUI.Label(new Rect(a.x, a.y + 28, 650, 54), "МАГАЗИН", h1);
            GUI.Label(new Rect(a.x + 1040, a.y + 38, 450, 32), $"БАЛАНС   {session.Player.money:N0} ₽", Right(h2));

            float categoryWidth = 205f;
            for (int i = 0; i < categoryNames.Length; i++)
            {
                Rect category = new(a.x + i * (categoryWidth + 8), a.y + 92, categoryWidth, 42);
                if (GUI.Button(category, categoryNames[i], shopCategory == i ? navOn : nav)) shopCategory = i;
            }

            Rect listPanel = new(a.x, a.y + 150, 1010, 550);
            Panel(listPanel, panel);
            GUI.Label(new Rect(listPanel.x + 18, listPanel.y + 14, 500, 22), "КАТАЛОГ СНАРЯЖЕНИЯ", small);
            shopScroll = GUI.BeginScrollView(new Rect(listPanel.x + 14, listPanel.y + 48, 980, 484), shopScroll, new Rect(0, 0, 950, 650));
            int visibleIndex = 0;
            foreach (string productId in products)
            {
                ItemSO definition = ItemCatalog.Get(productId);
                if (!ShopCategoryMatches(definition.category, shopCategory)) continue;
                int column = visibleIndex % 3;
                int row = visibleIndex / 3;
                Rect card = new(column * 312, row * 154, 298, 140);
                bool selected = selectedShopItemId == productId;
                Fill(card, selected ? new Color(.13f, .16f, .16f, .98f) : new Color(.065f, .078f, .082f, .95f));
                Stroke(card, selected ? orange : line);
                Rect cardIcon = new(card.x + 12, card.y + 15, 72, 72);
                Fill(cardIcon, new Color(.025f, .032f, .034f, 1f));
                DrawItemIcon(cardIcon, definition);
                Stroke(cardIcon, line);
                GUI.Label(new Rect(card.x + 98, card.y + 14, 185, 42), definition.name, new GUIStyle(small) { normal = { textColor = Color.white }, wordWrap = true });
                GUI.Label(new Rect(card.x + 98, card.y + 61, 185, 20), $"РАЗМЕР {definition.width}×{definition.height}", small);
                GUI.Label(new Rect(card.x + 12, card.y + 101, 270, 25), $"{definition.price:N0} ₽", h2);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) selectedShopItemId = productId;
                visibleIndex++;
            }
            GUI.EndScrollView();

            Rect detail = new(a.x + 1030, a.y + 150, 490, 550);
            Panel(detail, panel2);
            ItemSO selectedDefinition = ItemCatalog.Get(selectedShopItemId);
            GUI.Label(new Rect(detail.x + 24, detail.y + 22, 440, 24), "ВЫБРАННЫЙ ТОВАР", small);
            GUI.Label(new Rect(detail.x + 24, detail.y + 60, 440, 58), selectedDefinition.name, h2);
            Rect detailIcon = new(detail.x + 24, detail.y + 125, 442, 150);
            Fill(detailIcon, new Color(.025f, .032f, .034f, 1f));
            DrawItemIcon(detailIcon, selectedDefinition);
            Stroke(detailIcon, line);
            GUI.Label(new Rect(detail.x + 42, detail.y + 175, 406, 42), $"{selectedDefinition.width} × {selectedDefinition.height} ЯЧЕЕК", Center(h2));
            GUI.Label(new Rect(detail.x + 24, detail.y + 300, 210, 22), "КОЛИЧЕСТВО", small);
            if (GUI.Button(new Rect(detail.x + 24, detail.y + 332, 48, 42), "−", nav)) shopQuantity = Mathf.Max(1, shopQuantity - 1);
            GUI.Label(new Rect(detail.x + 80, detail.y + 332, 74, 42), shopQuantity.ToString(), Center(h2));
            if (GUI.Button(new Rect(detail.x + 160, detail.y + 332, 48, 42), "+", nav)) shopQuantity = Mathf.Min(10, shopQuantity + 1);
            int totalPrice = selectedDefinition.price * shopQuantity;
            GUI.Label(new Rect(detail.x + 240, detail.y + 335, 225, 32), $"{totalPrice:N0} ₽", Right(h2));
            bool affordable = session.Player.money >= totalPrice;
            GUI.enabled = affordable;
            if (GUI.Button(new Rect(detail.x + 24, detail.y + 400, 442, 58), "КУПИТЬ В ХРАНИЛИЩЕ", action)) BuyShopItem(selectedDefinition, shopQuantity);
            GUI.enabled = true;
            GUI.Label(new Rect(detail.x + 24, detail.y + 474, 442, 54), affordable ? shopMessage : "Недостаточно средств", new GUIStyle(small) { wordWrap = true });

            if (!string.IsNullOrEmpty(shopNotice) && Time.realtimeSinceStartup < shopNoticeUntil)
            {
                Rect notice = new(a.x + 470, a.y + 8, 580, 58);
                Fill(notice, new Color(.34f, .055f, .045f, .98f));
                Stroke(notice, new Color(1f, .24f, .16f, 1f));
                GUI.Label(new Rect(notice.x + 20, notice.y + 17, notice.width - 40, 28), shopNotice, Center(h2));
            }
        }

        private static bool ShopCategoryMatches(ItemCategory category, int selectedCategory)
        {
            if (selectedCategory == 0) return true;
            if (selectedCategory == 1) return category == ItemCategory.Weapon;
            if (selectedCategory == 2) return category == ItemCategory.Armor;
            if (selectedCategory == 3) return category == ItemCategory.Ammo;
            if (selectedCategory == 4) return category == ItemCategory.Medical;
            if (selectedCategory == 5) return category == ItemCategory.Backpack;
            return category == ItemCategory.Modification;
        }

        private void BuyShopItem(ItemSO definition, int count)
        {
            int price = definition.price * count;
            if (session.Player.money < price) { shopMessage = "Недостаточно средств"; return; }
            int originalCount = session.Player.stash.Count;
            bool stackable = definition.category == ItemCategory.Ammo || definition.category == ItemCategory.Medical;
            int instances = stackable ? 1 : count;
            for (int i = 0; i < instances; i++)
            {
                var item = new ItemInstance
                {
                    instanceId = Guid.NewGuid().ToString("N"),
                    definitionId = definition.id,
                    quantity = stackable ? count : 1,
                    folded = definition.CanFold
                };
                if (!FindSpaceInRoot(item, out int x, out int y))
                {
                    session.Player.stash.RemoveRange(originalCount, session.Player.stash.Count - originalCount);
                    shopMessage = "В хранилище недостаточно свободного места";
                    shopNotice = "НЕДОСТАТОЧНО МЕСТА В ХРАНИЛИЩЕ";
                    shopNoticeUntil = Time.realtimeSinceStartup + 4f;
                    return;
                }
                item.x = x; item.y = y;
                session.Player.stash.Add(item);
            }
            session.Player.money -= price;
            session.Save();
            shopMessage = $"Куплено: {definition.name} ×{count}";
        }

        private void Stash(Rect a)
        {
            if (gunsmithItem != null)
            {
                if (!session.Player.stash.Contains(gunsmithItem)) gunsmithItem = null;
                else { DrawGunsmith(a); return; }
            }
            const int columns = StashColumns, rows = StashRows;
            const float cell = InventoryLayout.CellSize;
            Vector2 origin = new(a.x + 900, a.y + 118);
            GUI.Label(new Rect(a.x, a.y, 500, 30), "СНАРЯЖЕНИЕ И ИМУЩЕСТВО", small);
            GUI.Label(new Rect(a.x, a.y + 29, 700, 58), "ХРАНИЛИЩЕ", h1);
            GUI.Label(new Rect(a.x + 850, a.y + 43, 650, 25), "F — БЫСТРО · R — ПОВЕРНУТЬ · ЛКМ — ПЕРЕТАЩИТЬ", Right(small));
            GUI.Label(new Rect(a.x + 850, a.y + 68, 650, 22), inventoryMessage, Right(small));

            Panel(new Rect(origin.x - 10, origin.y - 10, columns * cell + 20, rows * cell + 20), new Color(.025f, .032f, .034f, .96f));
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                Rect slot = new(origin.x + x * cell + 1, origin.y + y * cell + 1, cell - 2, cell - 2);
                Fill(slot, new Color(.09f, .105f, .108f, .78f));
                Stroke(slot, new Color(.23f, .26f, .26f, .42f));
            }

            HandleStashInput(origin, columns, rows, cell);
            foreach (ItemInstance item in session.Player.stash)
            {
                if (item == draggedItem || !string.IsNullOrEmpty(item.equippedSlot) || !string.IsNullOrEmpty(item.parentContainerId)) continue;
                DrawItem(ItemRect(item, origin, cell), item, item == selectedItem ? orange : line, 1f);
            }
            Rect side = new(a.x, a.y + 118, EquipmentPanelWidth, 570); Panel(side, panel);
            DrawCharacterPanel(side);
            // Nested containers that are not equipped keep a compact inspection window;
            // rig and backpack contents remain embedded in the equipment column.
            if (openContainer != null) DrawContainerPanel();
            DrawPlacementPreview(origin, columns, rows, cell);
            if (draggedItem != null)
            {
                ItemCatalog.GetSize(draggedItem, out int width, out int height);
                float drawCell = openContainer != null && draggedItem.parentContainerId == openContainer.instanceId ? InventoryLayout.CellSize : cell;
                if (!string.IsNullOrEmpty(draggedItem.parentContainerId))
                {
                    ItemInstance parent = session.Player.stash.Find(item => item.instanceId == draggedItem.parentContainerId);
                    if (parent != null && !string.IsNullOrEmpty(parent.equippedSlot) && TryGetEquippedContainerGrid(parent, out _, out float equippedCell)) drawCell = equippedCell;
                }
                Rect draggedRect = new(uiMouse.x - dragOffset.x, uiMouse.y - dragOffset.y, width * drawCell - 3, height * drawCell - 3);
                if (unfoldingPreview)
                {
                    int px = Mathf.RoundToInt((uiMouse.x - dragOffset.x - origin.x) / cell);
                    int py = Mathf.RoundToInt((uiMouse.y - dragOffset.y - origin.y) / cell);
                    bool valid = CanPlace(draggedItem, px, py, width, height, columns, rows);
                    GUI.BeginClip(new Rect(origin.x, origin.y, columns * cell, rows * cell));
                    DrawItem(new Rect(draggedRect.x - origin.x, draggedRect.y - origin.y, draggedRect.width, draggedRect.height), draggedItem,
                        valid ? new Color(.25f, 1f, .45f) : new Color(1f, .2f, .15f), .64f);
                    GUI.EndClip();
                }
                else DrawItem(draggedRect, draggedItem, orange, .86f);
            }
            else DrawInventoryTooltip(origin, cell);
            DrawItemContextMenu();
            DrawSaleConfirmation();
            DrawItemDetails();
        }

        private void DrawCharacterPanel(Rect side)
        {
            Rect equipmentButton = new(side.x + 18, side.y + 14, 418, 42);
            Rect healthButton = new(side.x + 442, side.y + 14, 430, 42);
            if (GUI.Button(equipmentButton, "СНАРЯЖЕНИЕ", characterPanelTab == 0 ? navOn : nav)) characterPanelTab = 0;
            if (GUI.Button(healthButton, "ЗДОРОВЬЕ", characterPanelTab == 1 ? navOn : nav)) characterPanelTab = 1;
            Fill(new Rect(side.x + 18, side.y + 64, 854, 1), line);

            Rect viewport = new(side.x + 18, side.y + 76, 854, 474);
            float contentHeight = characterPanelTab == 0 ? 900f : 620f;
            equipmentScroll = GUI.BeginScrollView(viewport, equipmentScroll, new Rect(0, 0, 830, contentHeight));
            if (characterPanelTab == 0) DrawEquipmentContent(); else DrawHealthContent();
            GUI.EndScrollView();
        }

        private void DrawEquipmentContent()
        {
            GUI.Label(new Rect(570, 15, 190, 22), "КАРМАНЫ", small);
            for (int i = 0; i < 4; i++) PocketSlot(570 + i * 52, 42, InventoryLayout.CellSize, $"pocket_{i}", i + 1);

            Rect rigRect = GetEquipmentContainerRect("rig");
            Rect backpackRect = GetEquipmentContainerRect("backpack");
            EquipmentContainerSlot(rigRect.x, rigRect.y, rigRect.width, rigRect.height, "rig", "РАЗГРУЗКА");
            EquipmentContainerSlot(backpackRect.x, backpackRect.y, backpackRect.width, backpackRect.height, "backpack", "РЮКЗАК");

            if (operatorImage != null) GUI.DrawTexture(new Rect(155, 0, 250, 390), operatorImage, ScaleMode.ScaleToFit, true);
            EquipmentSlot(10, 15, 130, 115, "headset", "НАУШНИКИ");
            EquipmentSlot(10, 140, 130, 150, "armor", "БРОНЕЖИЛЕТ");
            EquipmentSlot(420, 15, 130, 115, "helmet", "ШЛЕМ");
            EquipmentSlot(420, 140, 130, 115, "face_cover", "ЗАЩИТА ЛИЦА");
            EquipmentSlot(420, 265, 130, 105, "secure", "КОНТЕЙНЕР");

            EquipmentSlot(10, 400, 135, 95, "holster", "ПИСТОЛЕТ");
            EquipmentSlot(10, 505, 135, 95, "melee", "ХОЛОДНОЕ ОРУЖИЕ");
            EquipmentSlot(155, 400, 395, 95, "main_weapon", "ОСНОВНОЕ ОРУЖИЕ");
            EquipmentSlot(155, 505, 395, 95, "second_weapon", "ДОПОЛНИТЕЛЬНОЕ ОРУЖИЕ");
        }

        private Rect GetEquipmentContainerRect(string slotId)
        {
            ItemInstance rig = FindEquipped("rig");
            float rigWidth = rig == null ? 220f : ItemCatalog.Get(rig.definitionId).internalWidth * InventoryLayout.CellSize + 20f;
            float rigHeight = rig == null ? 58f : ItemCatalog.Get(rig.definitionId).internalHeight * InventoryLayout.CellSize + 48f;
            if (slotId == "rig") return new Rect(570, 105, rigWidth, rigHeight);
            ItemInstance backpack = FindEquipped("backpack");
            float backpackWidth = backpack == null ? 220f : ItemCatalog.Get(backpack.definitionId).internalWidth * InventoryLayout.CellSize + 20f;
            float backpackHeight = backpack == null ? 58f : ItemCatalog.Get(backpack.definitionId).internalHeight * InventoryLayout.CellSize + 48f;
            return new Rect(570, 115 + rigHeight, backpackWidth, backpackHeight);
        }

        private void EquipmentContainerSlot(float x, float y, float w, float h, string slotId, string label)
        {
            Rect panelRect = new(x, y, w, h);
            ItemInstance equipped = FindEquipped(slotId);
            bool compatible = selectedItem != null && IsCompatible(selectedItem, slotId);
            bool dragCompatible = draggedItem != null && IsCompatible(draggedItem, slotId);
            Fill(panelRect, compatible || dragCompatible ? new Color(.16f, .27f, .19f, .88f) : new Color(.065f, .078f, .082f, .94f));
            Stroke(panelRect, compatible || dragCompatible ? new Color(.28f, .75f, .38f) : line);
            if (equipped == null)
            {
                GUI.Label(new Rect(x + 10, y + 7, w - 20, 20), label, small);
                GUI.Label(new Rect(x + 10, y + 38, w - 20, 24), compatible || dragCompatible ? "МОЖНО ЭКИПИРОВАТЬ" : "ПУСТО", small);
                return;
            }

            ItemSO definition = ItemCatalog.Get(equipped.definitionId);
            DrawItemIcon(new Rect(x + 7, y + 5, 34, 34), definition, .95f, equipped.rotated);
            GUI.Label(new Rect(x + 47, y + 10, w - 57, 22), definition.name, new GUIStyle(small) { normal = { textColor = Color.white }, clipping = TextClipping.Clip });
            Fill(new Rect(x + 7, y + 41, w - 14, 1), line);
            float cell = InventoryLayout.CellSize;
            Vector2 origin = new(x + 10, y + 45);
            for (int gy = 0; gy < definition.internalHeight; gy++)
            for (int gx = 0; gx < definition.internalWidth; gx++)
            {
                Rect c = new(origin.x + gx * cell, origin.y + gy * cell, cell, cell);
                Fill(c, new Color(.035f, .045f, .047f, .95f)); Stroke(c, new Color(.19f, .23f, .23f, .8f));
            }
            foreach (ItemInstance item in session.Player.stash)
            {
                if (item == draggedItem || item.parentContainerId != equipped.instanceId) continue;
                DrawItem(ItemRect(item, origin, cell), item, item == selectedItem ? orange : line, 1f);
            }
        }

        private void PocketSlot(float x, float y, float size, string slotId, int number)
        {
            Rect rect = new(x, y, size, size);
            ItemInstance item = FindEquipped(slotId);
            bool compatible = (selectedItem != null && IsCompatible(selectedItem, slotId))
                || (draggedItem != null && IsCompatible(draggedItem, slotId));
            Fill(rect, compatible ? new Color(.16f, .27f, .19f, .9f) : new Color(.065f, .078f, .082f, .96f));
            Stroke(rect, compatible ? new Color(.28f, .75f, .38f) : line);
            if (item != null) DrawItemIcon(new Rect(x + 4, y + 4, size - 8, size - 8), ItemCatalog.Get(item.definitionId), .95f, item.rotated);
            Fill(new Rect(x + 2, y + 2, 17, 17), new Color(.025f, .032f, .034f, .92f));
            GUI.Label(new Rect(x + 5, y + 2, 14, 17), number.ToString(), small);
            if (item != null && item.quantity > 1)
            {
                Fill(new Rect(x + size - 31, y + size - 20, 29, 18), new Color(.025f, .032f, .034f, .92f));
                GUI.Label(new Rect(x + size - 30, y + size - 20, 26, 18), $"×{item.quantity}", Right(small));
            }
        }

        private void DrawHealthContent()
        {
            PlayerVitals v = session.Player.vitals;
            GUI.Label(new Rect(0, 0, 890, 26), $"ОБЩЕЕ ЗДОРОВЬЕ   {v.CurrentHealth} / {PlayerVitals.MaxHealth}", h2);
            if (healthImage != null) GUI.DrawTexture(new Rect(300, 30, 290, 455), healthImage, ScaleMode.ScaleToFit, true);
            Rect bodyRect = new(300, 30, 290, 455);
            BodyPartTint(bodyRect, headMask, v.head, 35);
            BodyPartTint(bodyRect, chestMask, v.chest, 85);
            BodyPartTint(bodyRect, abdomenMask, v.abdomen, 70);
            BodyPartTint(bodyRect, rightArmMask, v.rightArm, 60);
            BodyPartTint(bodyRect, leftArmMask, v.leftArm, 60);
            BodyPartTint(bodyRect, rightLegMask, v.rightLeg, 65);
            BodyPartTint(bodyRect, leftLegMask, v.leftLeg, 65);
            HealthBar(new Rect(30, 55, 210, 36), "ГОЛОВА", v.head, 35);
            HealthBar(new Rect(650, 55, 210, 36), "ГРУДЬ", v.chest, 85);
            HealthBar(new Rect(30, 150, 210, 36), "ПРАВАЯ РУКА", v.rightArm, 60);
            HealthBar(new Rect(650, 150, 210, 36), "ЛЕВАЯ РУКА", v.leftArm, 60);
            HealthBar(new Rect(30, 280, 210, 36), "ПРАВАЯ НОГА", v.rightLeg, 65);
            HealthBar(new Rect(650, 280, 210, 36), "ЛЕВАЯ НОГА", v.leftLeg, 65);
            HealthBar(new Rect(650, 215, 210, 36), "ЖИВОТ", v.abdomen, 70);
            Fill(new Rect(0, 505, 890, 1), line);
            ResourceBar(new Rect(60, 530, 220, 52), "ВОДА", v.hydration, new Color(.18f, .55f, .82f));
            ResourceBar(new Rect(335, 530, 220, 52), "ПИТАНИЕ", v.nutrition, new Color(.72f, .58f, .18f));
            ResourceBar(new Rect(610, 530, 220, 52), "ЭНЕРГИЯ", v.energy, new Color(.32f, .72f, .35f));
        }

        private void BodyPartTint(Rect rect, Texture2D mask, int value, int max)
        {
            float ratio = max <= 0 ? 0f : Mathf.Clamp01((float)value / max);
            if (ratio >= .8f || mask == null) return;
            Color old = GUI.color;
            GUI.color = ratio > .3f ? new Color(1f, .72f, .05f, .58f) : new Color(1f, .08f, .04f, .68f);
            GUI.DrawTexture(rect, mask, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        private void CreateBodyMasks()
        {
            headMask = BodyMask(new[] { P(124, 15), P(132, 6), P(158, 6), P(166, 15), P(168, 42), P(160, 64), P(145, 71), P(129, 64), P(121, 42) });
            chestMask = BodyMask(new[] { P(106, 79), P(128, 72), P(145, 81), P(162, 72), P(184, 79), P(204, 101), P(194, 145), P(178, 169), P(145, 180), P(112, 169), P(96, 145), P(86, 101) });
            abdomenMask = BodyMask(new[] { P(112, 165), P(178, 165), P(184, 198), P(176, 230), P(160, 248), P(145, 254), P(130, 248), P(114, 230), P(106, 198) });
            rightArmMask = BodyMask(new[]
            {
                P(101, 78), P(112, 91), P(108, 118), P(104, 145), P(98, 174), P(87, 203),
                P(75, 231), P(63, 247), P(51, 252), P(38, 244), P(31, 231), P(35, 216),
                P(50, 202), P(61, 174), P(69, 153), P(70, 118), P(76, 91), P(76, 78)
            });
            leftArmMask = BodyMask(new[]
            {
                P(189, 78), P(178, 91), P(182, 118), P(186, 145), P(192, 174), P(203, 203),
                P(215, 231), P(227, 247), P(239, 252), P(252, 244), P(259, 231), P(255, 216),
                P(240, 202), P(229, 174), P(221, 153), P(220, 118), P(214, 91), P(214, 78)
            });
            rightLegMask = BodyMask(new[]
            {
                P(106, 205), P(124, 211), P(145, 218), P(143, 260), P(138, 300), P(127, 330),
                P(124, 363), P(119, 401), P(116, 438), P(105, 449), P(91, 444), P(94, 410),
                P(98, 380), P(92, 350), P(96, 315), P(98, 275), P(96, 238)
            });
            leftLegMask = BodyMask(new[]
            {
                P(184, 205), P(166, 211), P(145, 218), P(147, 260), P(152, 300), P(163, 330),
                P(166, 363), P(171, 401), P(174, 438), P(185, 449), P(199, 444), P(196, 410),
                P(192, 380), P(198, 350), P(194, 315), P(192, 275), P(194, 238)
            });
        }

        private static Vector2 P(float x, float y) => new(x, y);

        private Texture2D BodyMask(params Vector2[][] polygons)
        {
            const int width = 290, height = 455;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool inside = false;
                foreach (Vector2[] polygon in polygons)
                {
                    bool hit = false;
                    for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                    {
                        Vector2 a = polygon[i], b = polygon[j];
                        if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) hit = !hit;
                    }
                    if (hit) { inside = true; break; }
                }
                byte silhouetteAlpha = 0;
                if (inside && healthImage != null)
                {
                    // ScaleMode.ScaleToFit leaves a narrow horizontal margin because
                    // the anatomical texture is taller than its UI rectangle.
                    float drawnWidth = height * healthImage.width / (float)healthImage.height;
                    float margin = (width - drawnWidth) * .5f;
                    int sampleX = Mathf.Clamp(Mathf.FloorToInt((x - margin) / drawnWidth * healthImage.width), 0, healthImage.width - 1);
                    int sampleY = Mathf.Clamp(Mathf.FloorToInt(y / (float)height * healthImage.height), 0, healthImage.height - 1);
                    silhouetteAlpha = healthImage.GetPixel(sampleX, healthImage.height - 1 - sampleY).a > .12f ? (byte)255 : (byte)0;
                }
                pixels[(height - 1 - y) * width + x] = new Color32(255, 255, 255, silhouetteAlpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D ExtractBody(Texture2D source)
        {
            Color32[] sourcePixels = source.GetPixels32();
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var output = new Color32[sourcePixels.Length];
            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color32 p = sourcePixels[i];
                float r = p.r / 255f, g = p.g / 255f, b = p.b / 255f;
                float minimum = Mathf.Min(r, Mathf.Min(g, b));
                float maximum = Mathf.Max(r, Mathf.Max(g, b));
                bool neutralLightBackground = minimum > .88f && maximum - minimum < .09f;
                byte alpha = neutralLightBackground ? (byte)0 : (byte)255;
                output[i] = new Color32(p.r, p.g, p.b, alpha);
            }
            result.SetPixels32(output);
            result.Apply(false, false);
            return result;
        }

        private void HealthBar(Rect rect, string label, int value, int max)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 18), $"{label}   {value}/{max}", small);
            Fill(new Rect(rect.x, rect.y + 23, rect.width, 5), new Color(.18f, .20f, .20f));
            float ratio = max == 0 ? 0 : (float)value / max;
            Color color = ratio >= .8f ? new Color(.35f, .72f, .38f) : ratio > .3f ? new Color(.86f, .58f, .14f) : new Color(.82f, .16f, .12f);
            Fill(new Rect(rect.x, rect.y + 23, rect.width * ratio, 5), color);
        }

        private void ResourceBar(Rect rect, string label, int value, Color color)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20), $"{label}   {value}/100", small);
            Fill(new Rect(rect.x, rect.y + 30, rect.width, 8), line);
            Fill(new Rect(rect.x, rect.y + 30, rect.width * Mathf.Clamp01(value / 100f), 8), color);
        }

        private void DrawItemContextMenu()
        {
            if (contextItem == null || !session.Player.stash.Contains(contextItem)) { contextItem = null; return; }
            bool magazine = AmmunitionService.IsMagazine(ItemCatalog.Get(contextItem.definitionId));
            Rect menu = new(contextMenuPosition.x, contextMenuPosition.y, ContextMenuWidth, magazine ? 214 : 170);
            Fill(menu, new Color(.025f, .032f, .034f, .995f));
            Stroke(menu, new Color(.48f, .53f, .53f, .9f));
            ItemSO definition = ItemCatalog.Get(contextItem.definitionId);
            GUI.Label(new Rect(menu.x + 14, menu.y + 9, menu.width - 28, 24), definition.name, small);
            DrawContextOption(new Rect(menu.x, menu.y + 38, menu.width, 42), "ПРОДАТЬ", orange);
            DrawContextOption(new Rect(menu.x, menu.y + 82, menu.width, 42), definition.category == ItemCategory.Weapon ? "СОБРАТЬ" : "ИНФОРМАЦИЯ", Color.white);
            DrawContextOption(new Rect(menu.x, menu.y + 126, menu.width, 42), "ХАРАКТЕРИСТИКИ", Color.white);
            if (magazine) DrawContextOption(new Rect(menu.x, menu.y + 170, menu.width, 42), "ЗАРЯДИТЬ", new Color(.55f, .9f, .58f));
        }

        private void LoadContextMagazine()
        {
            if (contextItem == null) return;
            ItemInstance magazine = contextItem;
            ItemSO magazineDefinition = ItemCatalog.Get(magazine.definitionId);
            ItemInstance ammo = session.Player.stash.Find(item =>
            {
                ItemSO definition = ItemCatalog.Get(item.definitionId);
                return definition.category == ItemCategory.Ammo
                    && definition.ammunition.caliber == magazineDefinition.modification.magazineCaliber
                    && (string.IsNullOrEmpty(magazine.loadedAmmoDefinitionId) || magazine.loadedAmmoDefinitionId == item.definitionId)
                    && item.quantity > 0;
            });
            if (ammo == null) inventoryMessage = $"Нет патронов калибра {magazineDefinition.modification.magazineCaliber}";
            else if (AmmunitionService.TryLoadMagazine(magazine, ammo, int.MaxValue, out int loaded))
            {
                if (ammo.quantity <= 0) session.Player.stash.Remove(ammo);
                inventoryMessage = $"Магазин заряжен: +{loaded} · {magazine.loadedAmmoCount}/{AmmunitionService.MagazineCapacity(magazine)}";
                session.Save();
            }
            else inventoryMessage = "Магазин уже полон или тип патронов не подходит";
            contextItem = null;
        }

        private void DrawContextOption(Rect rect, string text, Color textColor)
        {
            bool hovered = rect.Contains(uiMouse);
            Fill(rect, hovered ? new Color(.16f, .18f, .18f, .98f) : new Color(.075f, .087f, .09f, .98f));
            Fill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), line);
            GUI.Label(new Rect(rect.x + 14, rect.y + 10, rect.width - 28, 24), text,
                new GUIStyle(small) { normal = { textColor = textColor } });
        }

        private void RequestSale()
        {
            if (contextItem == null) return;
            if (contextItem.permanent)
            {
                inventoryMessage = "Постоянный нож нельзя продать";
                contextItem = null;
                return;
            }
            if (!string.IsNullOrEmpty(contextItem.equippedSlot))
            {
                inventoryMessage = "Сначала снимите предмет с оператора";
                contextItem = null;
                return;
            }
            if (HasChildren(contextItem))
            {
                inventoryMessage = "Нельзя продать контейнер, пока внутри находятся предметы";
                contextItem = null;
                return;
            }
            pendingSaleItem = contextItem;
            contextItem = null;
        }

        private void ConfirmSale()
        {
            if (pendingSaleItem == null || !session.Player.stash.Contains(pendingSaleItem)) { pendingSaleItem = null; return; }
            ItemSO definition = ItemCatalog.Get(pendingSaleItem.definitionId);
            int salePrice = SalePrice(pendingSaleItem);
            string soldName = definition.name;
            if (selectedItem == pendingSaleItem) selectedItem = null;
            if (openContainer == pendingSaleItem) openContainer = null;
            session.Player.stash.Remove(pendingSaleItem);
            pendingSaleItem = null;
            session.Player.money += salePrice;
            session.Save();
            inventoryMessage = $"Продано: {soldName} · +{salePrice:N0} ₽";
        }

        private static int SalePrice(ItemInstance item)
        {
            ItemSO definition = ItemCatalog.Get(item.definitionId);
            int pricedUnits = definition.category == ItemCategory.Ammo ? 1 : Mathf.Max(1, item.quantity);
            return Mathf.Max(1, Mathf.RoundToInt(definition.price * pricedUnits * .6f));
        }

        private static Rect SaleConfirmationRect() => new(570, 318, 460, 214);

        private void DrawSaleConfirmation()
        {
            if (pendingSaleItem == null || !session.Player.stash.Contains(pendingSaleItem)) { pendingSaleItem = null; return; }
            Fill(new Rect(0, 74, W, 758), new Color(0f, 0f, 0f, .48f));
            Rect modal = SaleConfirmationRect();
            Fill(modal, new Color(.035f, .043f, .046f, .995f));
            Stroke(modal, new Color(.52f, .57f, .57f, .95f));
            ItemSO definition = ItemCatalog.Get(pendingSaleItem.definitionId);
            GUI.Label(new Rect(modal.x + 22, modal.y + 18, modal.width - 44, 24), "ПОДТВЕРЖДЕНИЕ ПРОДАЖИ", small);
            GUI.Label(new Rect(modal.x + 22, modal.y + 52, modal.width - 44, 34), definition.name, h2);
            GUI.Label(new Rect(modal.x + 22, modal.y + 96, 190, 26), "ЦЕНА ПРОДАЖИ", small);
            GUI.Label(new Rect(modal.x + 212, modal.y + 91, 226, 34), $"{SalePrice(pendingSaleItem):N0} ₽", Right(h2));
            DrawModalButton(new Rect(modal.x + 22, modal.y + 142, 204, 48), "ПРОДАТЬ", true);
            DrawModalButton(new Rect(modal.x + 234, modal.y + 142, 204, 48), "ОТМЕНА", false);
        }

        private void DrawModalButton(Rect rect, string label, bool primary)
        {
            bool hovered = rect.Contains(uiMouse);
            Color baseColor = primary ? new Color(.76f, .31f, .055f, 1f) : new Color(.10f, .12f, .125f, 1f);
            Fill(rect, hovered ? Color.Lerp(baseColor, Color.white, .12f) : baseColor);
            Stroke(rect, primary ? orange : line);
            GUI.Label(rect, label, Center(h2));
        }

        private void ShowContextInformation(bool characteristics)
        {
            if (contextItem == null) return;
            detailsItem = contextItem;
            showCharacteristics = characteristics;
            contextItem = null;
        }

        private void OpenInformationOrGunsmith()
        {
            if (contextItem == null) return;
            ItemSO definition = ItemCatalog.Get(contextItem.definitionId);
            if (definition.category == ItemCategory.Weapon)
            {
                gunsmithItem = contextItem;
                gunsmithItem.attachmentIds ??= new System.Collections.Generic.List<string>();
                gunsmithDraft = new List<string>(gunsmithItem.attachmentIds);
                gunsmithDraft.Remove("magazine");
                contextItem = null;
                selectedItem = gunsmithItem;
                return;
            }
            ShowContextInformation(false);
        }

        private static Rect ItemDetailsRect() => new(540, 245, 520, 380);

        private void DrawItemDetails()
        {
            if (detailsItem == null || !session.Player.stash.Contains(detailsItem)) { detailsItem = null; return; }
            Fill(new Rect(0, 74, W, 758), new Color(0f, 0f, 0f, .42f));
            Rect modal = ItemDetailsRect();
            Fill(modal, new Color(.035f, .043f, .046f, .995f));
            Stroke(modal, new Color(.46f, .52f, .52f, .95f));
            ItemSO definition = ItemCatalog.Get(detailsItem.definitionId);
            GUI.Label(new Rect(modal.x + 24, modal.y + 18, 410, 22), showCharacteristics ? "ХАРАКТЕРИСТИКИ" : "ИНФОРМАЦИЯ О ПРЕДМЕТЕ", small);
            GUI.Label(new Rect(modal.x + 24, modal.y + 52, 430, 38), definition.name, h2);
            DrawModalButton(new Rect(modal.x + modal.width - 42, modal.y + 12, 28, 28), "×", false);
            Rect infoIcon = new(modal.x + 24, modal.y + 102, 150, 150);
            Fill(infoIcon, new Color(.025f, .032f, .034f, 1f));
            DrawItemIcon(infoIcon, definition);
            Stroke(infoIcon, line);
            GUI.Label(new Rect(modal.x + 198, modal.y + 102, 290, 24), $"КАТЕГОРИЯ   {CategoryLabel(definition.category).ToUpperInvariant()}", small);
            GUI.Label(new Rect(modal.x + 198, modal.y + 145, 290, 25), $"СТОИМОСТЬ   {definition.price:N0} ₽", body);
            GUI.Label(new Rect(modal.x + 198, modal.y + 190, 290, 25), $"СОСТОЯНИЕ   {detailsItem.condition}%", body);
            Fill(new Rect(modal.x + 24, modal.y + 274, modal.width - 48, 1), line);
            string description = showCharacteristics ? ItemCharacteristics(definition, detailsItem) : ItemDescription(definition);
            GUI.Label(new Rect(modal.x + 24, modal.y + 294, modal.width - 48, 62), description, new GUIStyle(body) { wordWrap = true });
        }

        private static string ItemDescription(ItemSO definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.description)) return definition.description;
            if (definition.category == ItemCategory.Weapon) return "Огнестрельное оружие для основного или дополнительного слота оператора.";
            if (definition.category == ItemCategory.Armor) return definition.IsContainer ? "Тактическое снаряжение с внутренними ячейками для припасов." : "Защитное снаряжение, снижающее получаемый урон.";
            if (definition.category == ItemCategory.Backpack) return "Контейнер для переноски добычи. В хранилище его можно свернуть.";
            if (definition.category == ItemCategory.Ammo) return "Боеприпасы для совместимого оружия. Позже будут разделены по пробитию и урону.";
            if (definition.category == ItemCategory.Medical) return "Медицинский предмет для восстановления здоровья и лечения повреждений.";
            if (definition.category == ItemCategory.Modification) return "Оружейная модификация. Её можно установить в оружейной или найти во время рейда.";
            return "Ценный предмет, предназначенный для продажи торговцам.";
        }

        private static string ItemCharacteristics(ItemSO definition, ItemInstance item)
        {
            if (definition.IsContainer)
                return $"Внутренний размер: {definition.internalWidth} × {definition.internalHeight}. Вместимость: {definition.internalWidth * definition.internalHeight} ячеек. Состояние: {(item.folded ? "свёрнут" : "развёрнут")}.";
            if (definition.id.StartsWith("headset_"))
                return $"Дальность слышимости: ×{definition.headset.hearingDistanceMultiplier:0.00}. Подавление фонового шума: {definition.headset.ambientNoiseReduction * 100f:0}%. Защита от выстрелов: {definition.headset.gunshotProtection * 100f:0}%.";
            if (definition.category == ItemCategory.Weapon)
                return $"Калибр: {definition.weapon.caliber}. Урон: {definition.weapon.damage}. Темп стрельбы: {definition.weapon.rateOfFire} выстр./мин. Магазин: {definition.weapon.magazineCapacity}. Эргономика: {definition.weapon.ergonomics}. Контроль отдачи: {definition.weapon.recoilControl}.";
            if (definition.category == ItemCategory.Armor && definition.armor.armorClass > 0)
                return $"Класс защиты: {definition.armor.armorClass}. Прочность: {item.condition}% от {definition.armor.maxDurability}. Защищаемая область: {definition.armor.protectedArea}.";
            if (definition.category == ItemCategory.Ammo)
                return $"Количество: {Mathf.Max(1, item.quantity)}. Калибр: {definition.ammunition.caliber}. Урон: {definition.ammunition.damage}. Пробитие: {definition.ammunition.penetration}.";
            if (definition.category == ItemCategory.Medical)
                return $"Количество: {Mathf.Max(1, item.quantity)}. Лечение: {definition.medicine.healingAmount}. Время применения: {definition.medicine.useTime:0.#} сек.";
            if (definition.category == ItemCategory.Modification)
                return $"Слот: {definition.modification.slot}. Эргономика: {definition.modification.ergonomicsModifier:+#;-#;0}. Контроль отдачи: {definition.modification.recoilModifier:+#;-#;0}.";
            return $"Состояние: {item.condition}%.";
        }

        private static string CategoryLabel(ItemCategory category)
        {
            if (category == ItemCategory.Weapon) return "оружие";
            if (category == ItemCategory.Armor) return "броня и снаряжение";
            if (category == ItemCategory.Ammo) return "боеприпасы";
            if (category == ItemCategory.Medical) return "медицина";
            if (category == ItemCategory.Backpack) return "рюкзак";
            if (category == ItemCategory.Modification) return "модификация оружия";
            return "ценный предмет";
        }

        private void CaptureDragState(ItemInstance item)
        {
            dragSnapshotItem = item;
            dragSnapshotX = item.x;
            dragSnapshotY = item.y;
            dragSnapshotRotation = item.rotated;
            dragSnapshotFolded = item.folded;
            dragSnapshotParent = item.parentContainerId;
            dragSnapshotEquipmentSlot = item.equippedSlot;
        }

        private float CurrentDragCell(ItemInstance item, float rootCell)
        {
            if (string.IsNullOrEmpty(item.parentContainerId)) return rootCell;
            ItemInstance parent = session.Player.stash.Find(candidate => candidate.instanceId == item.parentContainerId);
            if (parent != null && !string.IsNullOrEmpty(parent.equippedSlot)
                && TryGetEquippedContainerGrid(parent, out _, out float equippedCell)) return equippedCell;
            return InventoryLayout.CellSize;
        }

        private void RestoreDragState()
        {
            if (dragSnapshotItem == null) return;
            dragSnapshotItem.x = dragSnapshotX;
            dragSnapshotItem.y = dragSnapshotY;
            dragSnapshotItem.rotated = dragSnapshotRotation;
            dragSnapshotItem.folded = dragSnapshotFolded;
            dragSnapshotItem.parentContainerId = dragSnapshotParent;
            dragSnapshotItem.equippedSlot = dragSnapshotEquipmentSlot;
            ClearDragState();
        }

        private void ClearDragState()
        {
            dragSnapshotItem = null;
            dragSnapshotParent = null;
            dragSnapshotEquipmentSlot = null;
        }

        private void HandleStashInput(Vector2 origin, int columns, int rows, float cell)
        {
            Event e = Event.current;
            if (detailsItem != null && e.type == EventType.MouseDown)
            {
                Rect details = ItemDetailsRect();
                Rect close = new(details.x + details.width - 42, details.y + 12, 28, 28);
                if (e.button == 0 && (close.Contains(uiMouse) || !details.Contains(uiMouse))) detailsItem = null;
                e.Use();
                return;
            }
            if (pendingSaleItem != null && e.type == EventType.MouseDown)
            {
                Rect modal = SaleConfirmationRect();
                Rect confirm = new(modal.x + 22, modal.y + 142, 204, 48);
                Rect cancel = new(modal.x + 234, modal.y + 142, 204, 48);
                if (e.button == 0 && confirm.Contains(uiMouse)) ConfirmSale();
                else if (e.button == 0 && cancel.Contains(uiMouse)) pendingSaleItem = null;
                e.Use();
                return;
            }
            if (contextItem != null && e.type == EventType.MouseDown && e.button == 0)
            {
                Rect sell = new(contextMenuPosition.x, contextMenuPosition.y + 38, ContextMenuWidth, 42);
                Rect information = new(contextMenuPosition.x, contextMenuPosition.y + 82, ContextMenuWidth, 42);
                Rect characteristics = new(contextMenuPosition.x, contextMenuPosition.y + 126, ContextMenuWidth, 42);
                Rect loadMagazine = new(contextMenuPosition.x, contextMenuPosition.y + 170, ContextMenuWidth, 42);
                if (sell.Contains(uiMouse)) RequestSale();
                else if (information.Contains(uiMouse)) OpenInformationOrGunsmith();
                else if (characteristics.Contains(uiMouse)) ShowContextInformation(true);
                else if (loadMagazine.Contains(uiMouse) && AmmunitionService.IsMagazine(ItemCatalog.Get(contextItem.definitionId))) LoadContextMagazine();
                else contextItem = null;
                e.Use();
                return;
            }
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                ItemInstance hovered = FindItemUnderPointer(origin, cell);
                if (hovered != null)
                {
                    contextItem = hovered;
                    selectedItem = hovered;
                    bool magazineMenu = AmmunitionService.IsMagazine(ItemCatalog.Get(hovered.definitionId));
                    contextMenuPosition = new Vector2(
                        Mathf.Clamp(uiMouse.x, 8f, W - ContextMenuWidth - 8f),
                        Mathf.Clamp(uiMouse.y, 82f, H - (magazineMenu ? 228f : 184f)));
                }
                else contextItem = null;
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F)
            {
                ItemInstance hoveredItem = FindItemUnderPointer(origin, cell);
                if (hoveredItem != null && !string.IsNullOrEmpty(hoveredItem.parentContainerId))
                {
                    selectedItem = hoveredItem;
                    inventoryMessage = TryMoveToRoot(hoveredItem)
                        ? "Предмет быстро перемещён в хранилище"
                        : "В хранилище недостаточно свободного места";
                    e.Use();
                    return;
                }
                if (openContainer == null || !containerWindow.Contains(uiMouse))
                {
                    string hoveredSlot = EquipmentSlotAt(uiMouse);
                    ItemInstance equippedUnderPointer = hoveredSlot == null ? null : FindEquipped(hoveredSlot);
                    if (equippedUnderPointer != null)
                    {
                        selectedItem = equippedUnderPointer;
                        inventoryMessage = TryMoveToRoot(equippedUnderPointer)
                            ? "Экипировка быстро снята в хранилище"
                            : "В хранилище недостаточно свободного места";
                        e.Use();
                        return;
                    }
                }
                if (hoveredItem == null) return;
                selectedItem = hoveredItem;
                if (!string.IsNullOrEmpty(selectedItem.parentContainerId))
                {
                    inventoryMessage = TryMoveToRoot(selectedItem)
                        ? "Предмет быстро перемещён в хранилище"
                        : "В хранилище недостаточно свободного места";
                    e.Use();
                    return;
                }
                ItemSO selectedDefinition = ItemCatalog.Get(selectedItem.definitionId);
                if (!selectedDefinition.CanFold) inventoryMessage = "Этот предмет нельзя свернуть";
                else if (!string.IsNullOrEmpty(selectedItem.equippedSlot)) inventoryMessage = "Сначала снимите контейнер с оператора";
                else if (HasChildren(selectedItem)) inventoryMessage = "Сначала освободите контейнер";
                else
                {
                    if (openContainer == selectedItem) openContainer = null;
                    if (selectedItem.folded) BeginUnfoldPreview(selectedItem, InventoryLayout.CellSize);
                    else
                    {
                        bool wasFolded = selectedItem.folded;
                        ToggleFold(selectedItem);
                        inventoryMessage = selectedItem.folded != wasFolded ? "Контейнер свёрнут" : "Не удалось свернуть контейнер";
                    }
                }
                e.Use();
            }
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (openContainer != null)
                {
                    ContainerGrid(openContainer, out Vector2 innerOrigin, out float innerCell);
                    for (int i = session.Player.stash.Count - 1; i >= 0; i--)
                    {
                        ItemInstance item = session.Player.stash[i];
                        if (item.parentContainerId != openContainer.instanceId) continue;
                        Rect r = ItemRect(item, innerOrigin, innerCell);
                        if (!r.Contains(uiMouse)) continue;
                        selectedItem = item;
                        if (e.clickCount >= 2 && OpenByDoubleClick(item)) { draggedItem = null; e.Use(); return; }
                        unfoldingPreview = false; CaptureDragState(item); draggedItem = item; dragOffset = uiMouse - r.position; e.Use(); return;
                    }
                    // The floating window is modal for pointer input. Handle its chrome
                    // here, before any controls rendered underneath can see the event.
                    if (containerWindow.Contains(uiMouse))
                    {
                        Rect close = new(containerWindow.xMax - 30, containerWindow.y + 9, 24, 24);
                        if (close.Contains(uiMouse)) { openContainer = null; e.Use(); return; }
                        Rect header = new(containerWindow.x, containerWindow.y, containerWindow.width - 34, 46);
                        if (header.Contains(uiMouse))
                        {
                            movingContainerWindow = true;
                            containerWindowOffset = uiMouse - containerWindow.position;
                        }
                        e.Use(); return;
                    }
                }
                if (TryFindEquippedContainerItem(uiMouse, out ItemInstance equippedChild, out Rect equippedChildRect))
                {
                    selectedItem = equippedChild;
                    if (e.clickCount >= 2 && OpenByDoubleClick(equippedChild)) { draggedItem = null; e.Use(); return; }
                    unfoldingPreview = false;
                    CaptureDragState(equippedChild);
                    draggedItem = equippedChild;
                    dragOffset = uiMouse - equippedChildRect.position;
                    e.Use();
                    return;
                }
                string equippedSlot = EquipmentSlotAt(uiMouse);
                ItemInstance equippedItem = equippedSlot == null || !CanBeginEquipmentDrag(equippedSlot, uiMouse)
                    ? null
                    : FindEquipped(equippedSlot);
                if (equippedItem != null)
                {
                    selectedItem = equippedItem;
                    CaptureDragState(equippedItem);
                    draggedItem = equippedItem;
                    equipmentDragActive = true;
                    ItemCatalog.GetSize(equippedItem, out int dragWidth, out int dragHeight);
                    dragOffset = new Vector2(dragWidth * cell * .5f, dragHeight * cell * .5f);
                    inventoryMessage = "Перетащите экипировку в свободное место хранилища";
                    e.Use();
                    return;
                }
                for (int i = session.Player.stash.Count - 1; i >= 0; i--)
                {
                    ItemInstance item = session.Player.stash[i]; Rect r = ItemRect(item, origin, cell);
                    if (!string.IsNullOrEmpty(item.equippedSlot) || !string.IsNullOrEmpty(item.parentContainerId)) continue;
                    if (!r.Contains(uiMouse)) continue;
                    selectedItem = item;
                    if (e.clickCount >= 2 && OpenByDoubleClick(item)) { draggedItem = null; e.Use(); break; }
                    unfoldingPreview = false; CaptureDragState(item); draggedItem = item; dragOffset = uiMouse - r.position; e.Use(); break;
                }
            }
            if (draggedItem != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                float dragCell = CurrentDragCell(draggedItem, cell);
                ItemCatalog.GetSize(draggedItem, out int oldWidth, out int oldHeight);
                Vector2 oldCenter = new(oldWidth * dragCell * .5f, oldHeight * dragCell * .5f);
                draggedItem.rotated = !draggedItem.rotated;
                ItemCatalog.GetSize(draggedItem, out int newWidth, out int newHeight);
                Vector2 newCenter = new(newWidth * dragCell * .5f, newHeight * dragCell * .5f);
                dragOffset += newCenter - oldCenter;
                e.Use();
            }
            if (equipmentDragActive && draggedItem != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                RestoreDragState();
                equipmentDragActive = false;
                draggedItem = null;
                inventoryMessage = "Перемещение экипировки отменено";
                e.Use();
                return;
            }
            if (equipmentDragActive && draggedItem != null && e.type == EventType.MouseUp && e.button == 0)
            {
                ItemCatalog.GetSize(draggedItem, out int width, out int height);
                Rect rootGrid = new(origin.x, origin.y, columns * cell, rows * cell);
                int targetX = Mathf.RoundToInt((uiMouse.x - dragOffset.x - origin.x) / cell);
                int targetY = Mathf.RoundToInt((uiMouse.y - dragOffset.y - origin.y) / cell);
                bool overStorage = rootGrid.Contains(uiMouse);
                bool placed = overStorage && CanPlace(draggedItem, targetX, targetY, width, height, columns, rows);
                if (placed)
                {
                    draggedItem.equippedSlot = null;
                    draggedItem.parentContainerId = null;
                    draggedItem.x = targetX;
                    draggedItem.y = targetY;
                    ClearDragState();
                    session.Save();
                    inventoryMessage = "Экипировка перемещена в хранилище";
                }
                else
                {
                    RestoreDragState();
                    inventoryMessage = overStorage
                        ? "Недостаточно места — перенос отменён"
                        : "Перемещение отменено — предмет возвращён в экипировку";
                }
                equipmentDragActive = false;
                draggedItem = null;
                e.Use();
                return;
            }
            if (draggedItem != null && e.type == EventType.MouseUp && e.button == 0)
            {
                if (openContainer != null && TryContainerDrop(draggedItem, openContainer, uiMouse))
                {
                    selectedItem = draggedItem; unfoldingPreview = false; ClearDragState(); draggedItem = null; session.Save(); e.Use(); return;
                }
                if (TryEquippedContainerDrop(draggedItem, uiMouse))
                {
                    selectedItem = draggedItem; unfoldingPreview = false; ClearDragState(); draggedItem = null; session.Save();
                    inventoryMessage = "Предмет перемещён в экипированный контейнер";
                    e.Use(); return;
                }
                int quickInsert = TryQuickInsert(draggedItem, uiMouse, origin, cell);
                if (quickInsert != 0)
                {
                    if (quickInsert == 1)
                    {
                        selectedItem = draggedItem; unfoldingPreview = false; ClearDragState(); session.Save();
                        inventoryMessage = "Предмет помещён внутрь контейнера";
                    }
                    else
                    {
                        if (unfoldingPreview) CancelUnfoldPreview();
                        else RestoreDragState();
                        inventoryMessage = "В контейнере недостаточно свободного места";
                    }
                    draggedItem = null; e.Use(); return;
                }
                string equipmentSlot = EquipmentSlotAt(uiMouse);
                if (equipmentSlot != null && IsCompatible(draggedItem, equipmentSlot))
                {
                    ItemInstance previouslyEquipped = FindEquipped(equipmentSlot);
                    bool equipped = previouslyEquipped == null
                        ? EquipDirectly(draggedItem, equipmentSlot)
                        : TrySwapEquipment(draggedItem, previouslyEquipped, equipmentSlot);
                    if (equipped)
                    {
                        selectedItem = draggedItem;
                        unfoldingPreview = false;
                        inventoryMessage = previouslyEquipped == null ? "Предмет экипирован" : "Экипировка заменена";
                        ClearDragState();
                        draggedItem = null;
                        session.Save();
                    }
                    else
                    {
                        if (unfoldingPreview) CancelUnfoldPreview();
                        else RestoreDragState();
                        inventoryMessage = "Нет места, чтобы снять прежнюю экипировку";
                        draggedItem = null;
                    }
                    e.Use();
                    return;
                }
                ItemCatalog.GetSize(draggedItem, out int w, out int h);
                int x = Mathf.RoundToInt((uiMouse.x - dragOffset.x - origin.x) / cell);
                int y = Mathf.RoundToInt((uiMouse.y - dragOffset.y - origin.y) / cell);
                bool placed = CanPlace(draggedItem, x, y, w, h, columns, rows);
                if (placed)
                {
                    draggedItem.parentContainerId = null; draggedItem.x = x; draggedItem.y = y; unfoldingPreview = false; session.Save();
                    ClearDragState();
                    inventoryMessage = "Контейнер развёрнут и размещён";
                }
                else if (unfoldingPreview) CancelUnfoldPreview();
                else RestoreDragState();
                draggedItem = null; e.Use();
            }
        }

        private ItemInstance FindItemUnderPointer(Vector2 rootOrigin, float rootCell)
        {
            if (openContainer != null && containerWindow.Contains(uiMouse))
            {
                ContainerGrid(openContainer, out Vector2 innerOrigin, out float innerCell);
                for (int i = session.Player.stash.Count - 1; i >= 0; i--)
                {
                    ItemInstance item = session.Player.stash[i];
                    if (item.parentContainerId != openContainer.instanceId) continue;
                    if (ItemRect(item, innerOrigin, innerCell).Contains(uiMouse)) return item;
                }
                return null;
            }

            foreach (string slot in new[] { "rig", "backpack" })
            {
                ItemInstance container = FindEquipped(slot);
                if (container == null || !TryGetEquippedContainerGrid(container, out Vector2 equippedOrigin, out float equippedCell)) continue;
                ItemSO containerDefinition = ItemCatalog.Get(container.definitionId);
                Rect equippedGrid = new(equippedOrigin.x, equippedOrigin.y, containerDefinition.internalWidth * equippedCell, containerDefinition.internalHeight * equippedCell);
                if (!equippedGrid.Contains(uiMouse)) continue;
                for (int i = session.Player.stash.Count - 1; i >= 0; i--)
                {
                    ItemInstance item = session.Player.stash[i];
                    if (item.parentContainerId == container.instanceId && ItemRect(item, equippedOrigin, equippedCell).Contains(uiMouse)) return item;
                }
                return null;
            }

            for (int i = session.Player.stash.Count - 1; i >= 0; i--)
            {
                ItemInstance item = session.Player.stash[i];
                if (!string.IsNullOrEmpty(item.equippedSlot) || !string.IsNullOrEmpty(item.parentContainerId)) continue;
                if (ItemRect(item, rootOrigin, rootCell).Contains(uiMouse)) return item;
            }
            return null;
        }

        private bool TryFindEquippedContainerItem(Vector2 point, out ItemInstance foundItem, out Rect foundRect)
        {
            foundItem = null;
            foundRect = default;
            foreach (string slot in new[] { "rig", "backpack" })
            {
                ItemInstance container = FindEquipped(slot);
                if (container == null || !TryGetEquippedContainerGrid(container, out Vector2 gridOrigin, out float gridCell)) continue;
                ItemSO containerDefinition = ItemCatalog.Get(container.definitionId);
                Rect grid = new(gridOrigin.x, gridOrigin.y, containerDefinition.internalWidth * gridCell, containerDefinition.internalHeight * gridCell);
                if (!grid.Contains(point)) continue;
                for (int i = session.Player.stash.Count - 1; i >= 0; i--)
                {
                    ItemInstance item = session.Player.stash[i];
                    if (item.parentContainerId != container.instanceId) continue;
                    Rect itemRect = ItemRect(item, gridOrigin, gridCell);
                    if (!itemRect.Contains(point)) continue;
                    foundItem = item;
                    foundRect = itemRect;
                    return true;
                }
            }
            return false;
        }

        private bool TryEquippedContainerDrop(ItemInstance item, Vector2 point)
        {
            ItemInstance container = EquippedContainerAt(point);
            if (container == null || item == container || IsDescendant(container, item)) return false;
            if (!TryGetEquippedContainerGrid(container, out Vector2 gridOrigin, out float gridCell)) return false;
            ItemSO containerDefinition = ItemCatalog.Get(container.definitionId);
            Rect grid = new(gridOrigin.x, gridOrigin.y, containerDefinition.internalWidth * gridCell, containerDefinition.internalHeight * gridCell);
            if (!grid.Contains(point)) return false;
            ItemCatalog.GetSize(item, out int width, out int height);
            int x = Mathf.RoundToInt((point.x - dragOffset.x - gridOrigin.x) / gridCell);
            int y = Mathf.RoundToInt((point.y - dragOffset.y - gridOrigin.y) / gridCell);
            if (!CanPlaceInContainer(item, container, x, y, width, height)) return false;
            item.parentContainerId = container.instanceId;
            item.equippedSlot = null;
            item.x = x;
            item.y = y;
            return true;
        }

        private bool TryMoveToRoot(ItemInstance item)
        {
            if (item != null && item.permanent) { inventoryMessage = "Постоянный нож нельзя снять"; return false; }
            bool originalRotation = item.rotated;
            if (!FindSpaceInRoot(item, out int x, out int y))
            {
                item.rotated = !originalRotation;
                if (!FindSpaceInRoot(item, out x, out y))
                {
                    item.rotated = originalRotation;
                    return false;
                }
            }
            item.parentContainerId = null;
            item.equippedSlot = null;
            item.x = x;
            item.y = y;
            session.Save();
            return true;
        }

        private static bool EquipDirectly(ItemInstance item, string slot)
        {
            item.parentContainerId = null;
            item.equippedSlot = slot;
            ItemSO definition = ItemCatalog.Get(item.definitionId);
            if (definition.IsContainer) item.folded = false;
            return true;
        }

        private bool TrySwapEquipment(ItemInstance incoming, ItemInstance outgoing, string slot)
        {
            if (outgoing != null && outgoing.permanent) { inventoryMessage = "Постоянный нож нельзя заменить"; return false; }
            string incomingOldSlot = incoming.equippedSlot;
            string incomingOldParent = incoming.parentContainerId;
            int incomingOldX = incoming.x;
            int incomingOldY = incoming.y;
            bool incomingOldFolded = incoming.folded;

            // Temporarily equip the incoming item so its old stash cells become free
            // while we search for a place for the item being removed.
            incoming.parentContainerId = null;
            incoming.equippedSlot = slot;
            ItemSO incomingDefinition = ItemCatalog.Get(incoming.definitionId);
            if (incomingDefinition.IsContainer) incoming.folded = false;
            outgoing.equippedSlot = null;
            if (FindSpaceInRoot(outgoing, out int x, out int y))
            {
                outgoing.parentContainerId = null;
                outgoing.x = x;
                outgoing.y = y;
                return true;
            }

            outgoing.equippedSlot = slot;
            incoming.equippedSlot = incomingOldSlot;
            incoming.parentContainerId = incomingOldParent;
            incoming.x = incomingOldX;
            incoming.y = incomingOldY;
            incoming.folded = incomingOldFolded;
            return false;
        }

        private bool FindSpaceInRoot(ItemInstance item, out int foundX, out int foundY)
        {
            ItemCatalog.GetSize(item, out int width, out int height);
            for (int y = 0; y <= 11 - height; y++)
            for (int x = 0; x <= 10 - width; x++)
            {
                if (!CanPlace(item, x, y, width, height, 10, 11)) continue;
                foundX = x; foundY = y; return true;
            }
            foundX = -1; foundY = -1; return false;
        }

        private void BeginUnfoldPreview(ItemInstance item, float cell)
        {
            if (item == null || !item.folded || HasChildren(item) || !string.IsNullOrEmpty(item.equippedSlot)) return;
            unfoldOriginalX = item.x;
            unfoldOriginalY = item.y;
            unfoldOriginalParent = item.parentContainerId;
            item.folded = false;
            item.rotated = false;
            ItemCatalog.GetSize(item, out int width, out int height);
            draggedItem = item;
            selectedItem = item;
            unfoldingPreview = true;
            dragOffset = new Vector2(width * cell * .5f, height * cell * .5f);
            inventoryMessage = "Выберите свободное место для развёрнутого контейнера";
        }

        private void CancelUnfoldPreview()
        {
            if (draggedItem == null) return;
            draggedItem.folded = true;
            draggedItem.x = unfoldOriginalX;
            draggedItem.y = unfoldOriginalY;
            draggedItem.parentContainerId = unfoldOriginalParent;
            unfoldingPreview = false;
            inventoryMessage = "Недостаточно места — контейнер снова свёрнут";
        }

        private bool OpenByDoubleClick(ItemInstance item)
        {
            ItemSO definition = ItemCatalog.Get(item.definitionId);
            if (!definition.IsContainer) return false;
            if (item.folded)
            {
                inventoryMessage = "Сначала разверните контейнер клавишей F";
                return true;
            }
            openContainer = item;
            inventoryMessage = $"Открыт контейнер: {definition.name}";
            return true;
        }

        private void DrawContainerPanel()
        {
            ItemSO containerDef = ItemCatalog.Get(openContainer.definitionId);
            containerWindow.width = containerDef.internalWidth * InventoryLayout.CellSize + 12f;
            containerWindow.height = containerDef.internalHeight * InventoryLayout.CellSize + 58f;
            containerWindow.x = Mathf.Clamp(containerWindow.x, 15, W - containerWindow.width - 15);
            containerWindow.y = Mathf.Clamp(containerWindow.y, 90, H - containerWindow.height - 15);
            HandleContainerWindowMovement();
            Rect window = containerWindow;
            Fill(new Rect(window.x + 8, window.y + 10, window.width, window.height), new Color(0, 0, 0, .5f));
            Panel(window, new Color(.025f, .033f, .036f, .995f));
            Fill(new Rect(window.x, window.y, window.width, 46), new Color(.065f, .08f, .085f, 1f));
            Fill(new Rect(window.x, window.y, 4, 46), orange);
            GUI.Label(new Rect(window.x + 12, window.y + 7, window.width - 48, 32), containerDef.name, new GUIStyle(h2) { fontSize = 15 });
            Rect closeRect = new(window.xMax - 30, window.y + 9, 24, 24);
            GUI.Label(closeRect, "×", new GUIStyle(h2) { fontSize = 23, alignment = TextAnchor.MiddleCenter, normal = { textColor = closeRect.Contains(uiMouse) ? orange : new Color(.68f, .72f, .72f) } });
            if (openContainer == null) return;

            ContainerGrid(openContainer, out Vector2 origin, out float cell);
            Rect gridBack = new(origin.x - 3, origin.y - 3, containerDef.internalWidth * cell + 6, containerDef.internalHeight * cell + 6);
            Panel(gridBack, new Color(.02f, .027f, .029f, 1f));
            for (int y = 0; y < containerDef.internalHeight; y++)
            for (int x = 0; x < containerDef.internalWidth; x++)
            {
                Rect slot = new(origin.x + x * cell + 1, origin.y + y * cell + 1, cell - 2, cell - 2);
                Fill(slot, new Color(.09f, .105f, .108f, .86f)); Stroke(slot, new Color(.23f, .26f, .26f, .5f));
            }
            foreach (ItemInstance item in session.Player.stash)
            {
                if (item == draggedItem || item.parentContainerId != openContainer.instanceId) continue;
                DrawItem(ItemRect(item, origin, cell), item, item == selectedItem ? orange : line, 1f);
            }
        }

        private void ContainerGrid(ItemInstance container, out Vector2 origin, out float cell)
        {
            origin = new Vector2(containerWindow.x + 6, containerWindow.y + 52);
            cell = InventoryLayout.CellSize;
        }

        private void HandleContainerWindowMovement()
        {
            Event e = Event.current;
            if (movingContainerWindow && e.type == EventType.MouseDrag)
            {
                containerWindow.position = uiMouse - containerWindowOffset;
                containerWindow.x = Mathf.Clamp(containerWindow.x, 15, W - containerWindow.width - 15);
                containerWindow.y = Mathf.Clamp(containerWindow.y, 90, H - containerWindow.height - 15);
                e.Use();
            }
            else if (movingContainerWindow && e.type == EventType.MouseUp && e.button == 0)
            {
                movingContainerWindow = false;
                e.Use();
            }
        }

        private bool TryContainerDrop(ItemInstance item, ItemInstance container, Vector2 point)
        {
            ItemSO targetDef = ItemCatalog.Get(container.definitionId);
            if (!targetDef.IsContainer || container.folded || item == container || IsDescendant(container, item)) return false;
            if (ItemCatalog.Get(item.definitionId).IsContainer && ContainerDepth(container) >= 3) return false;
            ContainerGrid(container, out Vector2 origin, out float cell);
            Rect grid = new(origin.x, origin.y, targetDef.internalWidth * cell, targetDef.internalHeight * cell);
            if (!grid.Contains(point)) return false;
            ItemCatalog.GetSize(item, out int w, out int h);
            int x = Mathf.RoundToInt((point.x - dragOffset.x - origin.x) / cell);
            int y = Mathf.RoundToInt((point.y - dragOffset.y - origin.y) / cell);
            if (!CanPlaceInContainer(item, container, x, y, w, h)) return false;
            item.parentContainerId = container.instanceId; item.equippedSlot = null; item.x = x; item.y = y;
            return true;
        }

        // Returns 0 when no container is under the pointer, 1 on success,
        // and 2 when a container was hit but has no suitable free rectangle.
        private int TryQuickInsert(ItemInstance moving, Vector2 point, Vector2 rootOrigin, float rootCell)
        {
            ItemInstance target = EquippedContainerAt(point);
            bool overFloatingWindow = openContainer != null && containerWindow.Contains(point);
            for (int i = session.Player.stash.Count - 1; i >= 0 && !overFloatingWindow && target == null; i--)
            {
                ItemInstance candidate = session.Player.stash[i];
                if (candidate == moving || !string.IsNullOrEmpty(candidate.equippedSlot) || !string.IsNullOrEmpty(candidate.parentContainerId)) continue;
                ItemSO definition = ItemCatalog.Get(candidate.definitionId);
                if (!definition.IsContainer || candidate.folded) continue;
                if (ItemRect(candidate, rootOrigin, rootCell).Contains(point)) { target = candidate; break; }
            }

            if (target == null && openContainer != null && overFloatingWindow)
            {
                ContainerGrid(openContainer, out Vector2 innerOrigin, out float innerCell);
                for (int i = session.Player.stash.Count - 1; i >= 0; i--)
                {
                    ItemInstance candidate = session.Player.stash[i];
                    if (candidate == moving || candidate.parentContainerId != openContainer.instanceId) continue;
                    ItemSO definition = ItemCatalog.Get(candidate.definitionId);
                    if (!definition.IsContainer || candidate.folded) continue;
                    if (ItemRect(candidate, innerOrigin, innerCell).Contains(point)) { target = candidate; break; }
                }
            }

            if (target == null) return 0;
            if (target == moving || IsDescendant(target, moving) || ContainerDepth(target) >= 3) return 2;

            bool originalRotation = moving.rotated;
            if (!FindSpaceInContainer(moving, target, out int x, out int y))
            {
                moving.rotated = !originalRotation;
                if (!FindSpaceInContainer(moving, target, out x, out y))
                {
                    moving.rotated = originalRotation;
                    return 2;
                }
            }

            moving.parentContainerId = target.instanceId;
            moving.equippedSlot = null;
            moving.x = x;
            moving.y = y;
            return 1;
        }

        private ItemInstance EquippedContainerAt(Vector2 point)
        {
            if (characterPanelTab != 0) return null;
            float sx = EquipmentViewportX;
            float sy = EquipmentViewportY - equipmentScroll.y;
            Rect rig = GetEquipmentContainerRect("rig"); rig.position += new Vector2(sx, sy);
            Rect backpack = GetEquipmentContainerRect("backpack"); backpack.position += new Vector2(sx, sy);
            if (rig.Contains(point)) return FindEquipped("rig");
            if (backpack.Contains(point)) return FindEquipped("backpack");
            return null;
        }

        private bool TryGetEquippedContainerGrid(ItemInstance container, out Vector2 origin, out float cell)
        {
            origin = Vector2.zero;
            cell = 0f;
            if (container == null || characterPanelTab != 0) return false;
            ItemSO definition = ItemCatalog.Get(container.definitionId);
            if (container.equippedSlot != "rig" && container.equippedSlot != "backpack") return false;
            Rect panelRect = GetEquipmentContainerRect(container.equippedSlot);
            cell = InventoryLayout.CellSize;
            origin = new Vector2(EquipmentViewportX + panelRect.x + 10f, EquipmentViewportY - equipmentScroll.y + panelRect.y + 45f);
            return true;
        }

        private bool FindSpaceInContainer(ItemInstance moving, ItemInstance container, out int foundX, out int foundY)
        {
            ItemSO target = ItemCatalog.Get(container.definitionId);
            ItemCatalog.GetSize(moving, out int width, out int height);
            for (int y = 0; y <= target.internalHeight - height; y++)
            for (int x = 0; x <= target.internalWidth - width; x++)
            {
                if (!CanPlaceInContainer(moving, container, x, y, width, height)) continue;
                foundX = x; foundY = y; return true;
            }
            foundX = -1; foundY = -1; return false;
        }

        private bool CanPlaceInContainer(ItemInstance moving, ItemInstance container, int x, int y, int w, int h)
        {
            ItemSO target = ItemCatalog.Get(container.definitionId);
            if (x < 0 || y < 0 || x + w > target.internalWidth || y + h > target.internalHeight) return false;
            RectInt rect = new(x, y, w, h);
            foreach (ItemInstance other in session.Player.stash)
            {
                if (other == moving || other.parentContainerId != container.instanceId) continue;
                ItemCatalog.GetSize(other, out int ow, out int oh);
                if (rect.Overlaps(new RectInt(other.x, other.y, ow, oh))) return false;
            }
            return true;
        }

        private bool IsDescendant(ItemInstance possibleChild, ItemInstance possibleParent)
        {
            ItemInstance current = possibleChild;
            for (int i = 0; i < 8 && current != null && !string.IsNullOrEmpty(current.parentContainerId); i++)
            {
                if (current.parentContainerId == possibleParent.instanceId) return true;
                current = session.Player.stash.Find(value => value.instanceId == current.parentContainerId);
            }
            return false;
        }

        private int ContainerDepth(ItemInstance item)
        {
            int depth = 1;
            for (int i = 0; i < 8 && item != null && !string.IsNullOrEmpty(item.parentContainerId); i++)
            {
                depth++;
                item = session.Player.stash.Find(value => value.instanceId == item.parentContainerId);
            }
            return depth;
        }

        private bool HasChildren(ItemInstance container) => session.Player.stash.Exists(item => item.parentContainerId == container.instanceId);

        private void ToggleFold(ItemInstance item)
        {
            ItemSO d = ItemCatalog.Get(item.definitionId);
            if (!d.CanFold || !string.IsNullOrEmpty(item.equippedSlot) || HasChildren(item)) return;
            bool old = item.folded;
            item.folded = !item.folded;
            ItemCatalog.GetSize(item, out int w, out int h);
            bool placed = string.IsNullOrEmpty(item.parentContainerId)
                ? CanPlace(item, item.x, item.y, w, h, 10, 11)
                : CanPlaceInContainer(item, session.Player.stash.Find(value => value.instanceId == item.parentContainerId), item.x, item.y, w, h);
            if (!placed) item.folded = old;
            else session.Save();
        }

        private bool CanPlace(ItemInstance moving, int x, int y, int w, int h, int columns, int rows)
        {
            if (x < 0 || y < 0 || x + w > columns || y + h > rows) return false;
            RectInt target = new(x, y, w, h);
            foreach (ItemInstance item in session.Player.stash)
            {
                if (item == moving || !string.IsNullOrEmpty(item.equippedSlot) || !string.IsNullOrEmpty(item.parentContainerId)) continue;
                ItemSO d = ItemCatalog.Get(item.definitionId);
                ItemCatalog.GetSize(item, out int iw, out int ih);
                if (target.Overlaps(new RectInt(item.x, item.y, iw, ih))) return false;
            }
            return true;
        }

        private Rect ItemRect(ItemInstance item, Vector2 origin, float cell)
        {
            ItemSO d = ItemCatalog.Get(item.definitionId);
            ItemCatalog.GetSize(item, out int w, out int h);
            return new Rect(origin.x + item.x * cell + 2, origin.y + item.y * cell + 2, w * cell - 4, h * cell - 4);
        }

        private void DrawItem(Rect r, ItemInstance item, Color border, float alpha)
        {
            ItemSO d = ItemCatalog.Get(item.definitionId);
            Color c = Color.Lerp(new Color(.035f, .043f, .045f), d.color, .24f); c.a = alpha;
            Fill(r, c); Stroke(r, border);
            DrawItemIcon(new Rect(r.x + 3, r.y + 19, r.width - 6, Mathf.Max(8, r.height - 22)), d, alpha * .96f, item.rotated);
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, 18), new Color(.018f, .024f, .026f, .76f * alpha));
            GUI.Label(new Rect(r.x + 6, r.y + 1, r.width - 12, 17), d.name, new GUIStyle(small) { fontSize = 10, normal = { textColor = Color.white }, clipping = TextClipping.Clip });
            if (item.quantity > 1) GUI.Label(new Rect(r.x + 5, r.yMax - 24, r.width - 10, 20), $"×{item.quantity}", Right(small));
            if (AmmunitionService.IsMagazine(d))
                GUI.Label(new Rect(r.x + 5, r.yMax - 24, r.width - 10, 20),
                    $"{item.loadedAmmoCount}/{AmmunitionService.MagazineCapacity(item)}", Right(small));
        }

        private void DrawInventoryTooltip(Vector2 rootOrigin, float rootCell)
        {
            if (contextItem != null || pendingSaleItem != null || detailsItem != null) return;
            ItemInstance hovered = FindItemUnderPointer(rootOrigin, rootCell);
            if (hovered == null) return;
            ItemSO definition = ItemCatalog.Get(hovered.definitionId);
            float width = 250f;
            float height = 64f;
            float x = Mathf.Min(uiMouse.x + 18f, W - width - 12f);
            float y = Mathf.Min(uiMouse.y + 18f, H - height - 12f);
            Rect tooltip = new(x, y, width, height);
            Fill(tooltip, new Color(.012f, .018f, .02f, .97f));
            Stroke(tooltip, new Color(.36f, .4f, .4f, .9f));
            GUI.Label(new Rect(x + 12, y + 7, width - 24, 20), definition.name, new GUIStyle(small) { normal = { textColor = Color.white }, clipping = TextClipping.Clip });
            GUI.Label(new Rect(x + 12, y + 31, width - 24, 20), $"≈ {definition.price:N0} ₽     F — ПЕРЕМЕСТИТЬ     R — ПОВЕРНУТЬ", new GUIStyle(small) { fontSize = 10 });
        }

        private void DrawItemIcon(Rect rect, ItemSO definition, float alpha = 1f, bool rotated = false)
        {
            Color previous = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            if (rotated)
            {
                Vector3 pivot = new(rect.center.x, rect.center.y, 0f);
                GUI.matrix = previousMatrix
                    * Matrix4x4.Translate(pivot)
                    * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f))
                    * Matrix4x4.Translate(-pivot);
            }
            if (definition.icon != null)
            {
                Rect spriteRect = rotated
                    ? new Rect(rect.center.x - rect.height * .5f, rect.center.y - rect.width * .5f, rect.height, rect.width)
                    : rect;
                GUI.DrawTexture(spriteRect, definition.icon.texture, ScaleMode.ScaleToFit, true);
            }
            else if (itemAtlas != null && definition.atlasIconIndex >= 0)
            {
                int column = definition.atlasIconIndex % 4;
                int row = definition.atlasIconIndex / 4;
                Rect uv = new(column * .25f, 1f - (row + 1) * .25f, .25f, .25f);
                float side = Mathf.Min(rect.width, rect.height);
                Rect fitted = new(rect.center.x - side * .5f, rect.center.y - side * .5f, side, side);
                GUI.DrawTextureWithTexCoords(fitted, itemAtlas, uv, true);
            }
            GUI.matrix = previousMatrix;
            GUI.color = previous;
        }

        private void EquipmentSlot(float x, float y, float w, float h, string slotId, string label)
        {
            Rect r = new(x, y, w, h);
            ItemInstance equipped = FindEquipped(slotId);
            bool compatible = selectedItem != null && IsCompatible(selectedItem, slotId);
            bool dragCompatible = draggedItem != null && IsCompatible(draggedItem, slotId);
            Color fill = compatible || dragCompatible ? new Color(.16f, .27f, .19f, .88f) : new Color(.09f, .105f, .108f, .72f);
            Color border = compatible || dragCompatible ? new Color(.28f, .75f, .38f, 1f) : line;
            Fill(r, fill); Stroke(r, border);
            ItemSO equippedDefinition = equipped == null ? null : ItemCatalog.Get(equipped.definitionId);
            if (equipped == null)
            {
                GUI.Label(new Rect(x + 10, y + 7, w - 20, 20), label, small);
                GUI.Label(new Rect(x + 12, y + 35, w - 24, 24), compatible || dragCompatible ? "МОЖНО ЭКИПИРОВАТЬ" : "ПУСТО", new GUIStyle(small) { normal = { textColor = compatible || dragCompatible ? new Color(.55f, .9f, .58f) : new Color(.35f, .39f, .39f) } });
            }
            else
            {
                string state = equippedDefinition.category == ItemCategory.Weapon
                    ? $"{equipped.loadedAmmoCount}/{AmmunitionService.WeaponMagazineCapacity(equipped)}"
                    : $"{equipped.condition}%";
                Rect header = new(x + 1, y + 1, w - 2, 25);
                Fill(header, new Color(.035f, .044f, .046f, .98f));
                GUI.Label(new Rect(x + 8, y + 4, w - 78, 19), equippedDefinition.name, new GUIStyle(small) { normal = { textColor = Color.white }, clipping = TextClipping.Clip });
                GUI.Label(new Rect(x + w - 68, y + 4, 58, 19), state, Right(small));
                DrawItemIcon(new Rect(x + 4, y + 29, w - 8, h - 33), equippedDefinition, .95f, equipped.rotated);
            }
        }

        private void DrawPlacementPreview(Vector2 origin, int columns, int rows, float cell)
        {
            if (draggedItem == null) return;
            ItemInstance equippedContainer = EquippedContainerAt(uiMouse);
            if (equippedContainer != null && TryGetEquippedContainerGrid(equippedContainer, out Vector2 equippedOrigin, out float equippedCell))
            {
                ItemSO equippedDefinition = ItemCatalog.Get(equippedContainer.definitionId);
                Rect equippedGrid = new(equippedOrigin.x, equippedOrigin.y, equippedDefinition.internalWidth * equippedCell, equippedDefinition.internalHeight * equippedCell);
                if (equippedGrid.Contains(uiMouse))
                {
                    ItemCatalog.GetSize(draggedItem, out int ew, out int eh);
                    int ex = Mathf.RoundToInt((uiMouse.x - dragOffset.x - equippedOrigin.x) / equippedCell);
                    int ey = Mathf.RoundToInt((uiMouse.y - dragOffset.y - equippedOrigin.y) / equippedCell);
                    bool validEquipped = draggedItem != equippedContainer && !IsDescendant(equippedContainer, draggedItem)
                        && CanPlaceInContainer(draggedItem, equippedContainer, ex, ey, ew, eh);
                    Rect equippedPreview = new(equippedOrigin.x + ex * equippedCell + 1, equippedOrigin.y + ey * equippedCell + 1, ew * equippedCell - 2, eh * equippedCell - 2);
                    Fill(equippedPreview, validEquipped ? new Color(.12f, .72f, .28f, .34f) : new Color(.85f, .12f, .08f, .36f));
                    Stroke(equippedPreview, validEquipped ? new Color(.25f, 1f, .45f, .95f) : new Color(1f, .2f, .15f, .95f));
                    return;
                }
            }
            if (EquipmentSlotAt(uiMouse) != null) return;
            if (openContainer != null)
            {
                ItemSO target = ItemCatalog.Get(openContainer.definitionId);
                ContainerGrid(openContainer, out Vector2 innerOrigin, out float innerCell);
                Rect innerRect = new(innerOrigin.x, innerOrigin.y, target.internalWidth * innerCell, target.internalHeight * innerCell);
                if (innerRect.Contains(uiMouse))
                {
                    ItemCatalog.GetSize(draggedItem, out int iw, out int ih);
                    int ix = Mathf.RoundToInt((uiMouse.x - dragOffset.x - innerOrigin.x) / innerCell);
                    int iy = Mathf.RoundToInt((uiMouse.y - dragOffset.y - innerOrigin.y) / innerCell);
                    bool allowed = draggedItem != openContainer && !IsDescendant(openContainer, draggedItem) && CanPlaceInContainer(draggedItem, openContainer, ix, iy, iw, ih);
                    Rect innerPreview = new(innerOrigin.x + ix * innerCell + 1, innerOrigin.y + iy * innerCell + 1, iw * innerCell - 2, ih * innerCell - 2);
                    Fill(innerPreview, allowed ? new Color(.12f, .72f, .28f, .34f) : new Color(.85f, .12f, .08f, .36f));
                    Stroke(innerPreview, allowed ? new Color(.25f, 1f, .45f, .95f) : new Color(1f, .2f, .15f, .95f));
                    return;
                }
            }
            ItemSO d = ItemCatalog.Get(draggedItem.definitionId);
            ItemCatalog.GetSize(draggedItem, out int w, out int h);
            int x = Mathf.RoundToInt((uiMouse.x - dragOffset.x - origin.x) / cell);
            int y = Mathf.RoundToInt((uiMouse.y - dragOffset.y - origin.y) / cell);
            bool valid = CanPlace(draggedItem, x, y, w, h, columns, rows);
            Color fill = valid ? new Color(.12f, .72f, .28f, .34f) : new Color(.85f, .12f, .08f, .36f);
            Color border = valid ? new Color(.25f, 1f, .45f, .95f) : new Color(1f, .2f, .15f, .95f);
            Rect preview = new(origin.x + x * cell + 1, origin.y + y * cell + 1, w * cell - 2, h * cell - 2);
            Fill(preview, fill); Stroke(preview, border);
        }

        private string EquipmentSlotAt(Vector2 point)
        {
            if (characterPanelTab != 0) return null;
            float sx = EquipmentViewportX;
            float sy = EquipmentViewportY - equipmentScroll.y;
            Rect rig = GetEquipmentContainerRect("rig"); rig.position += new Vector2(sx, sy);
            Rect backpack = GetEquipmentContainerRect("backpack"); backpack.position += new Vector2(sx, sy);
            if (rig.Contains(point)) return "rig";
            if (backpack.Contains(point)) return "backpack";
            for (int i = 0; i < 4; i++)
                if (new Rect(sx + 570 + i * 52, sy + 42, InventoryLayout.CellSize, InventoryLayout.CellSize).Contains(point)) return $"pocket_{i}";
            if (new Rect(sx + 10, sy + 15, 130, 115).Contains(point)) return "headset";
            if (new Rect(sx + 10, sy + 140, 130, 150).Contains(point)) return "armor";
            if (new Rect(sx + 420, sy + 15, 130, 115).Contains(point)) return "helmet";
            if (new Rect(sx + 420, sy + 140, 130, 115).Contains(point)) return "face_cover";
            if (new Rect(sx + 420, sy + 265, 130, 105).Contains(point)) return "secure";
            if (new Rect(sx + 10, sy + 400, 135, 95).Contains(point)) return "holster";
            if (new Rect(sx + 10, sy + 505, 135, 95).Contains(point)) return "melee";
            if (new Rect(sx + 155, sy + 400, 395, 95).Contains(point)) return "main_weapon";
            if (new Rect(sx + 155, sy + 505, 395, 95).Contains(point)) return "second_weapon";
            return null;
        }

        private bool CanBeginEquipmentDrag(string slot, Vector2 point)
        {
            ItemInstance equipped = FindEquipped(slot);
            if (equipped != null && equipped.permanent) return false;
            if (slot != "rig" && slot != "backpack") return true;
            float sx = EquipmentViewportX;
            float sy = EquipmentViewportY - equipmentScroll.y;
            Rect header = GetEquipmentContainerRect(slot);
            header.position += new Vector2(sx, sy);
            header.height = 42f;
            return header.Contains(point);
        }

        private static bool IsCompatible(ItemInstance item, string slot)
        {
            ItemSO definition = ItemCatalog.Get(item.definitionId);
            ItemCategory category = definition.category;
            if (slot == "main_weapon" || slot == "second_weapon") return category == ItemCategory.Weapon;
            if (slot == "holster") return item.definitionId.StartsWith("pistol_");
            if (slot == "melee") return item.definitionId.StartsWith("melee_");
            if (slot == "armor") return item.definitionId == "armor_t3";
            if (slot == "helmet") return item.definitionId.StartsWith("helmet_");
            if (slot == "headset") return item.definitionId.StartsWith("headset_");
            if (slot == "face_cover") return item.definitionId.StartsWith("face_");
            if (slot == "rig") return item.definitionId.StartsWith("rig_");
            if (slot == "backpack") return category == ItemCategory.Backpack;
            if (slot.StartsWith("pocket_"))
            {
                ItemCatalog.GetSize(item, out int width, out int height);
                return width == 1 && height == 1;
            }
            return false;
        }

        private ItemInstance FindEquipped(string slot)
        {
            return session.Player.stash.Find(item => item.equippedSlot == slot);
        }

        private void Unequip(ItemInstance item)
        {
            if (item == null || item.permanent) { inventoryMessage = "Постоянный нож нельзя снять"; return; }
            ItemSO d = ItemCatalog.Get(item.definitionId);
            ItemCatalog.GetSize(item, out int itemWidth, out int itemHeight);
            for (int y = 0; y <= 11 - itemHeight; y++)
            for (int x = 0; x <= 10 - itemWidth; x++)
            {
                if (!CanPlace(item, x, y, itemWidth, itemHeight, 10, 11)) continue;
                item.equippedSlot = null; item.rotated = false; item.x = x; item.y = y; selectedItem = item; session.Save(); return;
            }
        }

        private static string CategoryName(ItemCategory c) => c switch
        {
            ItemCategory.Weapon => "ОРУЖИЕ", ItemCategory.Armor => "БРОНЯ", ItemCategory.Ammo => "БОЕПРИПАСЫ",
            ItemCategory.Medical => "МЕДИЦИНА", ItemCategory.Backpack => "РЮКЗАК", _ => "ЦЕННЫЙ ПРЕДМЕТ"
        };

        private void Bunker(Rect a)
        {
            bool powered = BunkerService.IsPowered(session.Player);
            GUI.Label(new Rect(a.x, a.y, 600, 28), "ПОДЗЕМНЫЙ КОМПЛЕКС", small);
            GUI.Label(new Rect(a.x, a.y + 28, 700, 52), "БУНКЕР", h1);
            int bunkerLevel = 0;
            foreach (BunkerModuleState state in session.Player.bunkerModules) bunkerLevel += state.level;
            GUI.Label(new Rect(a.x + 1010, a.y + 39, 470, 28), $"{(powered ? "ЭНЕРГИЯ В НОРМЕ" : "НЕТ ЭНЕРГИИ")}   ·   УРОВЕНЬ {bunkerLevel}", Right(powered ? h2 : danger));

            Rect menu = new(a.x, a.y + 92, 235, 575);
            Panel(menu, new Color(.025f, .032f, .034f, .94f));
            string[] sections = { "ОБЗОР", "МОДУЛИ", "УЛУЧШЕНИЯ", "СКЛАД", "ЗАЛ СЛАВЫ" };
            for (int i = 0; i < sections.Length; i++)
            {
                Rect button = new(menu.x + 14, menu.y + 20 + i * 58, menu.width - 28, 46);
                if (GUI.Button(button, sections[i], bunkerSection == i ? navOn : nav)) bunkerSection = i;
            }
            GUI.Label(new Rect(menu.x + 18, menu.yMax - 86, menu.width - 36, 22), "СОСТОЯНИЕ", small);
            GUI.Label(new Rect(menu.x + 18, menu.yMax - 59, menu.width - 36, 42), powered ? "Генератор работает\nЭффективность: 100%" : "Аварийное освещение\nЭффективность: 50%", body);

            Rect room = new(a.x + 255, a.y + 92, 785, 575);
            Panel(room, powered ? new Color(.025f, .031f, .032f, .88f) : new Color(.010f, .012f, .012f, .98f));
            GUI.Label(new Rect(room.x + 24, room.y + 20, 500, 28), bunkerSection == 4 ? "КОЛЛЕКЦИЯ ТРОФЕЕВ" : "СХЕМА ПОМЕЩЕНИЙ", h2);
            GUI.Label(new Rect(room.x + 24, room.y + 52, 700, 24), "Выберите объект для просмотра и улучшения", small);
            TimeSpan powerLeft = BunkerService.RemainingPower(session.Player);
            GUI.Label(new Rect(room.x + 485, room.y + 18, 270, 22), powered ? $"ТОПЛИВО: {Mathf.FloorToInt((float)powerLeft.TotalHours):00}:{powerLeft.Minutes:00}" : "ТОПЛИВО ЗАКОНЧИЛОСЬ", Right(small));
            int fuelCount = BunkerService.Count(session.Player.stash, "fuel_can");
            GUI.enabled = BunkerService.GetLevel(session.Player, "generator") > 0 && fuelCount > 0;
            if (GUI.Button(new Rect(room.x + 555, room.y + 47, 200, 34), $"ЗАПРАВИТЬ · {fuelCount}", nav)) session.AddBunkerFuel();
            GUI.enabled = true;

            var modules = new List<BunkerModuleSO>(BunkerCatalog.All);
            modules.Sort((left, right) => string.CompareOrdinal(left.id, right.id));
            for (int i = 0; i < modules.Count; i++)
            {
                BunkerModuleSO module = modules[i];
                int column = i % 2, row = i / 2;
                Rect card = new(room.x + 24 + column * 370, room.y + 95 + row * 137, 346, 116);
                bool selected = module.id == selectedBunkerModuleId;
                Fill(card, selected ? new Color(.20f, .14f, .07f, .92f) : new Color(.055f, .065f, .065f, .96f));
                Stroke(card, selected ? orange : line);
                int level = BunkerService.GetLevel(session.Player, module.id);
                GUI.Label(new Rect(card.x + 18, card.y + 14, card.width - 90, 24), module.displayName, h2);
                GUI.Label(new Rect(card.x + 18, card.y + 47, card.width - 36, 40), module.description, new GUIStyle(small) { wordWrap = true });
                GUI.Label(new Rect(card.x + card.width - 75, card.y + 14, 58, 24), $"{level}/{module.MaxLevel}", Right(h2));
                Fill(new Rect(card.x + 18, card.yMax - 13, card.width - 36, 3), line);
                if (level > 0) Fill(new Rect(card.x + 18, card.yMax - 13, (card.width - 36) * level / module.MaxLevel, 3), orange);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) selectedBunkerModuleId = module.id;
            }
            if (!powered)
            {
                GUI.Label(new Rect(room.x + 25, room.yMax - 58, 440, 24), "СВЕЧИ · ПОМЕЩЕНИЯ ЕЛЕ ОСВЕЩЕНЫ", danger);
                for (int i = 0; i < 4; i++)
                {
                    float candleX = room.x + 505 + i * 58;
                    Fill(new Rect(candleX - 10, room.yMax - 75, 28, 28), new Color(1f, .38f, .05f, .08f));
                    Fill(new Rect(candleX, room.yMax - 55, 8, 25), new Color(.72f, .60f, .40f, .9f));
                    Fill(new Rect(candleX + 1, room.yMax - 66, 6, 13), new Color(1f, .52f, .10f, .85f));
                }
            }

            Rect detail = new(a.x + 1060, a.y + 92, 420, 575);
            Panel(detail, new Color(.035f, .043f, .044f, .96f));
            DrawBunkerDetails(detail);
        }

        private void DrawBunkerDetails(Rect rect)
        {
            BunkerModuleSO module = BunkerCatalog.Get(selectedBunkerModuleId);
            if (module == null) { GUI.Label(new Rect(rect.x + 24, rect.y + 24, 360, 30), "МОДУЛИ ЗАГРУЖАЮТСЯ", h2); return; }
            int level = BunkerService.GetLevel(session.Player, module.id);
            GUI.Label(new Rect(rect.x + 24, rect.y + 24, rect.width - 48, 28), module.displayName, h2);
            GUI.Label(new Rect(rect.x + 24, rect.y + 60, rect.width - 48, 24), $"УРОВЕНЬ {level} ИЗ {module.MaxLevel}", small);
            GUI.Label(new Rect(rect.x + 24, rect.y + 102, rect.width - 48, 58), module.description, new GUIStyle(body) { wordWrap = true });
            if (level >= module.MaxLevel)
            {
                GUI.Label(new Rect(rect.x + 24, rect.y + 190, rect.width - 48, 28), "МАКСИМАЛЬНЫЙ УРОВЕНЬ", h2);
                return;
            }
            BunkerLevelData next = module.levels[level];
            GUI.Label(new Rect(rect.x + 24, rect.y + 180, rect.width - 48, 24), $"БОНУС УРОВНЯ {level + 1}", small);
            GUI.Label(new Rect(rect.x + 24, rect.y + 211, rect.width - 48, 46), $"{next.bonusDescription}\nЭффективность: {BunkerService.Efficiency(session.Player) * 100f:0}%", h2);
            GUI.Label(new Rect(rect.x + 24, rect.y + 285, rect.width - 48, 22), "ТРЕБУЕМЫЕ РЕСУРСЫ", small);
            int y = 320;
            bool ready = true;
            foreach (BunkerRequirement requirement in next.requirements)
            {
                int owned = BunkerService.Count(session.Player.stash, requirement.itemId);
                ready &= owned >= requirement.quantity;
                ItemSO item = ItemCatalog.Get(requirement.itemId);
                GUI.Label(new Rect(rect.x + 24, rect.y + y, 250, 28), item.name, body);
                GUI.Label(new Rect(rect.x + 280, rect.y + y, 110, 28), $"{owned} / {requirement.quantity}", Right(owned >= requirement.quantity ? body : danger));
                y += 38;
            }
            GUI.enabled = ready;
            if (GUI.Button(new Rect(rect.x + 24, rect.yMax - 70, rect.width - 48, 46), "УЛУЧШИТЬ МОДУЛЬ", action)) session.UpgradeBunker(module.id);
            GUI.enabled = true;
        }

        private void Abilities(Rect a)
        {
            GUI.Label(new Rect(a.x, a.y, 600, 30), "РАЗВИТИЕ ОПЕРАТОРА", small);
            GUI.Label(new Rect(a.x, a.y + 29, 650, 58), "СПОСОБНОСТИ", h1);
            GUI.Label(new Rect(a.x + 870, a.y + 42, 335, 32), $"СВОБОДНЫЕ ОЧКИ   {session.Player.abilityPoints}", Right(h2));
            scroll = GUI.BeginScrollView(new Rect(a.x, a.y + 120, a.width, 590), scroll, new Rect(0, 0, a.width - 22, 620));
            for (int i = 0; i < abilities.Length; i++)
            {
                AbilityDef d = abilities[i]; int rank = session.Player.GetAbilityRank(d.id);
                Rect c = new(0, i * 96, a.width - 30, 82); Panel(c, i % 2 == 0 ? panel : panel2); Fill(new Rect(0, c.y, 4, 82), rank > 0 ? orange : line);
                GUI.Label(new Rect(24, c.y + 13, 400, 26), d.name, h2); GUI.Label(new Rect(24, c.y + 44, 480, 23), d.description, body);
                GUI.Label(new Rect(560, c.y + 15, 250, 23), $"БОНУС {d.bonus} / РАНГ", small); Ranks(new Rect(560, c.y + 51, 250, 9), rank);
                GUI.enabled = session.Player.abilityPoints > 0 && rank < 5;
                if (GUI.Button(new Rect(930, c.y + 17, 220, 48), rank >= 5 ? "МАКСИМУМ" : $"УЛУЧШИТЬ  {rank}/5", action)) session.Upgrade(d.id);
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        private void Profile(Rect a)
        {
            PlayerData p = session.Player; PlayerStatistics s = p.statistics;
            GUI.Label(new Rect(a.x, a.y, 600, 30), "ЛИЧНОЕ ДЕЛО", small); GUI.Label(new Rect(a.x, a.y + 29, 800, 58), p.playerName.ToUpperInvariant(), h1);
            Rect id = new(a.x, a.y + 125, 365, 500); Panel(id, panel); Fill(new Rect(id.x, id.y, id.width, 7), orange);
            GUI.Label(new Rect(id.x + 28, id.y + 34, 310, 30), $"УРОВЕНЬ {p.level}", h2); GUI.Label(new Rect(id.x + 28, id.y + 75, 310, 22), $"{p.experience:N0} / {p.ExperienceForNextLevel:N0} XP", small);
            float progress = p.ExperienceForNextLevel == 0 ? 0 : (float)p.experience / p.ExperienceForNextLevel; Fill(new Rect(id.x + 28, id.y + 112, 309, 4), line); Fill(new Rect(id.x + 28, id.y + 112, 309 * progress, 4), orange);
            GUI.Label(new Rect(id.x + 28, id.y + 160, 310, 22), "РАЗБЛОКИРОВАНО СКИНОВ", small); GUI.Label(new Rect(id.x + 28, id.y + 190, 310, 52), p.unlockedSkinIds.Count.ToString("00"), h1);
            int next = Mathf.Min(Progression.MaxLevel, ((p.level / 5) + 1) * 5); GUI.Label(new Rect(id.x + 28, id.y + 278, 310, 22), "СЛЕДУЮЩАЯ НАГРАДА", small); GUI.Label(new Rect(id.x + 28, id.y + 310, 310, 34), $"СКИН · УРОВЕНЬ {next}", h2);
            Rect st = new(a.x + 395, a.y + 125, 810, 500); Panel(st, panel); GUI.Label(new Rect(st.x + 28, st.y + 25, 500, 30), "БОЕВАЯ СТАТИСТИКА", h2); Fill(new Rect(st.x + 28, st.y + 70, 754, 1), line);
            Stat(st.x + 28, st.y + 95, "РЕЙДЫ", s.raids.ToString("N0")); Stat(st.x + 278, st.y + 95, "ВЫЖИВАЕМОСТЬ", $"{s.SurvivalRate:0.0}%"); Stat(st.x + 528, st.y + 95, "УБИЙСТВА", s.kills.ToString("N0"));
            Stat(st.x + 28, st.y + 225, "СМЕРТИ", s.deaths.ToString("N0")); Stat(st.x + 278, st.y + 225, "К / Д", s.KillDeathRatio.ToString("0.00")); Stat(st.x + 528, st.y + 225, "ЭВАКУАЦИИ", s.survivedRaids.ToString("N0"));
            GUI.Label(new Rect(st.x + 28, st.y + 380, 400, 22), "ОБЩАЯ СТОИМОСТЬ ДОБЫЧИ", small); GUI.Label(new Rect(st.x + 28, st.y + 412, 600, 45), $"{s.extractedValue:N0} ₽", h1);
        }

        private void Placeholder(Rect a, string title, string text)
        {
            GUI.Label(new Rect(a.x, a.y, 600, 30), "МОДУЛЬ В РАЗРАБОТКЕ", small); GUI.Label(new Rect(a.x, a.y + 29, 1000, 58), title, h1);
            Rect c = new(a.x, a.y + 145, a.width, 250); Panel(c, panel); Fill(new Rect(c.x, c.y, 7, c.height), orange);
            GUI.Label(new Rect(c.x + 38, c.y + 48, c.width - 80, 42), text, h2); GUI.Label(new Rect(c.x + 38, c.y + 118, c.width - 80, 65), "Основа интерфейса готова. Здесь появятся интерактивные предметы, фильтры и подробная информация.", body);
        }

        private void Pair(float x, float y, string a, string b) { GUI.Label(new Rect(x, y, 300, 22), a, small); GUI.Label(new Rect(x, y + 25, 330, 30), b, h2); }
        private void Loadout(float x, float y, string text) { GUI.Label(new Rect(x, y, 300, 21), text, small); GUI.Label(new Rect(x, y + 23, 330, 27), "НЕ ВЫБРАНО", h2); }
        private void Stat(float x, float y, string a, string b) { GUI.Label(new Rect(x, y, 220, 22), a, small); GUI.Label(new Rect(x, y + 29, 220, 50), b, h1); }
        private void Tag(Rect r, string text) { Fill(r, new Color(0.13f, 0.15f, 0.155f)); Stroke(r, line); GUI.Label(r, text, Center(small)); }
        private void Ranks(Rect r, int rank) { float w = (r.width - 20) / 5; for (int i = 0; i < 5; i++) Fill(new Rect(r.x + i * (w + 5), r.y, w, r.height), i < rank ? orange : line); }
        private void Panel(Rect r, Color c) { Fill(r, c); Stroke(r, line); }
        private void Stroke(Rect r, Color c) { Fill(new Rect(r.x, r.y, r.width, 1), c); Fill(new Rect(r.x, r.yMax - 1, r.width, 1), c); Fill(new Rect(r.x, r.y, 1, r.height), c); Fill(new Rect(r.xMax - 1, r.y, 1, r.height), c); }
        private void Fill(Rect r, Color c) { Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, pixel); GUI.color = old; }

        private void Styles()
        {
            if (logo != null) return;
            logo = Text(24, FontStyle.Bold, Color.white); h1 = Text(42, FontStyle.Bold, Color.white); h2 = Text(18, FontStyle.Bold, new Color(0.9f, 0.92f, 0.92f));
            body = Text(15, FontStyle.Normal, new Color(0.68f, 0.71f, 0.71f)); body.wordWrap = true; small = Text(12, FontStyle.Bold, new Color(0.50f, 0.55f, 0.55f));
            nav = Button(15, new Color(0.66f, 0.69f, 0.69f), new Color(0.055f, 0.065f, 0.068f)); nav.alignment = TextAnchor.MiddleLeft; nav.padding = new RectOffset(22, 8, 0, 0);
            navOn = Button(15, Color.white, new Color(0.13f, 0.145f, 0.15f)); navOn.alignment = TextAnchor.MiddleLeft; navOn.padding = new RectOffset(22, 8, 0, 0);
            action = Button(14, Color.white, new Color(0.72f, 0.30f, 0.055f)); danger = Button(14, new Color(0.88f, 0.88f, 0.88f), new Color(0.19f, 0.065f, 0.055f));
        }

        private GUIStyle Text(int size, FontStyle weight, Color c) => new(GUI.skin.label) { fontSize = size, fontStyle = weight, alignment = TextAnchor.MiddleLeft, normal = { textColor = c } };
        private GUIStyle Button(int size, Color text, Color bg)
        {
            Texture2D n = Rounded(bg), hover = Rounded(Color.Lerp(bg, orange, .23f));
            return new GUIStyle(GUI.skin.button) { fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = text, background = n }, hover = { textColor = Color.white, background = hover }, active = { textColor = Color.white, background = hover }, border = new RectOffset(9, 9, 9, 9) };
        }
        private static GUIStyle Center(GUIStyle s) => new(s) { alignment = TextAnchor.MiddleCenter };
        private static GUIStyle Right(GUIStyle s) => new(s) { alignment = TextAnchor.MiddleRight };
        private static Texture2D Solid(Color c) { var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; t.SetPixel(0, 0, c); t.Apply(); return t; }
        private static Texture2D Rounded(Color c)
        {
            const int size = 32;
            const float radius = 7.5f;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius), 0f);
                float alpha = Mathf.Clamp01(radius + .65f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(c.r, c.g, c.b, c.a * alpha);
            }
            t.SetPixels(pixels); t.Apply(); return t;
        }
        private static Texture2D Backdrop()
        {
            const int w = 512, h = 288; var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp }; var p = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { float v = 1 - (float)y / h; float glow = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x, y), new Vector2(405, 52)) / 260); float n = Mathf.PerlinNoise(x * .055f, y * .055f) * .018f; p[y * w + x] = Color.Lerp(new Color(.018f, .025f, .028f), new Color(.07f, .09f, .095f), v) + new Color(.09f, .035f, .003f) * glow + new Color(n, n, n); }
            t.SetPixels(p); t.Apply(); return t;
        }

        private readonly struct AbilityDef
        {
            public readonly string id, name, description, bonus;
            public AbilityDef(string id, string name, string description, string bonus) { this.id = id; this.name = name; this.description = description; this.bonus = bonus; }
        }
    }
}
