using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OfflineExtraction.Raid
{
    public sealed class RaidInventoryUI : MonoBehaviour
    {
        private const float W = 1600f;
        private const float H = 900f;
        public static bool IsOpen { get; private set; }
        private RaidLootContainer openedContainer;
        private int tab;
        private ItemInstance hoveredItem;
        private bool hoveredFromContainer;
        private string message = "TAB — закрыть · F — быстрое действие";
        private Texture2D operatorImage, healthImage, itemAtlas;
        private Vector2 storageScroll;
        private ItemInstance draggedItem;
        private bool draggedFromContainer;
        private readonly List<DropZone> dropZones = new();
        private readonly List<ItemHit> itemHits = new();
        private Vector2 dropZoneOffset;
        private ItemInstance contextItem;
        private Vector2 contextPosition;
        private ItemInstance detailedWeapon;
        private ItemInstance actionMagazine;
        private ItemInstance actionAmmo;
        private bool actionAmmoFromContainer;
        private ItemInstance unloadAmmoTarget;
        private bool actionUnloading;
        private int actionTotal;
        private int actionCompleted;
        private float actionRoundTimer;
        private ItemInstance healingItem;
        private string healingTarget;
        private float healingStartedAt;
        private float healingDuration;
        private bool selectingBodyPart;
        private const float RoundActionTime = .22f;

        private enum ZoneKind { Equipment, Grid, Pocket, Loot }
        private sealed class DropZone
        {
            public Rect rect;
            public ZoneKind kind;
            public string parentId;
            public string slot;
            public int columns, rows;
            public float cell;
            public Vector2 origin;
        }
        private sealed class ItemHit { public Rect rect; public ItemInstance item; public bool fromContainer; }

        private void Awake()
        {
            operatorImage = Resources.Load<Texture2D>("UI/operator_equipment");
            Texture2D dynamicBody = Resources.Load<Texture2D>("UI/body_health_dynamic");
            healthImage = dynamicBody != null ? ExtractBody(dynamicBody) : Resources.Load<Texture2D>("UI/body_health");
            itemAtlas = Resources.Load<Texture2D>("UI/item_atlas");
        }

        private void Update()
        {
            if (RaidBootstrap.IsPaused) return;
            AdvanceContainerSearch();
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            AdvanceRoundAction();
            AdvanceHealing();
            if (keyboard.tabKey.wasPressedThisFrame) Toggle(null);
            if (IsOpen && keyboard.rKey.wasPressedThisFrame)
            {
                ItemHit hit = ItemHitUnderPointer();
                if (hit != null)
                {
                    hoveredItem = hit.item;
                    hoveredFromContainer = hit.fromContainer;
                    TryRotateHovered();
                }
            }
            if (IsOpen && keyboard.fKey.wasPressedThisFrame && hoveredItem != null)
            {
                if (hoveredItem.permanent) { message = "Постоянный нож нельзя перемещать или выбросить"; return; }
                if (hoveredFromContainer) MoveToEquipment(hoveredItem);
                else if (openedContainer != null) MoveToContainer(hoveredItem);
                else Drop(hoveredItem);
            }
        }

        private void OnDestroy() => IsOpen = false;

        public void Toggle(RaidLootContainer container)
        {
            if (container != null)
            {
                openedContainer = container;
                IsOpen = true;
            }
            else IsOpen = !IsOpen;
            if (!IsOpen) { openedContainer = null; CancelRoundAction(); CancelHealing(); contextItem = null; detailedWeapon = null; }
            Cursor.lockState = IsOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = IsOpen;
        }

        public void Close()
        {
            IsOpen = false; openedContainer = null; draggedItem = null;
            CancelRoundAction(); CancelHealing();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            float interfaceScale = Mathf.Min(Screen.width / W, Screen.height / H);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * interfaceScale) * .5f, (Screen.height - H * interfaceScale) * .5f), Quaternion.identity, Vector3.one * interfaceScale);
            hoveredItem = null;
            dropZones.Clear();
            itemHits.Clear();
            GUI.Box(new Rect(0, 0, W, H), GUIContent.none);
            float margin = 24f;
            float equipmentWidth = openedContainer == null ? W - margin * 2 : W * .50f - margin * 1.5f;
            Rect equipment = new(margin, 74, equipmentWidth, H - 98);
            GUI.Box(equipment, GUIContent.none);
            if (GUI.Button(new Rect(margin, 18, 190, 42), "СНАРЯЖЕНИЕ")) tab = 0;
            if (GUI.Button(new Rect(margin + 200, 18, 190, 42), "ЗДОРОВЬЕ")) tab = 1;
            GUI.Label(new Rect(W - 420, 25, 390, 30), message);
            if (tab == 0) DrawEquipment(equipment); else DrawHealth(equipment);
            if (openedContainer != null) DrawLoot(new Rect(W * .51f, 74, W * .47f, H - 98));
            ProcessDrag();
            DrawContextMenu();
            DrawWeaponDetails();
            DrawRoundProgress();
            DrawBodyPartSelector();
            DrawHealingProgress();
            GUI.matrix = oldMatrix;
        }

        private void DrawEquipment(Rect rect)
        {
            RaidLoadout loadout = RaidContext.Loadout;
            if (loadout == null) return;
            float contentTop = rect.y + 44f;
            float contentHeight = Mathf.Max(220f, rect.height - 54f);
            float leftWidth = rect.width * .50f;
            float rightX = rect.x + leftWidth + 8f;
            float rightWidth = rect.width - leftWidth - 18f;
            GUI.Label(new Rect(rect.x + 16, rect.y + 12, 420, 26), "СНАРЯЖЕНИЕ ОПЕРАТОРА");
            Rect operatorRect = new(rect.x + leftWidth * .30f, contentTop, leftWidth * .42f, contentHeight * .62f);
            if (operatorImage != null) GUI.DrawTexture(operatorRect, operatorImage, ScaleMode.ScaleToFit, true);

            float sideWidth = Mathf.Max(68f, leftWidth * .24f);
            float sideHeight = Mathf.Max(62f, contentHeight * .19f);
            DrawSlot(new Rect(rect.x + 10, contentTop + 10, sideWidth, sideHeight), "headset", "НАУШНИКИ");
            DrawSlot(new Rect(rect.x + 10, contentTop + sideHeight + 22, sideWidth, sideHeight * 1.15f), "armor", "БРОНЕЖИЛЕТ");
            DrawSlot(new Rect(rect.x + leftWidth - sideWidth - 8, contentTop + 10, sideWidth, sideHeight), "helmet", "ШЛЕМ");
            DrawSlot(new Rect(rect.x + leftWidth - sideWidth - 8, contentTop + sideHeight + 22, sideWidth, sideHeight), "face_cover", "ЗАЩИТА ЛИЦА");
            DrawSlot(new Rect(rect.x + leftWidth - sideWidth - 8, contentTop + sideHeight * 2 + 34, sideWidth, sideHeight), "secure", "КОНТЕЙНЕР");

            float weaponY = contentTop + contentHeight * .64f;
            float smallWeapon = leftWidth * .28f;
            float largeWeapon = leftWidth - smallWeapon - 26f;
            float weaponHeight = Mathf.Max(48f, contentHeight * .145f);
            DrawSlot(new Rect(rect.x + 10, weaponY, smallWeapon, weaponHeight), "holster", "ПИСТОЛЕТ");
            DrawSlot(new Rect(rect.x + 10, weaponY + weaponHeight + 6, smallWeapon, weaponHeight), "melee", "ХОЛОДНОЕ ОРУЖИЕ");
            DrawSlot(new Rect(rect.x + smallWeapon + 16, weaponY, largeWeapon, weaponHeight), "main_weapon", "ОСНОВНОЕ ОРУЖИЕ");
            DrawSlot(new Rect(rect.x + smallWeapon + 16, weaponY + weaponHeight + 6, largeWeapon, weaponHeight), "second_weapon", "ВТОРОЕ ОРУЖИЕ");

            ItemInstance rig = loadout.items.Find(item => item.equippedSlot == "rig");
            ItemInstance backpack = loadout.items.Find(item => item.equippedSlot == "backpack");
            float rigWidth = rig == null ? 220f : ItemCatalog.Get(rig.definitionId).internalWidth * InventoryLayout.CellSize + 12f;
            float backpackWidth = backpack == null ? 220f : ItemCatalog.Get(backpack.definitionId).internalWidth * InventoryLayout.CellSize + 12f;
            float rigHeight = rig == null ? 92f : ItemCatalog.Get(rig.definitionId).internalHeight * InventoryLayout.CellSize + 34f;
            float backpackHeight = backpack == null ? 92f : ItemCatalog.Get(backpack.definitionId).internalHeight * InventoryLayout.CellSize + 34f;
            float storageContentWidth = Mathf.Max(rightWidth - 18f, Mathf.Max(rigWidth, backpackWidth));
            float storageContentHeight = 24f + InventoryLayout.CellSize + 14f + rigHeight + 8f + backpackHeight + 10f;
            Rect storageViewport = new(rightX, contentTop, rightWidth, contentHeight);
            storageScroll = GUI.BeginScrollView(storageViewport, storageScroll, new Rect(0, 0, storageContentWidth, storageContentHeight));
            dropZoneOffset = new Vector2(storageViewport.x - storageScroll.x, storageViewport.y - storageScroll.y);

            GUI.Label(new Rect(0, 0, storageContentWidth, 22), "КАРМАНЫ");
            float pocketGap = 4f;
            float pocketSize = InventoryLayout.CellSize;
            for (int i = 0; i < 4; i++)
            {
                Rect pocket = new(i * (pocketSize + pocketGap), 24, pocketSize, pocketSize);
                DrawSlot(pocket, $"pocket_{i}", (i + 1).ToString());
                AddDropZone(new DropZone { rect = pocket, kind = ZoneKind.Pocket, slot = $"pocket_{i}" });
            }
            float containersY = pocketSize + 38f;
            DrawContainerPanel(new Rect(0, containersY, rigWidth, rigHeight), "rig", "РАЗГРУЗКА");
            DrawContainerPanel(new Rect(0, containersY + rigHeight + 8f, backpackWidth, backpackHeight), "backpack", "РЮКЗАК");
            GUI.EndScrollView();
            dropZoneOffset = Vector2.zero;
        }

        private void DrawSlot(Rect card, string slot, string label)
        {
            RaidLoadout loadout = RaidContext.Loadout;
            ItemInstance item = loadout.items.Find(value => value.equippedSlot == slot);
            GUI.Box(card, GUIContent.none);
            if (!slot.StartsWith("pocket_")) AddDropZone(new DropZone { rect = card, kind = ZoneKind.Equipment, slot = slot });
            if (card.height < 50f)
            {
                if (item != null) { DrawItemIcon(new Rect(card.x + 2, card.y + 2, card.width - 4, card.height - 4), ItemCatalog.Get(item.definitionId), item); DrawItemState(card, item); Hover(card, item, false); }
                else GUI.Label(card, label);
                return;
            }
            GUI.Label(new Rect(card.x + 6, card.y + 4, card.width - 12, 19), item == null ? label : ItemCatalog.Get(item.definitionId).name);
            if (item != null) { DrawItemIcon(new Rect(card.x + 6, card.y + 25, card.width - 12, card.height - 31), ItemCatalog.Get(item.definitionId), item); DrawItemState(card, item); Hover(card, item, false); }
            else GUI.Label(new Rect(card.x + 6, card.y + 28, card.width - 12, 20), "ПУСТО");
        }

        private void DrawContainerPanel(Rect panel, string slot, string label)
        {
            RaidLoadout loadout = RaidContext.Loadout;
            ItemInstance container = loadout.items.Find(item => item.equippedSlot == slot);
            if (container == null)
            {
                GUI.Box(panel, GUIContent.none);
                GUI.Label(new Rect(panel.x + 8, panel.y + 5, panel.width - 16, 20), label + " · ПУСТО");
                AddDropZone(new DropZone { rect = panel, kind = ZoneKind.Equipment, slot = slot });
                return;
            }
            ItemSO definition = ItemCatalog.Get(container.definitionId);
            float cell = InventoryLayout.CellSize;
            Rect fittedPanel = new(panel.x, panel.y, definition.internalWidth * cell + 12, definition.internalHeight * cell + 34);
            GUI.Box(fittedPanel, GUIContent.none);
            GUI.Label(new Rect(fittedPanel.x + 8, fittedPanel.y + 5, fittedPanel.width - 16, 20), definition.name);
            // Заголовок контейнера служит зоной замены экипированной разгрузки/рюкзака.
            // Сама сетка остается зоной помещения предметов внутрь контейнера.
            AddDropZone(new DropZone { rect = new Rect(fittedPanel.x, fittedPanel.y, fittedPanel.width, 27f), kind = ZoneKind.Equipment, slot = slot });
            Vector2 origin = new(panel.x + 6, panel.y + 28);
            Rect gridRect = new(origin.x, origin.y, definition.internalWidth * cell, definition.internalHeight * cell);
            DrawGrid(origin, definition.internalWidth, definition.internalHeight, cell);
            AddDropZone(new DropZone { rect = gridRect, kind = ZoneKind.Grid, parentId = container.instanceId, columns = definition.internalWidth, rows = definition.internalHeight, cell = cell, origin = origin });
            foreach (ItemInstance item in loadout.items.ToArray())
            {
                if (item.parentContainerId != container.instanceId) continue;
                ItemCatalog.GetSize(item, out int w, out int h);
                Rect itemRect = new(origin.x + item.x * cell, origin.y + item.y * cell, w * cell, h * cell);
                GUI.Box(itemRect, GUIContent.none); DrawItemIcon(itemRect, ItemCatalog.Get(item.definitionId), item); DrawItemState(itemRect, item); Hover(itemRect, item, false);
            }
        }

        private void DrawLoot(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 18, rect.y + 14, rect.width - 36, 26), openedContainer.displayName);
            float cell = InventoryLayout.CellSize;
            Vector2 origin = new(rect.x + 18, rect.y + 54); DrawGrid(origin, openedContainer.columns, openedContainer.rows, cell);
            AddDropZone(new DropZone { rect = new Rect(origin.x, origin.y, openedContainer.columns * cell, openedContainer.rows * cell), kind = ZoneKind.Loot, columns = openedContainer.columns, rows = openedContainer.rows, cell = cell, origin = origin });
            for (int i = 0; i < openedContainer.items.Count; i++)
            {
                ItemInstance item = openedContainer.items[i]; ItemCatalog.GetSize(item, out int w, out int h);
                Rect itemRect = new(origin.x + item.x * cell, origin.y + item.y * cell, w * cell, h * cell);
                bool revealed = i < openedContainer.revealedCount;
                GUI.Box(itemRect, GUIContent.none);
                if (revealed)
                {
                    ItemSO definition = ItemCatalog.Get(item.definitionId);
                    DrawItemIcon(new Rect(itemRect.x + 3, itemRect.y + 20, itemRect.width - 6, itemRect.height - 23), definition, item);
                    GUI.Label(new Rect(itemRect.x + 4, itemRect.y + 2, itemRect.width - 8, 19), definition.name);
                    DrawItemState(itemRect, item);
                    Hover(itemRect, item, true);
                }
                else
                {
                    Color old = GUI.color; GUI.color = new Color(.42f,.44f,.44f,.92f); GUI.DrawTexture(new Rect(itemRect.x + 2, itemRect.y + 2, itemRect.width - 4, itemRect.height - 4), Texture2D.whiteTexture); GUI.color = old;
                }
            }
        }

        private void DrawHealth(Rect rect)
        {
            PlayerVitals v = RaidContext.Loadout?.vitals ?? new PlayerVitals();
            GUI.Label(new Rect(rect.x + 25, rect.y + 20, 400, 28), $"ОБЩЕЕ ЗДОРОВЬЕ   {v.CurrentHealth} / {PlayerVitals.MaxHealth}");
            int bleeding = v.bleedingParts?.Count ?? 0, fractures = v.fracturedParts?.Count ?? 0;
            if (bleeding > 0 || fractures > 0) GUI.Label(new Rect(rect.x + 300, rect.y + 20, 430, 28), $"КРОВОТЕЧЕНИЕ: {bleeding}   ПЕРЕЛОМЫ: {fractures}");
            Rect bodyRect = new(rect.center.x - Mathf.Min(150, rect.width * .18f), rect.y + 62, Mathf.Min(300, rect.width * .36f), rect.height - 95);
            RaidBodyFigure.Draw(bodyRect, v);
            Health(new Rect(rect.x + 25, rect.y + 85, 210, 38), "ГОЛОВА", v.head, 35, IsBleeding(v, "head"));
            Health(new Rect(rect.x + 25, rect.y + 165, 210, 38), "ПРАВАЯ РУКА", v.rightArm, 60, IsBleeding(v, "rightArm"));
            Health(new Rect(rect.x + 25, rect.y + 265, 210, 38), "ПРАВАЯ НОГА", v.rightLeg, 65, IsBleeding(v, "rightLeg"));
            Health(new Rect(rect.xMax - 235, rect.y + 85, 210, 38), "ГРУДЬ", v.chest, 85, IsBleeding(v, "chest"));
            Health(new Rect(rect.xMax - 235, rect.y + 165, 210, 38), "ЛЕВАЯ РУКА", v.leftArm, 60, IsBleeding(v, "leftArm"));
            Health(new Rect(rect.xMax - 235, rect.y + 225, 210, 38), "ЖИВОТ", v.abdomen, 70, IsBleeding(v, "abdomen"));
            Health(new Rect(rect.xMax - 235, rect.y + 305, 210, 38), "ЛЕВАЯ НОГА", v.leftLeg, 65, IsBleeding(v, "leftLeg"));
        }

        private void AdvanceContainerSearch()
        {
            if (!IsOpen || openedContainer == null || openedContainer.revealedCount >= openedContainer.items.Count) return;
            openedContainer.revealTimer += Time.unscaledDeltaTime;
            while (openedContainer.revealedCount < openedContainer.items.Count)
            {
                float delay = openedContainer.revealedCount == 0 ? .75f : 1.05f;
                if (openedContainer.revealTimer < delay) break;
                openedContainer.revealTimer -= delay;
                openedContainer.revealedCount++;
            }
        }

        private static bool IsBleeding(PlayerVitals vitals, string part) => vitals.bleedingParts != null && vitals.bleedingParts.Contains(part);

        private static void Health(Rect rect, string label, int value, int max, bool bleeding)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 20), $"{label}   {value}/{max}");
            if (bleeding)
            {
                Color previous = GUI.color; GUI.color = new Color(.95f,.08f,.06f);
                GUI.Label(new Rect(rect.xMax - 25, rect.y - 2, 24, 22), "♦", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold });
                GUI.color = previous;
            }
            GUI.Box(new Rect(rect.x, rect.y + 24, rect.width, 6), GUIContent.none);
            Color old = GUI.color; float ratio = max == 0 ? 0 : (float)value / max;
            GUI.color = value <= 0 ? Color.black : ratio > .8f ? new Color(.25f,.85f,.35f) : ratio > .3f ? new Color(1f,.68f,.1f) : new Color(1f,.18f,.12f);
            GUI.DrawTexture(new Rect(rect.x, rect.y + 24, rect.width * ratio, 6), Texture2D.whiteTexture); GUI.color = old;
        }

        private void MoveToEquipment(ItemInstance item)
        {
            if (TryPlaceInLoadout(item)) { openedContainer.items.Remove(item); message = "Предмет перемещён в снаряжение"; }
            else message = "В снаряжении недостаточно места";
        }

        public bool PickUp(ItemInstance item)
        {
            if (!TryPlaceInLoadout(item)) { message = "Нет места — предмет оставлен на земле"; return false; }
            message = $"Подобрано: {ItemCatalog.Get(item.definitionId).name}"; return true;
        }

        private static bool TryPlaceInLoadout(ItemInstance item)
        {
            RaidLoadout loadout = RaidContext.Loadout;
            if (loadout == null || item == null) return false;
            ItemCatalog.GetSize(item, out int width, out int height);
            foreach (string slot in new[] { "rig", "backpack" })
            {
                ItemInstance parent = loadout.items.Find(value => value.equippedSlot == slot);
                if (parent == null) continue;
                ItemSO container = ItemCatalog.Get(parent.definitionId);
                for (int y = 0; y <= container.internalHeight - height; y++)
                for (int x = 0; x <= container.internalWidth - width; x++)
                    if (CanPlace(loadout.items, item, parent.instanceId, x, y, width, height))
                    {
                        item.parentContainerId = parent.instanceId; item.equippedSlot = null; item.x = x; item.y = y;
                        if (!loadout.items.Contains(item)) loadout.items.Add(item);
                        return true;
                    }
            }
            if (width == 1 && height == 1)
            {
                for (int i = 0; i < 4; i++)
                {
                    string pocket = $"pocket_{i}";
                    if (loadout.items.Exists(value => value.equippedSlot == pocket)) continue;
                    item.parentContainerId = null; item.equippedSlot = pocket; item.x = item.y = 0;
                    if (!loadout.items.Contains(item)) loadout.items.Add(item);
                    return true;
                }
            }
            return false;
        }

        private void MoveToContainer(ItemInstance item)
        {
            if (item != null && item.permanent) { message = "Постоянный нож нельзя положить в контейнер"; return; }
            ItemCatalog.GetSize(item, out int w, out int h);
            for (int y = 0; y <= openedContainer.rows - h; y++) for (int x = 0; x <= openedContainer.columns - w; x++)
                if (CanPlace(openedContainer.items, item, null, x, y, w, h))
                { RaidContext.Loadout.items.Remove(item); item.parentContainerId = null; item.equippedSlot = null; item.x = x; item.y = y; openedContainer.items.Add(item); message = "Предмет помещён в контейнер"; return; }
            message = "В контейнере недостаточно места";
        }

        private void Drop(ItemInstance item)
        {
            if (item == null || item.permanent) { message = "Постоянный нож нельзя выбросить"; return; }
            RaidContext.Loadout.items.Remove(item);
            Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (camera != null)
            {
                GameObject dropped = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dropped.name = "Выброшено · " + ItemCatalog.Get(item.definitionId).name;
                dropped.transform.localScale = new Vector3(.38f, .12f, .48f);
                Vector3 intended = camera.transform.position + camera.transform.forward * 1.05f;
                if (Physics.Raycast(intended + Vector3.up * 1.5f, Vector3.down, out RaycastHit surface, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    dropped.transform.position = surface.point + surface.normal * .075f;
                else dropped.transform.position = intended;
                dropped.transform.rotation = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
                item.parentContainerId = null; item.equippedSlot = null;
                dropped.AddComponent<RaidDroppedItem>().item = item;
                Rigidbody body = dropped.AddComponent<Rigidbody>();
                body.mass = .35f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
            message = $"Выброшено: {ItemCatalog.Get(item.definitionId).name}";
        }
        private static bool CanPlace(List<ItemInstance> items, ItemInstance moving, string parent, int x, int y, int w, int h)
        {
            RectInt candidate = new(x, y, w, h);
            foreach (ItemInstance other in items) { if (other == moving || other.parentContainerId != parent) continue; ItemCatalog.GetSize(other, out int ow, out int oh); if (candidate.Overlaps(new RectInt(other.x, other.y, ow, oh))) return false; }
            return true;
        }
        private void DrawItemIcon(Rect rect, ItemSO definition, ItemInstance item = null)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            bool rotated = item != null && item.rotated && definition.width != definition.height;
            if (rotated)
            {
                Vector2 center = rect.center;
                GUI.matrix = oldMatrix
                    * Matrix4x4.Translate(new Vector3(center.x, center.y, 0f))
                    * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f))
                    * Matrix4x4.Translate(new Vector3(-center.x, -center.y, 0f));
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
                int column = definition.atlasIconIndex % 4, row = definition.atlasIconIndex / 4;
                Rect uv = new(column * .25f, 1f - (row + 1) * .25f, .25f, .25f);
                // Каждая картинка атласа квадратная. Вписываем её в центр,
                // вместо растягивания на весь прямоугольник предмета.
                float side = Mathf.Min(rect.width, rect.height);
                Rect fitted = new(rect.center.x - side * .5f, rect.center.y - side * .5f, side, side);
                GUI.DrawTextureWithTexCoords(fitted, itemAtlas, uv, true);
            }
            else
            {
                Color old = GUI.color; GUI.color = definition.color;
                GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
            }
            GUI.matrix = oldMatrix;
        }

        private ItemHit ItemHitUnderPointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || itemHits.Count == 0) return null;
            float interfaceScale = Mathf.Min(Screen.width / W, Screen.height / H);
            if (interfaceScale <= 0f) return null;
            Vector2 screen = mouse.position.ReadValue();
            screen.y = Screen.height - screen.y;
            Vector2 offset = new((Screen.width - W * interfaceScale) * .5f, (Screen.height - H * interfaceScale) * .5f);
            Vector2 point = (screen - offset) / interfaceScale;
            for (int i = itemHits.Count - 1; i >= 0; i--)
                if (itemHits[i].rect.Contains(point)) return itemHits[i];
            return null;
        }

        private void TryRotateHovered()
        {
            ItemInstance item = hoveredItem;
            if (item == null || item.permanent) return;
            ItemSO definition = ItemCatalog.Get(item.definitionId);
            if (definition.width == definition.height) { message = "Квадратный предмет не требует поворота"; return; }
            if (!string.IsNullOrEmpty(item.equippedSlot)) { message = "Экипированный предмет поворачивать нельзя"; return; }

            item.rotated = !item.rotated;
            ItemCatalog.GetSize(item, out int width, out int height);
            bool valid = false;
            if (hoveredFromContainer && openedContainer != null)
                valid = item.x >= 0 && item.y >= 0 && item.x + width <= openedContainer.columns && item.y + height <= openedContainer.rows
                    && CanPlace(openedContainer.items, item, null, item.x, item.y, width, height);
            else if (!string.IsNullOrEmpty(item.parentContainerId) && RaidContext.Loadout != null)
            {
                ItemInstance parent = RaidContext.Loadout.items.Find(value => value.instanceId == item.parentContainerId);
                if (parent != null)
                {
                    ItemSO container = ItemCatalog.Get(parent.definitionId);
                    valid = item.x >= 0 && item.y >= 0 && item.x + width <= container.internalWidth && item.y + height <= container.internalHeight
                        && CanPlace(RaidContext.Loadout.items, item, parent.instanceId, item.x, item.y, width, height);
                }
            }
            if (!valid)
            {
                item.rotated = !item.rotated;
                message = "Недостаточно места для поворота";
                return;
            }
            message = "Предмет повёрнут";
        }

        private void DrawItemState(Rect rect, ItemInstance item)
        {
            string state = "";
            if (IsMagazine(item)) state = $"{item.loadedAmmoCount}/{AmmunitionService.MagazineCapacity(item)}";
            else if (IsWeapon(item))
            {
                ItemInstance magazine = InstalledMagazine(item);
                state = magazine == null ? "БЕЗ МАГ." : $"{magazine.loadedAmmoCount}/{AmmunitionService.MagazineCapacity(magazine)}";
            }
            else if (IsAmmo(item)) state = $"×{item.quantity}";
            if (!string.IsNullOrEmpty(state))
                GUI.Label(new Rect(rect.x + 3, rect.yMax - 21, rect.width - 6, 18), state,
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold });
        }

        private static bool IsWeapon(ItemInstance item) => item != null && ItemCatalog.Get(item.definitionId).category == ItemCategory.Weapon;
        private static bool IsMagazine(ItemInstance item) => item != null && AmmunitionService.IsMagazine(ItemCatalog.Get(item.definitionId));
        private static bool IsAmmo(ItemInstance item) => item != null && ItemCatalog.Get(item.definitionId).category == ItemCategory.Ammo;
        private static bool IsMedical(ItemInstance item) => item != null && ItemCatalog.Get(item.definitionId).category == ItemCategory.Medical;

        private void DrawContextMenu()
        {
            if (contextItem == null || !IsAccessibleItem(contextItem)) return;
            bool weapon = IsWeapon(contextItem), magazine = IsMagazine(contextItem), medical = IsMedical(contextItem);
            if (!weapon && !magazine && !medical) { contextItem = null; return; }
            Rect menu = new(contextPosition.x, contextPosition.y, 224, weapon ? 92 : medical ? 98 : 136);
            GUI.Box(menu, GUIContent.none);
            GUI.Label(new Rect(menu.x + 8, menu.y + 5, menu.width - 16, 20), ItemCatalog.Get(contextItem.definitionId).name);
            if (GUI.Button(new Rect(menu.x + 5, menu.y + 29, menu.width - 10, 30), weapon ? "РАЗРЯДИТЬ ОРУЖИЕ" : medical ? "ИСПОЛЬЗОВАТЬ" : "ЗАРЯДИТЬ"))
            {
                if (weapon) UnloadWeaponMagazine(contextItem);
                else if (medical) { healingItem = contextItem; selectingBodyPart = true; }
                else BeginLoadMagazine(contextItem);
                contextItem = null;
            }
            if (weapon)
            {
                if (GUI.Button(new Rect(menu.x + 5, menu.y + 61, menu.width - 10, 26), "ПОДРОБНЕЕ / РАЗБОРКА")) { detailedWeapon = contextItem; contextItem = null; }
            }
            else if (GUI.Button(new Rect(menu.x + 5, menu.y + 61, menu.width - 10, 30), "РАЗРЯДИТЬ"))
            {
                BeginUnloadMagazine(contextItem); contextItem = null;
            }
            if (!weapon && !medical && GUI.Button(new Rect(menu.x + 5, menu.y + 93, menu.width - 10, 30), "ОТМЕНА")) contextItem = null;
            Event e = Event.current;
            if (e.type == EventType.MouseDown && !menu.Contains(e.mousePosition)) { contextItem = null; e.Use(); }
        }

        private void DrawWeaponDetails()
        {
            if (detailedWeapon == null || !IsAccessibleItem(detailedWeapon)) { detailedWeapon = null; return; }
            Rect modal = new(430, 125, 740, 650);
            GUI.Box(modal, GUIContent.none);
            ItemSO weapon = ItemCatalog.Get(detailedWeapon.definitionId);
            GUI.Label(new Rect(modal.x + 20, modal.y + 15, 560, 30), "ПОЛЕВАЯ РАЗБОРКА · " + weapon.name);
            if (GUI.Button(new Rect(modal.xMax - 45, modal.y + 10, 34, 30), "×")) { detailedWeapon = null; return; }
            DrawItemIcon(new Rect(modal.x + 190, modal.y + 58, 360, 180), weapon);
            GUI.Label(new Rect(modal.x + 20, modal.y + 250, 300, 24), "УСТАНОВЛЕННЫЕ МОДУЛИ");
            detailedWeapon.attachmentIds ??= new List<string>();
            int row = 0;
            foreach (string mount in detailedWeapon.attachmentIds.ToArray())
            {
                if (mount == "magazine") continue;
                ItemSO module = ItemCatalog.Get("mod_" + mount);
                Rect line = new(modal.x + 20, modal.y + 282 + row * 42, 330, 36);
                GUI.Box(line, GUIContent.none); GUI.Label(new Rect(line.x + 8, line.y + 8, 200, 20), module.name);
                if (GUI.Button(new Rect(line.xMax - 105, line.y + 4, 100, 28), "СНЯТЬ")) { DetachModule(mount); return; }
                row++;
            }
            GUI.Label(new Rect(modal.x + 380, modal.y + 250, 300, 24), "ДОСТУПНО В СНАРЯЖЕНИИ");
            int availableRow = 0;
            foreach (ItemInstance item in RaidContext.Loadout.items.ToArray())
            {
                ItemSO definition = ItemCatalog.Get(item.definitionId);
                if (definition.category != ItemCategory.Modification || AmmunitionService.IsMagazine(definition)) continue;
                if (definition.modification.compatibleWeaponIds != null && definition.modification.compatibleWeaponIds.Count > 0 && !definition.modification.compatibleWeaponIds.Contains(detailedWeapon.definitionId)) continue;
                string mount = ModificationMount(definition.modification.slot);
                if (string.IsNullOrEmpty(mount) || detailedWeapon.attachmentIds.Contains(mount)) continue;
                Rect line = new(modal.x + 380, modal.y + 282 + availableRow * 42, 330, 36);
                GUI.Box(line, GUIContent.none); GUI.Label(new Rect(line.x + 8, line.y + 8, 200, 20), definition.name);
                if (GUI.Button(new Rect(line.xMax - 115, line.y + 4, 110, 28), "УСТАНОВИТЬ")) { AttachModule(item, mount); return; }
                availableRow++;
                if (availableRow >= 7) break;
            }
        }

        private static string ModificationMount(ModificationSlot slot) => slot switch
        {
            ModificationSlot.Muzzle => "muzzle", ModificationSlot.Optic => "optic", ModificationSlot.Stock => "stock",
            ModificationSlot.Tactical => "laser", ModificationSlot.Grip => "grip", _ => ""
        };

        private void DetachModule(string mount)
        {
            ItemInstance module = ItemInstance.Create("mod_" + mount);
            if (!TryPlaceInLoadout(module)) { message = "Нет места для снятой модификации"; return; }
            detailedWeapon.attachmentIds.Remove(mount); message = "Модификация снята";
        }

        private void AttachModule(ItemInstance module, string mount)
        {
            detailedWeapon.attachmentIds.Add(mount); RaidContext.Loadout.items.Remove(module); message = "Модификация установлена";
        }

        private void UnloadWeaponMagazine(ItemInstance weapon)
        {
            ItemInstance magazine = InstalledMagazine(weapon);
            if (magazine == null) { message = "В оружии нет магазина"; return; }
            string oldParent = magazine.parentContainerId; string oldSlot = magazine.equippedSlot; int oldX = magazine.x, oldY = magazine.y;
            magazine.parentContainerId = null; magazine.equippedSlot = null;
            if (!TryPlaceInLoadout(magazine))
            {
                magazine.parentContainerId = oldParent; magazine.equippedSlot = oldSlot; magazine.x = oldX; magazine.y = oldY;
                message = "Нет свободного места для магазина"; return;
            }
            openedContainer?.items.Remove(magazine);
            weapon.installedMagazineInstanceId = ""; message = "Магазин извлечён из оружия";
        }

        private ItemInstance InstalledMagazine(ItemInstance weapon)
        {
            if (weapon == null || RaidContext.Loadout == null) return null;
            ItemInstance magazine = RaidContext.Loadout.items.Find(item => item.instanceId == weapon.installedMagazineInstanceId && AmmunitionService.IsCompatibleMagazine(weapon, item));
            if (magazine == null) magazine = RaidContext.Loadout.items.Find(item => item.parentContainerId == weapon.instanceId && AmmunitionService.IsCompatibleMagazine(weapon, item));
            if (magazine == null && openedContainer != null) magazine = openedContainer.items.Find(item => item.instanceId == weapon.installedMagazineInstanceId && AmmunitionService.IsCompatibleMagazine(weapon, item));
            if (magazine == null && openedContainer != null) magazine = openedContainer.items.Find(item => item.parentContainerId == weapon.instanceId && AmmunitionService.IsCompatibleMagazine(weapon, item));
            if (magazine != null) weapon.installedMagazineInstanceId = magazine.instanceId;
            return magazine;
        }

        private void BeginLoadMagazine(ItemInstance magazine)
        {
            ItemSO mag = ItemCatalog.Get(magazine.definitionId);
            ItemInstance ammo = RaidContext.Loadout.items.Find(item => IsAmmo(item) && ItemCatalog.Get(item.definitionId).ammunition.caliber == mag.modification.magazineCaliber && item.quantity > 0 && (string.IsNullOrEmpty(magazine.loadedAmmoDefinitionId) || magazine.loadedAmmoDefinitionId == item.definitionId));
            if (ammo == null && openedContainer != null)
                ammo = openedContainer.items.Find(item => IsAmmo(item) && ItemCatalog.Get(item.definitionId).ammunition.caliber == mag.modification.magazineCaliber && item.quantity > 0 && (string.IsNullOrEmpty(magazine.loadedAmmoDefinitionId) || magazine.loadedAmmoDefinitionId == item.definitionId));
            BeginLoadMagazine(magazine, ammo);
        }

        private void BeginLoadMagazine(ItemInstance magazine, ItemInstance ammo)
        {
            if (magazine == null || ammo == null) { message = "Нет подходящих патронов"; return; }
            ItemSO magazineDefinition = ItemCatalog.Get(magazine.definitionId);
            ItemSO ammoDefinition = ItemCatalog.Get(ammo.definitionId);
            if (!AmmunitionService.IsMagazine(magazineDefinition) || ammoDefinition.category != ItemCategory.Ammo || magazineDefinition.modification.magazineCaliber != ammoDefinition.ammunition.caliber)
            { message = "Патроны не подходят к этому магазину"; return; }
            int count = Mathf.Min(ammo.quantity, AmmunitionService.MagazineCapacity(magazine) - magazine.loadedAmmoCount);
            if (count <= 0) { message = "Магазин уже полон"; return; }
            actionMagazine = magazine; actionAmmo = ammo; actionAmmoFromContainer = openedContainer != null && openedContainer.items.Contains(ammo); unloadAmmoTarget = null; actionUnloading = false; actionTotal = count; actionCompleted = 0; actionRoundTimer = 0f;
            message = "ЗАРЯЖАНИЕ МАГАЗИНА";
        }

        private void BeginUnloadMagazine(ItemInstance magazine)
        {
            if (magazine.loadedAmmoCount <= 0 || string.IsNullOrEmpty(magazine.loadedAmmoDefinitionId)) { message = "Магазин пуст"; return; }
            ItemInstance target = RaidContext.Loadout.items.Find(item => item.definitionId == magazine.loadedAmmoDefinitionId && item != magazine && IsAmmo(item) && item.quantity < ItemCatalog.Get(item.definitionId).maxStack);
            if (target == null)
            {
                target = ItemInstance.Create(magazine.loadedAmmoDefinitionId, quantity: 0);
                target.quantity = 0;
                if (!TryPlaceInLoadout(target)) { message = "Нет места для извлечённых патронов"; return; }
            }
            int free = Mathf.Max(0, ItemCatalog.Get(target.definitionId).maxStack - target.quantity);
            actionMagazine = magazine; actionAmmo = null; unloadAmmoTarget = target; actionUnloading = true; actionTotal = Mathf.Min(magazine.loadedAmmoCount, free); actionCompleted = 0; actionRoundTimer = 0f;
            message = "РАЗРЯЖАНИЕ МАГАЗИНА";
        }

        private void AdvanceRoundAction()
        {
            if (actionMagazine == null || actionCompleted >= actionTotal) return;
            actionRoundTimer += Time.unscaledDeltaTime;
            while (actionRoundTimer >= RoundActionTime && actionCompleted < actionTotal)
            {
                actionRoundTimer -= RoundActionTime;
                if (actionUnloading)
                {
                    if (actionMagazine.loadedAmmoCount <= 0) break;
                    unloadAmmoTarget.quantity++; actionMagazine.loadedAmmoCount--; actionCompleted++;
                    if (actionMagazine.loadedAmmoCount == 0) actionMagazine.loadedAmmoDefinitionId = "";
                }
                else
                {
                    if (!AmmunitionService.TryLoadMagazine(actionMagazine, actionAmmo, 1, out int loaded) || loaded == 0) break;
                    actionCompleted++;
                    if (actionAmmo.quantity <= 0)
                    {
                        if (actionAmmoFromContainer) openedContainer?.items.Remove(actionAmmo);
                        else RaidContext.Loadout.items.Remove(actionAmmo);
                    }
                }
            }
            if (actionCompleted >= actionTotal || (!actionUnloading && (actionAmmo == null || actionAmmo.quantity <= 0)))
            {
                message = actionUnloading ? "Магазин разряжен" : "Магазин заряжен";
                actionMagazine = actionAmmo = unloadAmmoTarget = null;
                actionAmmoFromContainer = false;
            }
        }

        private void CancelRoundAction()
        {
            actionMagazine = actionAmmo = unloadAmmoTarget = null;
            actionAmmoFromContainer = false; actionTotal = actionCompleted = 0; actionRoundTimer = 0f;
        }

        private void DrawRoundProgress()
        {
            if (actionMagazine == null || actionTotal <= 0) return;
            float overall = Mathf.Clamp01((actionCompleted + actionRoundTimer / RoundActionTime) / actionTotal);
            // Компактный индикатор всегда находится снизу по центру и одинаково
            // используется как при зарядке, так и при разрядке магазина.
            Vector2 center = new(W * .5f, H - 42f);
            const int segments = 24;
            Color old = GUI.color; GUI.color = new Color(1f, .62f, .12f, .95f);
            int filled = Mathf.CeilToInt(segments * overall);
            for (int i = 0; i < filled; i++)
            {
                // Не вращаем GUI.matrix: при масштабировании интерфейса это
                // растягивало дугу на сотни пикселей. Все точки лежат внутри 50x50.
                float angle = -Mathf.PI * .5f + i * Mathf.PI * 2f / segments;
                float x = center.x + Mathf.Cos(angle) * 21f;
                float y = center.y + Mathf.Sin(angle) * 21f;
                GUI.DrawTexture(new Rect(x - 2f, y - 2f, 4f, 4f), Texture2D.whiteTexture);
            }
            GUI.color = old;
            GUI.Label(new Rect(center.x - 25, center.y - 9, 50, 18), $"{actionCompleted}/{actionTotal}", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10
            });
        }
        private static Texture2D ExtractBody(Texture2D source)
        {
            Color32[] pixels = source.GetPixels32();
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            var output = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i]; float minimum = Mathf.Min(p.r, Mathf.Min(p.g, p.b)) / 255f;
                float maximum = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) / 255f;
                output[i] = new Color32(p.r, p.g, p.b, minimum > .88f && maximum - minimum < .09f ? (byte)0 : (byte)255);
            }
            result.SetPixels32(output); result.Apply(false, false); return result;
        }
        private static void DrawGrid(Vector2 origin, int columns, int rows, float cell) { for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++) GUI.Box(new Rect(origin.x + x * cell, origin.y + y * cell, cell, cell), GUIContent.none); }
        private void AddDropZone(DropZone zone)
        {
            zone.rect.position += dropZoneOffset;
            zone.origin += dropZoneOffset;
            dropZones.Add(zone);
        }
        private void Hover(Rect rect, ItemInstance item, bool fromContainer)
        {
            Rect hitRect = rect;
            hitRect.position += dropZoneOffset;
            itemHits.Add(new ItemHit { rect = hitRect, item = item, fromContainer = fromContainer });
            if (item == null || !rect.Contains(Event.current.mousePosition)) return;
            hoveredItem = item; hoveredFromContainer = fromContainer;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && contextItem == null && detailedWeapon == null)
            {
                if (item.permanent) { message = "Постоянный нож закреплён за оператором"; Event.current.Use(); return; }
                draggedItem = item;
                draggedFromContainer = fromContainer;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && detailedWeapon == null && (IsWeapon(item) || IsMagazine(item) || IsMedical(item)))
            {
                contextItem = item;
                contextPosition = Event.current.mousePosition + dropZoneOffset;
                Event.current.Use();
            }
        }

        private void ProcessDrag()
        {
            if (draggedItem == null) return;
            Event current = Event.current;
            if (current.type == EventType.Repaint || current.type == EventType.MouseDrag)
            {
                ItemCatalog.GetSize(draggedItem, out int width, out int height);
                float previewCell = InventoryLayout.CellSize;
                Rect preview = new(current.mousePosition.x - width * previewCell * .5f, current.mousePosition.y - height * previewCell * .5f, width * previewCell, height * previewCell);
                Color old = GUI.color; GUI.color = new Color(1f, 1f, 1f, .82f);
                GUI.Box(preview, GUIContent.none); DrawItemIcon(preview, ItemCatalog.Get(draggedItem.definitionId), draggedItem); GUI.color = old;
            }
            if (current.type != EventType.MouseUp || current.button != 0) return;
            bool moved = false;
            if (IsAmmo(draggedItem))
            {
                for (int i = itemHits.Count - 1; i >= 0; i--)
                {
                    ItemHit hit = itemHits[i];
                    if (hit.item == draggedItem || !hit.rect.Contains(current.mousePosition)) continue;
                    ItemInstance targetMagazine = IsMagazine(hit.item) ? hit.item : IsWeapon(hit.item) ? InstalledMagazine(hit.item) : null;
                    if (targetMagazine != null)
                    {
                        BeginLoadMagazine(targetMagazine, draggedItem);
                        moved = true;
                    }
                    else if (IsWeapon(hit.item) && targetMagazine == null) message = "Сначала установите магазин в оружие";
                    break;
                }
            }
            for (int i = dropZones.Count - 1; !moved && i >= 0; i--)
            {
                DropZone zone = dropZones[i];
                if (!zone.rect.Contains(current.mousePosition)) continue;
                moved = TryDrop(zone, current.mousePosition);
                break;
            }
            if (!moved) message = "Нельзя разместить предмет здесь";
            else if (actionMagazine == null) message = "Предмет перемещён";
            draggedItem = null;
            current.Use();
        }

        private bool TryDrop(DropZone zone, Vector2 mouse)
        {
            RaidLoadout loadout = RaidContext.Loadout;
            if (loadout == null || draggedItem == null) return false;
            if (draggedItem.permanent) return false;
            ItemCatalog.GetSize(draggedItem, out int width, out int height);

            if (zone.kind == ZoneKind.Equipment)
            {
                if (!CanEquip(draggedItem, zone.slot)) return false;
                ItemInstance occupied = loadout.items.Find(item => item != draggedItem && item.equippedSlot == zone.slot);
                if (occupied != null && occupied.permanent) { message = "Постоянное оружие нельзя заменить"; return false; }
                if (occupied != null)
                {
                    occupied.equippedSlot = null; occupied.parentContainerId = null;
                    if (!TryPlaceInLoadout(occupied))
                    {
                        occupied.equippedSlot = zone.slot;
                        message = "Нет места для снятого снаряжения";
                        return false;
                    }
                }
                MoveDraggedToLoadout();
                draggedItem.parentContainerId = null; draggedItem.equippedSlot = zone.slot; draggedItem.x = draggedItem.y = 0; draggedItem.folded = false;
                if (!loadout.items.Contains(draggedItem)) loadout.items.Add(draggedItem);
                return true;
            }

            if (zone.kind == ZoneKind.Pocket)
            {
                if (width != 1 || height != 1) return false;
                ItemInstance occupied = loadout.items.Find(item => item != draggedItem && item.equippedSlot == zone.slot);
                if (occupied != null) return false;
                MoveDraggedToLoadout();
                draggedItem.parentContainerId = null; draggedItem.equippedSlot = zone.slot; draggedItem.x = draggedItem.y = 0;
                return true;
            }

            int x = Mathf.FloorToInt((mouse.x - zone.origin.x) / zone.cell);
            int y = Mathf.FloorToInt((mouse.y - zone.origin.y) / zone.cell);
            if (x < 0 || y < 0 || x + width > zone.columns || y + height > zone.rows) return false;

            if (zone.kind == ZoneKind.Grid)
            {
                if (!CanPlace(loadout.items, draggedItem, zone.parentId, x, y, width, height)) return false;
                MoveDraggedToLoadout();
                draggedItem.parentContainerId = zone.parentId; draggedItem.equippedSlot = null; draggedItem.x = x; draggedItem.y = y;
                return true;
            }

            if (zone.kind == ZoneKind.Loot && openedContainer != null)
            {
                if (!CanPlace(openedContainer.items, draggedItem, null, x, y, width, height)) return false;
                if (!draggedFromContainer)
                {
                    loadout.items.Remove(draggedItem);
                    if (!openedContainer.items.Contains(draggedItem)) openedContainer.items.Add(draggedItem);
                }
                draggedItem.parentContainerId = null; draggedItem.equippedSlot = null; draggedItem.x = x; draggedItem.y = y;
                return true;
            }
            return false;
        }

        private static bool CanEquip(ItemInstance item, string slot)
        {
            if (item == null || string.IsNullOrEmpty(slot)) return false;
            ItemSO definition = ItemCatalog.Get(item.definitionId);
            if (slot == "main_weapon" || slot == "second_weapon") return definition.category == ItemCategory.Weapon && !item.definitionId.StartsWith("melee_");
            if (slot == "holster") return item.definitionId.StartsWith("pistol_");
            if (slot == "melee") return item.definitionId.StartsWith("melee_");
            if (slot == "armor") return item.definitionId.StartsWith("armor_");
            if (slot == "helmet") return item.definitionId.StartsWith("helmet_");
            if (slot == "headset") return item.definitionId.StartsWith("headset_");
            if (slot == "face_cover") return item.definitionId.StartsWith("face_");
            if (slot == "secure") return item.definitionId.StartsWith("secure_");
            if (slot == "rig") return item.definitionId.StartsWith("rig_");
            if (slot == "backpack") return definition.category == ItemCategory.Backpack;
            return false;
        }

        private void MoveDraggedToLoadout()
        {
            if (!draggedFromContainer) return;
            openedContainer?.items.Remove(draggedItem);
            if (!RaidContext.Loadout.items.Contains(draggedItem)) RaidContext.Loadout.items.Add(draggedItem);
            draggedFromContainer = false;
        }

        private void DrawBodyPartSelector()
        {
            if (!selectingBodyPart || healingItem == null) return;
            PlayerVitals vitals = RaidContext.Loadout?.vitals;
            if (vitals == null) { selectingBodyPart = false; return; }
            Rect modal = new(610, 220, 380, 430); GUI.Box(modal, GUIContent.none);
            GUI.Label(new Rect(modal.x + 20, modal.y + 16, 340, 24), "ВЫБЕРИТЕ ЧАСТЬ ТЕЛА");
            string[] ids = { "head", "chest", "abdomen", "rightArm", "leftArm", "rightLeg", "leftLeg" };
            string[] labels = { "ГОЛОВА", "ГРУДЬ", "ЖИВОТ", "ПРАВАЯ РУКА", "ЛЕВАЯ РУКА", "ПРАВАЯ НОГА", "ЛЕВАЯ НОГА" };
            int[] values = { vitals.head, vitals.chest, vitals.abdomen, vitals.rightArm, vitals.leftArm, vitals.rightLeg, vitals.leftLeg };
            int[] maximums = { 35, 85, 70, 60, 60, 65, 65 };
            for (int i = 0; i < ids.Length; i++)
                if (GUI.Button(new Rect(modal.x + 20, modal.y + 52 + i * 45, 340, 36), $"{labels[i]}   {values[i]}/{maximums[i]}")) BeginHealing(ids[i]);
            if (GUI.Button(new Rect(modal.x + 20, modal.y + 375, 340, 34), "ОТМЕНА")) { selectingBodyPart = false; healingItem = null; }
        }

        private void BeginHealing(string bodyPart)
        {
            ItemSO definition = ItemCatalog.Get(healingItem.definitionId);
            PlayerVitals vitals = RaidContext.Loadout?.vitals;
            bool destroyed = vitals?.destroyedParts != null && vitals.destroyedParts.Contains(bodyPart);
            if (destroyed && healingItem.definitionId != "surgical_kit")
            {
                message = "УНИЧТОЖЕННАЯ ЧАСТЬ ЛЕЧИТСЯ ТОЛЬКО ХИРУРГИЧЕСКИМ НАБОРОМ";
                selectingBodyPart = false; healingItem = null; return;
            }
            healingTarget = bodyPart; healingDuration = Mathf.Max(.5f, definition.medicine.useTime);
            healingStartedAt = Time.unscaledTime; selectingBodyPart = false;
            message = $"ЛЕЧЕНИЕ · {definition.name}";
        }

        private void AdvanceHealing()
        {
            if (healingItem == null || selectingBodyPart || string.IsNullOrEmpty(healingTarget)) return;
            if (!IsAccessibleItem(healingItem)) { CancelHealing(); return; }
            if (Time.unscaledTime - healingStartedAt < healingDuration) return;
            MedicalData medicine = ItemCatalog.Get(healingItem.definitionId).medicine;
            ApplyHealing(RaidContext.Loadout.vitals, healingTarget, medicine.healingAmount);
            if (healingItem.definitionId == "surgical_kit") RaidContext.Loadout.vitals.destroyedParts?.Remove(healingTarget);
            if (medicine.treatsBleeding) RaidContext.Loadout.vitals.bleedingParts?.Remove(healingTarget);
            if (medicine.treatsFracture) RaidContext.Loadout.vitals.fracturedParts?.Remove(healingTarget);
            healingItem.quantity--;
            if (healingItem.quantity <= 0)
            {
                RaidContext.Loadout.items.Remove(healingItem);
                openedContainer?.items.Remove(healingItem);
            }
            message = "ЛЕЧЕНИЕ ЗАВЕРШЕНО"; CancelHealing();
        }

        private static void ApplyHealing(PlayerVitals v, string part, int amount)
        {
            switch (part)
            {
                case "head": v.head = Mathf.Min(35, v.head + amount); break;
                case "chest": v.chest = Mathf.Min(85, v.chest + amount); break;
                case "abdomen": v.abdomen = Mathf.Min(70, v.abdomen + amount); break;
                case "rightArm": v.rightArm = Mathf.Min(60, v.rightArm + amount); break;
                case "leftArm": v.leftArm = Mathf.Min(60, v.leftArm + amount); break;
                case "rightLeg": v.rightLeg = Mathf.Min(65, v.rightLeg + amount); break;
                case "leftLeg": v.leftLeg = Mathf.Min(65, v.leftLeg + amount); break;
            }
        }

        private void DrawHealingProgress()
        {
            if (healingItem == null || selectingBodyPart || string.IsNullOrEmpty(healingTarget)) return;
            float progress = Mathf.Clamp01((Time.unscaledTime - healingStartedAt) / healingDuration);
            Rect bar = new(W * .5f - 170, H - 74, 340, 18); GUI.Box(bar, GUIContent.none);
            Color old = GUI.color; GUI.color = new Color(.25f, .78f, .42f); GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * progress, bar.height - 4), Texture2D.whiteTexture); GUI.color = old;
            GUI.Label(new Rect(bar.x, bar.y - 27, bar.width, 24), $"ЛЕЧЕНИЕ {Mathf.CeilToInt(healingDuration - (Time.unscaledTime - healingStartedAt))} С", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }

        private void CancelHealing()
        {
            healingItem = null; healingTarget = null; healingStartedAt = healingDuration = 0f; selectingBodyPart = false;
        }
        private bool IsAccessibleItem(ItemInstance item) => item != null &&
            ((RaidContext.Loadout?.items.Contains(item) ?? false) || (openedContainer?.items.Contains(item) ?? false));
        private static string SlotName(string slot) => slot.StartsWith("pocket_") ? "КАРМАН" : slot.ToUpperInvariant();
    }
}
