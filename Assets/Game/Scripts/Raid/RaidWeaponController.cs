using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OfflineExtraction.Raid
{
    [RequireComponent(typeof(RaidPlayerController))]
    public sealed class RaidWeaponController : MonoBehaviour
    {
        private RaidPlayerController movement;
        private Camera viewCamera;
        private ItemInstance weapon;
        private ItemSO definition;
        private GameObject viewModel;
        private float nextShotTime;
        private float reloadFinishesAt;
        private int shotsSinceWear;
        private string activeSlot = "main_weapon";
        private string status = "";
        private ItemInstance reloadMagazine;
        private FireMode currentFireMode = FireMode.Single;
        private string reloadSourceParent;
        private string reloadSourceSlot;
        private int reloadSourceX, reloadSourceY;
        private float aimSwayTime;
        private float meleeSwingUntil;

        private bool IsReloading => reloadFinishesAt > 0f;
        private bool IsMelee => weapon != null && weapon.definitionId.StartsWith("melee_");

        private void Awake()
        {
            movement = GetComponent<RaidPlayerController>();
            viewCamera = GetComponentInChildren<Camera>();
            SelectWeapon("main_weapon");
            if (weapon == null) SelectWeapon("second_weapon");
            if (weapon == null) SelectWeapon("holster");
            if (weapon == null) SelectWeapon("melee");
            CreateViewModel();
        }

        private void OnDestroy()
        {
            if (movement != null) movement.IsAiming = false;
        }

        private void Update()
        {
            if (RaidBootstrap.IsPaused || RaidInventoryUI.IsOpen || RaidBootstrap.IsDeploymentLocked)
            {
                movement.IsAiming = false;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SelectWeapon("main_weapon");
            if (keyboard.digit2Key.wasPressedThisFrame) SelectWeapon("second_weapon");
            if (keyboard.digit3Key.wasPressedThisFrame) SelectWeapon("holster");
            if (keyboard.digit4Key.wasPressedThisFrame) SelectWeapon("melee");
            if (keyboard.bKey.wasPressedThisFrame) ToggleFireMode();

            movement.IsAiming = weapon != null && !IsMelee && mouse.rightButton.isPressed && !IsReloading;
            UpdateViewModel();

            if (IsReloading)
            {
                if (Time.time >= reloadFinishesAt) FinishReload();
                return;
            }

            if (weapon == null) return;
            if (keyboard.rKey.wasPressedThisFrame) { StartReload(); return; }

            bool automatic = currentFireMode == FireMode.Automatic;
            bool wantsToFire = automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (wantsToFire && Time.time >= nextShotTime) Fire();
        }

        private void SelectWeapon(string slot)
        {
            if (IsReloading) return;
            ItemInstance selected = RaidContext.Loadout?.items.Find(item => item.equippedSlot == slot);
            if (selected != null && ItemCatalog.Get(selected.definitionId).category != ItemCategory.Weapon) selected = null;
            activeSlot = slot;
            weapon = selected;
            definition = weapon == null ? null : ItemCatalog.Get(weapon.definitionId);
            status = weapon == null ? "" : definition.name;
            if (viewModel != null) viewModel.SetActive(weapon != null);
            ConfigureViewModel();
            if (definition != null && !definition.weapon.fireModes.Contains(currentFireMode))
                currentFireMode = definition.weapon.fireModes.Contains(FireMode.Single) ? FireMode.Single : FireMode.Automatic;
        }

        private void ToggleFireMode()
        {
            if (definition == null || !definition.weapon.fireModes.Contains(FireMode.Automatic) || !definition.weapon.fireModes.Contains(FireMode.Single)) return;
            currentFireMode = currentFireMode == FireMode.Single ? FireMode.Automatic : FireMode.Single;
            status = currentFireMode == FireMode.Automatic ? "АВТОМАТИЧЕСКИЙ ОГОНЬ" : "ОДИНОЧНЫЙ ОГОНЬ";
        }

        private void Fire()
        {
            int rate = Mathf.Max(60, definition.weapon.rateOfFire);
            nextShotTime = Time.time + 60f / rate;
            if (weapon.condition <= 0) { status = "ОРУЖИЕ СЛОМАНО"; return; }
            if (IsMelee) { FireMelee(); return; }
            ItemInstance magazine = CurrentMagazine();
            if (magazine == null) { status = "НЕТ МАГАЗИНА · R — ВСТАВИТЬ"; return; }
            if (magazine.loadedAmmoCount <= 0) { status = "МАГАЗИН ПУСТ · R — СМЕНИТЬ"; return; }

            magazine.loadedAmmoCount--;
            RaidBotController.ReportGunshot(transform.position, 55f);
            shotsSinceWear++;
            if (shotsSinceWear >= 8) { shotsSinceWear = 0; weapon.condition = Mathf.Max(0, weapon.condition - 1); }

            float control = Mathf.Clamp01(definition.weapon.recoilControl / 100f);
            float aimMultiplier = movement.IsAiming ? (movement.IsHoldingBreath ? .48f : .62f) : 1f;
            float verticalRecoil = Mathf.Lerp(2.4f, .65f, control) * aimMultiplier;
            float horizontalRecoil = Random.Range(-.55f, .55f) * (1f - control * .55f) * aimMultiplier;
            movement.AddRecoil(verticalRecoil, horizontalRecoil);

            float spread = movement.IsAiming ? (movement.IsHoldingBreath ? .0008f : .0018f) : .008f;
            Vector3 direction = viewCamera.transform.forward
                + viewCamera.transform.right * Random.Range(-spread, spread)
                + viewCamera.transform.up * Random.Range(-spread, spread);
            if (Physics.Raycast(viewCamera.transform.position, direction.normalized, out RaycastHit hit, 180f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                RaidBotController bot = hit.collider.GetComponentInParent<RaidBotController>();
                if (bot != null) bot.ApplyHitAt(hit.point, definition.weapon.damage);
                else if (hit.collider.GetComponentInParent<RaidHealthController>() == null)
                    hit.collider.SendMessageUpwards("ApplyDamage", definition.weapon.damage, SendMessageOptions.DontRequireReceiver);
                CreateImpact(hit.point, hit.normal);
            }
            status = "";
        }

        private void StartReload()
        {
            if (IsMelee) return;
            reloadMagazine = FindReplacementMagazine();
            if (reloadMagazine == null) { status = "НЕТ ПОДХОДЯЩЕГО МАГАЗИНА"; return; }
            reloadSourceParent = reloadMagazine.parentContainerId;
            reloadSourceSlot = reloadMagazine.equippedSlot;
            reloadSourceX = reloadMagazine.x; reloadSourceY = reloadMagazine.y;
            reloadFinishesAt = Time.time + Mathf.Lerp(2.5f, 1.45f, definition.weapon.ergonomics / 100f);
            status = "СМЕНА МАГАЗИНА...";
        }

        private void FinishReload()
        {
            reloadFinishesAt = 0f;
            if (reloadMagazine == null || !RaidContext.Loadout.items.Contains(reloadMagazine) || !AmmunitionService.IsCompatibleMagazine(weapon, reloadMagazine))
            { status = "ПЕРЕЗАРЯДКА ПРЕРВАНА"; reloadMagazine = null; return; }

            ItemInstance oldMagazine = CurrentMagazine();
            if (oldMagazine != null)
            {
                oldMagazine.parentContainerId = reloadSourceParent;
                oldMagazine.equippedSlot = reloadSourceSlot;
                oldMagazine.x = reloadSourceX; oldMagazine.y = reloadSourceY;
            }
            reloadMagazine.parentContainerId = weapon.instanceId;
            reloadMagazine.equippedSlot = null;
            reloadMagazine.x = reloadMagazine.y = 0;
            weapon.installedMagazineInstanceId = reloadMagazine.instanceId;
            status = $"МАГАЗИН: {reloadMagazine.loadedAmmoCount}/{AmmunitionService.MagazineCapacity(reloadMagazine)}";
            reloadMagazine = null;
        }

        private ItemInstance CurrentMagazine()
        {
            if (weapon == null || RaidContext.Loadout == null) return null;
            ItemInstance installed = RaidContext.Loadout.items.Find(item => item.instanceId == weapon.installedMagazineInstanceId);
            if (installed != null && AmmunitionService.IsCompatibleMagazine(weapon, installed)) return installed;
            installed = RaidContext.Loadout.items.Find(item => item.parentContainerId == weapon.instanceId && AmmunitionService.IsCompatibleMagazine(weapon, item));
            if (installed != null) weapon.installedMagazineInstanceId = installed.instanceId;
            return installed;
        }

        private ItemInstance FindReplacementMagazine()
        {
            ItemInstance current = CurrentMagazine();
            ItemInstance best = null;
            foreach (ItemInstance item in RaidContext.Loadout.items)
            {
                if (item == current || item.parentContainerId == weapon.instanceId || !AmmunitionService.IsCompatibleMagazine(weapon, item)) continue;
                if (best == null || item.loadedAmmoCount > best.loadedAmmoCount) best = item;
            }
            return best;
        }

        private void CreateViewModel()
        {
            if (viewCamera == null) return;
            viewModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            viewModel.name = "Weapon View Model";
            viewModel.transform.SetParent(viewCamera.transform, false);
            viewModel.transform.localScale = new Vector3(.13f, .11f, .72f);
            Collider collider = viewModel.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = viewModel.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(.10f, .12f, .115f);
            viewModel.SetActive(weapon != null);
            ConfigureViewModel();
            UpdateViewModel();
        }

        private void ConfigureViewModel()
        {
            if (viewModel == null) return;
            viewModel.transform.localScale = IsMelee ? new Vector3(.055f, .025f, .48f) : new Vector3(.13f, .11f, .72f);
            Renderer renderer = viewModel.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = IsMelee ? new Color(.28f, .31f, .30f) : new Color(.10f, .12f, .115f);
        }

        private void FireMelee()
        {
            meleeSwingUntil = Time.time + .24f;
            Vector3 origin = viewCamera.transform.position;
            if (!Physics.SphereCast(origin, .18f, viewCamera.transform.forward, out RaycastHit hit, 2.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            RaidBotController bot = hit.collider.GetComponentInParent<RaidBotController>();
            if (bot != null) bot.ApplyHitAt(hit.point, definition.weapon.damage);
            else if (hit.collider.GetComponentInParent<RaidHealthController>() == null)
                hit.collider.SendMessageUpwards("ApplyDamage", definition.weapon.damage, SendMessageOptions.DontRequireReceiver);
            CreateImpact(hit.point, hit.normal);
        }

        private void UpdateViewModel()
        {
            if (viewModel == null || weapon == null) return;
            if (IsMelee)
            {
                bool swinging = Time.time < meleeSwingUntil;
                Vector3 knifePosition = swinging ? new Vector3(.05f, -.12f, .68f) : new Vector3(.28f, -.28f, .48f);
                Quaternion knifeRotation = Quaternion.Euler(swinging ? 58f : 18f, swinging ? -22f : 5f, swinging ? -42f : -18f);
                viewModel.transform.localPosition = Vector3.Lerp(viewModel.transform.localPosition, knifePosition, Time.deltaTime * 18f);
                viewModel.transform.localRotation = Quaternion.Lerp(viewModel.transform.localRotation, knifeRotation, Time.deltaTime * 18f);
                return;
            }
            Vector3 hip = new(.24f, -.22f, .58f);
            Vector3 aimed = new(0f, -.105f, .48f);
            Vector3 reload = new(.28f, -.40f, .42f);
            Vector3 target = IsReloading ? reload : movement.IsAiming ? aimed : hip;
            Quaternion targetRotation = Quaternion.Euler(IsReloading ? 24f : 0f, 0f, 0f);
            if (movement.IsAiming && !movement.IsHoldingBreath && !IsReloading)
            {
                aimSwayTime += Time.deltaTime;
                float fatigue = 1f + (1f - movement.BreathStamina / 100f) * .75f;
                target += new Vector3(Mathf.Sin(aimSwayTime * 1.7f) * .0045f, Mathf.Cos(aimSwayTime * 1.25f) * .0035f, 0f) * fatigue;
                targetRotation = Quaternion.Euler(Mathf.Sin(aimSwayTime * 1.2f) * .22f * fatigue, Mathf.Cos(aimSwayTime * 1.55f) * .28f * fatigue, 0f);
            }
            viewModel.transform.localPosition = Vector3.Lerp(viewModel.transform.localPosition, target, Time.deltaTime * 12f);
            viewModel.transform.localRotation = Quaternion.Lerp(viewModel.transform.localRotation, targetRotation, Time.deltaTime * 12f);
        }

        private static void CreateImpact(Vector3 point, Vector3 normal)
        {
            GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impact.name = "Bullet impact";
            impact.transform.position = point + normal * .012f;
            impact.transform.localScale = Vector3.one * .055f;
            Collider collider = impact.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            Renderer renderer = impact.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = new Color(.08f, .06f, .04f);
            Object.Destroy(impact, 1.8f);
        }

        private void OnGUI()
        {
            if (RaidBootstrap.IsPaused || RaidInventoryUI.IsOpen) return;
            GUIStyle hud = new(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
            string weaponName = weapon == null ? "ОРУЖИЕ НЕ ВЫБРАНО" : definition.name;
            ItemInstance magazine = CurrentMagazine();
            string ammunition = weapon == null ? "—" : IsMelee ? "ПОСТОЯННОЕ ОРУЖИЕ" : magazine == null ? "БЕЗ МАГАЗИНА" : $"{magazine.loadedAmmoCount} / {AmmunitionService.MagazineCapacity(magazine)}";
            string condition = weapon == null || IsMelee ? "" : $"СОСТОЯНИЕ {weapon.condition}%";
            GUI.Label(new Rect(Screen.width - 390, Screen.height - 112, 360, 28), weaponName, hud);
            string mode = weapon == null || IsMelee ? "" : currentFireMode == FireMode.Automatic ? "АВТО" : "ОДИН";
            string aim = IsMelee ? "БЛИЖНИЙ БОЙ" : movement.IsAiming ? "ПРИЦЕЛ" : "ОТ БЕДРА";
            GUI.Label(new Rect(Screen.width - 390, Screen.height - 82, 360, 28), $"{ammunition}   {mode} · {aim}   {condition}", hud);
            if (!string.IsNullOrEmpty(status)) GUI.Label(new Rect(Screen.width * .5f - 240, Screen.height - 105, 480, 30), status, new GUIStyle(hud) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
