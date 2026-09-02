using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OfflineExtraction.UI
{
    public sealed class HubWorkbenchUI : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        public Transform weaponDisplay;
        private LobbyPrototype lobby;
        private ItemInstance selectedWeapon;
        private GameObject displayedWeapon;
        private int section;
        private Vector2 scroll;
        private string message = "Выберите действие";

        public void Open()
        {
            lobby = FindFirstObjectByType<LobbyPrototype>();
            IsOpen = true; section = 0; message = "Выберите оружие для ремонта";
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        private void OnDestroy() => IsOpen = false;

        private void Close()
        {
            IsOpen = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            const float width = 760f, height = 560f;
            Rect modal = new((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            GUI.Box(modal, GUIContent.none);
            GUI.Label(new Rect(modal.x + 22, modal.y + 17, 500, 28), "ОРУЖЕЙНЫЙ ВЕРСТАК");
            if (GUI.Button(new Rect(modal.xMax - 48, modal.y + 12, 34, 30), "×")) { Close(); return; }
            if (GUI.Button(new Rect(modal.x + 22, modal.y + 58, 225, 38), "РЕМОНТ ОРУЖИЯ")) section = 0;
            if (GUI.Button(new Rect(modal.x + 257, modal.y + 58, 225, 38), "КРАФТ ПАТРОНОВ")) section = 1;
            GUI.Label(new Rect(modal.x + 22, modal.yMax - 38, modal.width - 44, 24), message);
            if (section == 0) DrawRepair(new Rect(modal.x + 22, modal.y + 112, modal.width - 44, 390));
            else DrawCraft(new Rect(modal.x + 22, modal.y + 112, modal.width - 44, 390));
        }

        private void DrawRepair(Rect rect)
        {
            PlayerData player = lobby?.ShelterPlayer;
            if (player == null) return;
            GUI.Box(new Rect(rect.x, rect.y, 430, rect.height), GUIContent.none);
            GUI.Label(new Rect(rect.x + 12, rect.y + 10, 400, 22), "ОРУЖИЕ В ХРАНИЛИЩЕ");
            var weapons = new List<ItemInstance>();
            foreach (ItemInstance item in player.stash)
                if (ItemCatalog.Get(item.definitionId).category == ItemCategory.Weapon && !item.permanent) weapons.Add(item);
            scroll = GUI.BeginScrollView(new Rect(rect.x + 10, rect.y + 42, 410, rect.height - 54), scroll, new Rect(0, 0, 385, Mathf.Max(330, weapons.Count * 52)));
            for (int i = 0; i < weapons.Count; i++)
            {
                ItemInstance weapon = weapons[i]; ItemSO definition = ItemCatalog.Get(weapon.definitionId);
                if (GUI.Button(new Rect(0, i * 52, 380, 44), $"{definition.name}     СОСТОЯНИЕ {weapon.condition}%")) SelectWeapon(weapon);
            }
            GUI.EndScrollView();

            Rect details = new(rect.x + 444, rect.y, rect.width - 444, rect.height);
            GUI.Box(details, GUIContent.none);
            if (selectedWeapon == null) { GUI.Label(new Rect(details.x + 14, details.y + 18, details.width - 28, 60), "Выберите оружие. Оно появится на столе."); return; }
            ItemSO selected = ItemCatalog.Get(selectedWeapon.definitionId);
            int price = Mathf.Max(0, 100 - selectedWeapon.condition) * 500;
            GUI.Label(new Rect(details.x + 14, details.y + 18, details.width - 28, 50), selected.name);
            GUI.Label(new Rect(details.x + 14, details.y + 76, details.width - 28, 26), $"СОСТОЯНИЕ: {selectedWeapon.condition}%");
            GUI.Label(new Rect(details.x + 14, details.y + 108, details.width - 28, 26), $"ЦЕНА: {price:N0} ₽");
            bool available = BunkerService.CanRepairWeapons(player) && selectedWeapon.condition < 100 && player.money >= price;
            GUI.enabled = available;
            if (GUI.Button(new Rect(details.x + 14, details.y + 155, details.width - 28, 42), "РЕМОНТИРОВАТЬ"))
            {
                player.money -= price; selectedWeapon.condition = 100; lobby.SaveShelterProgress();
                message = "Оружие полностью отремонтировано";
            }
            GUI.enabled = true;
            if (!BunkerService.CanRepairWeapons(player)) GUI.Label(new Rect(details.x + 14, details.y + 216, details.width - 28, 90), "Для ремонта нужен построенный верстак и работающий генератор.");
        }

        private void DrawCraft(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 18, rect.y + 16, rect.width - 36, 26), "СПЕЦИАЛЬНЫЕ БОЕПРИПАСЫ");
            GUI.Label(new Rect(rect.x + 18, rect.y + 58, rect.width - 36, 52), "Здесь будут создаваться редкие патроны с повышенным уроном и бронепробитием, недоступные в магазине.");
            GUI.Box(new Rect(rect.x + 18, rect.y + 132, rect.width - 36, 76), "УСИЛЕННЫЙ 5.56 · высокий урон");
            GUI.Box(new Rect(rect.x + 18, rect.y + 220, rect.width - 36, 76), "БРОНЕБОЙНЫЙ 5.56 · высокое пробитие");
            GUI.Label(new Rect(rect.x + 18, rect.y + 322, rect.width - 36, 38), "Рецепты и отдельные характеристики патронов подключим следующим этапом.");
        }

        private void SelectWeapon(ItemInstance weapon)
        {
            selectedWeapon = weapon; message = "Оружие размещено на верстаке";
            if (displayedWeapon != null) Destroy(displayedWeapon);
            if (weaponDisplay == null) return;
            displayedWeapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            displayedWeapon.name = "Weapon on Workbench · " + ItemCatalog.Get(weapon.definitionId).name;
            displayedWeapon.transform.position = weaponDisplay.position;
            displayedWeapon.transform.rotation = Quaternion.Euler(0, 8f, 0);
            displayedWeapon.transform.localScale = new Vector3(1.35f, .12f, .28f);
            Rigidbody body = displayedWeapon.AddComponent<Rigidbody>();
            body.mass = 2.5f;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            displayedWeapon.GetComponent<Renderer>().material.color = new Color(.12f, .13f, .13f);
        }
    }
}
