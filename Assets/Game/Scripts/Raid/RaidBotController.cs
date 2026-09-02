using System.Collections.Generic;
using OfflineExtraction.Core;
using UnityEngine;

namespace OfflineExtraction.Raid
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RaidBotController : MonoBehaviour
    {
        public float patrolSpeed = 1.7f, combatSpeed = 2.8f, visionDistance = 30f, visionHalfAngle = 58f, memoryDuration = 10f;
        private enum BotState { Patrol, Investigate, Search, Combat, Cover, Reload, Dead }
        private BotState state;
        private CharacterController controller;
        private RaidPlayerController player;
        private RaidHealthController playerHealth;
        private Renderer[] renderers;
        private HumanoidMotionDriver motion;
        private bool importedVisual;
        private Vector3 patrolA, patrolB, lastKnownPosition, searchTarget, coverTarget;
        private bool patrolToB = true, crouched;
        private float memoryUntil, searchUntil, nextShot, nextDecision, reloadUntil, bleedTimer, verticalVelocity;
        private int burstRemaining;
        private readonly Dictionary<string, int> bodyHealth = new()
        {
            { "head", 35 }, { "chest", 85 }, { "abdomen", 70 }, { "rightArm", 60 },
            { "leftArm", 60 }, { "rightLeg", 65 }, { "leftLeg", 65 }
        };
        private readonly HashSet<string> bleedingParts = new();
        private readonly List<ItemInstance> inventory = new();
        private ItemInstance rifle, magazine, spareMagazine, armor, rig;
        private static Vector3 lastGunshotPosition;
        private static float lastGunshotTime = -100f, lastGunshotRadius;

        public static void ReportGunshot(Vector3 position, float radius)
        { lastGunshotPosition = position; lastGunshotRadius = radius; lastGunshotTime = Time.time; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .35f; controller.center = Vector3.up * .9f;
            patrolA = transform.position; patrolB = patrolA + new Vector3(7f, 0f, 3f);
            CreateLoadout(); CreateBody();
            motion = gameObject.AddComponent<HumanoidMotionDriver>();
            motion.targetHeight = 1.78f;
        }

        private void Start()
        { player = FindFirstObjectByType<RaidPlayerController>(); playerHealth = player == null ? null : player.GetComponent<RaidHealthController>(); }

        private void Update()
        {
            if (state == BotState.Dead || RaidBootstrap.IsPaused || RaidInventoryUI.IsOpen || RaidBootstrap.IsDeploymentLocked || player == null) return;
            TickBleeding();
            bool sees = CanSeePlayer();
            bool hearsMovement = player.NoiseRadius > 0f && Distance(transform.position, player.transform.position) <= player.NoiseRadius;
            bool hearsShot = Time.time - lastGunshotTime < 1.2f && Distance(transform.position, lastGunshotPosition) <= lastGunshotRadius;
            if (sees) Remember(player.transform.position, BotState.Combat);
            else if (hearsMovement || hearsShot) Remember(hearsShot ? lastGunshotPosition : player.transform.position, BotState.Investigate);

            if (state == BotState.Reload) { Face(lastKnownPosition); if (Time.time >= reloadUntil) FinishReload(); return; }
            if (!sees && Time.time >= memoryUntil && (state == BotState.Combat || state == BotState.Cover || state == BotState.Investigate)) BeginSearch();
            switch (state)
            {
                case BotState.Combat: Combat(sees); break;
                case BotState.Cover: UseCover(sees); break;
                case BotState.Investigate: Investigate(); break;
                case BotState.Search: Search(); break;
                default: Patrol(); break;
            }
            UpdatePosture();
        }

        private void Remember(Vector3 position, BotState newState)
        { lastKnownPosition = position; memoryUntil = Time.time + memoryDuration; if (state != BotState.Combat || newState == BotState.Combat) state = newState; }

        private void CreateLoadout()
        {
            rifle = ItemInstance.Create("rifle_mk1"); rifle.condition = Random.Range(68, 96); rifle.equippedSlot = "main_weapon";
            magazine = ItemInstance.Create("mod_magazine"); magazine.loadedAmmoDefinitionId = "ammo_556"; magazine.loadedAmmoCount = Random.Range(20, 31); magazine.parentContainerId = rifle.instanceId; rifle.installedMagazineInstanceId = magazine.instanceId;
            spareMagazine = ItemInstance.Create("mod_magazine"); spareMagazine.loadedAmmoDefinitionId = "ammo_556"; spareMagazine.loadedAmmoCount = Random.Range(15, 31);
            armor = ItemInstance.Create("armor_t3"); armor.condition = Random.Range(55, 91); armor.equippedSlot = "armor";
            rig = ItemInstance.Create("rig_16"); rig.equippedSlot = "rig"; spareMagazine.parentContainerId = rig.instanceId;
            inventory.AddRange(new[] { rifle, magazine, spareMagazine, armor, rig, ItemInstance.Create("bandage", quantity: Random.Range(1, 3)) });
        }

        private void Combat(bool sees)
        {
            float distance = Distance(transform.position, player.transform.position);
            if (!sees) { MoveTowards(lastKnownPosition, combatSpeed); return; }
            Face(player.transform.position);
            if (magazine.loadedAmmoCount <= 0) { BeginReload(); return; }
            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + Random.Range(2.2f, 4.2f);
                if (distance < 16f && TryFindCover(out coverTarget)) { state = BotState.Cover; crouched = true; return; }
            }
            if (distance > 13f) MoveTowards(player.transform.position, combatSpeed * Mobility());
            else if (distance < 6f) MoveTowards(transform.position - (player.transform.position - transform.position), combatSpeed * .75f * Mobility());
            if (Time.time >= nextShot) Burst(distance);
            SetColor(new Color(.46f, .12f, .10f));
        }

        private void UseCover(bool sees)
        {
            if (Distance(transform.position, coverTarget) > .65f) { MoveTowards(coverTarget, combatSpeed * Mobility()); return; }
            crouched = true; Face(player.transform.position);
            if (sees && Time.time >= nextShot) Burst(Distance(transform.position, player.transform.position));
            if (Time.time >= nextDecision) { crouched = false; state = BotState.Combat; nextDecision = Time.time + Random.Range(2f, 3.5f); }
            SetColor(new Color(.35f, .18f, .10f));
        }

        private void Investigate()
        { if (Distance(transform.position, lastKnownPosition) > 1.1f) MoveTowards(lastKnownPosition, patrolSpeed * 1.25f * Mobility()); else BeginSearch(); SetColor(new Color(.42f, .27f, .08f)); }

        private void BeginSearch()
        { state = BotState.Search; searchUntil = Time.time + 8f; ChooseSearchTarget(); }
        private void ChooseSearchTarget()
        { searchTarget = lastKnownPosition + new Vector3(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f)); }
        private void Search()
        {
            if (Time.time >= searchUntil) { state = BotState.Patrol; crouched = false; return; }
            if (Distance(transform.position, searchTarget) < .8f) ChooseSearchTarget();
            MoveTowards(searchTarget, patrolSpeed * Mobility()); SetColor(new Color(.30f, .28f, .13f));
        }
        private void Patrol()
        {
            Vector3 target = patrolToB ? patrolB : patrolA;
            if (Distance(transform.position, target) < .8f) patrolToB = !patrolToB;
            MoveTowards(target, patrolSpeed * Mobility()); SetColor(new Color(.20f, .25f, .21f));
        }

        private bool TryFindCover(out Vector3 result)
        {
            Vector3 away = Vector3.ProjectOnPlane(transform.position - player.transform.position, Vector3.up).normalized;
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * away;
                if (!Physics.Raycast(transform.position + Vector3.up, direction, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                Vector3 candidate = hit.point + direction * .65f;
                if (Physics.Linecast(candidate + Vector3.up * 1.1f, player.transform.position + Vector3.up * 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                { result = candidate; return true; }
            }
            result = transform.position; return false;
        }

        private void Burst(float distance)
        {
            if (burstRemaining <= 0) burstRemaining = Random.Range(2, 5);
            Shoot(distance); burstRemaining--;
            nextShot = Time.time + (burstRemaining > 0 ? .11f : Random.Range(.65f, 1.15f));
        }
        private void BeginReload()
        {
            if (spareMagazine == null || spareMagazine.loadedAmmoCount <= 0) { state = BotState.Cover; nextDecision = Time.time + 3f; return; }
            state = BotState.Reload; reloadUntil = Time.time + 2.25f; crouched = true;
        }
        private void FinishReload()
        {
            ItemInstance empty = magazine; magazine = spareMagazine; spareMagazine = empty;
            magazine.parentContainerId = rifle.instanceId; rifle.installedMagazineInstanceId = magazine.instanceId; spareMagazine.parentContainerId = rig.instanceId;
            state = BotState.Combat; crouched = false; nextShot = Time.time + .35f;
        }

        public void ApplyBodyDamage(string part, int amount)
        {
            if (state == BotState.Dead || amount <= 0 || !bodyHealth.ContainsKey(part)) return;
            int actual = amount;
            if ((part == "head" || part == "chest" || part == "abdomen") && armor != null && armor.condition > 0)
            { actual = Mathf.Max(1, Mathf.RoundToInt(actual * .58f)); armor.condition = Mathf.Max(0, armor.condition - Random.Range(1, 5)); }
            bodyHealth[part] = Mathf.Max(0, bodyHealth[part] - actual);
            if (Random.value < .22f) bleedingParts.Add(part);
            Remember(player == null ? transform.position : player.transform.position, BotState.Combat);
            CheckDeath();
        }
        public void ApplyHitAt(Vector3 worldPoint, int amount)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            string part;
            if (local.y > 1.48f) part = "head";
            else if (local.y < .72f) part = local.x < 0f ? "rightLeg" : "leftLeg";
            else if (Mathf.Abs(local.x) > .32f) part = local.x < 0f ? "rightArm" : "leftArm";
            else part = local.y < 1.02f ? "abdomen" : "chest";
            ApplyBodyDamage(part, amount);
        }
        private void TickBleeding()
        {
            if (bleedingParts.Count == 0 || Time.time < bleedTimer) return;
            bleedTimer = Time.time + 2f;
            foreach (string part in new List<string>(bleedingParts)) bodyHealth[part] = Mathf.Max(0, bodyHealth[part] - 1);
            CheckDeath();
        }
        private void CheckDeath() { if (bodyHealth["head"] <= 0 || bodyHealth["chest"] <= 0 || TotalHealth() <= 0) Die(); }
        private int TotalHealth() { int total = 0; foreach (int value in bodyHealth.Values) total += value; return total; }
        private float Mobility() { float legs = Mathf.Min(bodyHealth["leftLeg"] / 65f, bodyHealth["rightLeg"] / 65f); return Mathf.Lerp(.42f, 1f, legs); }

        private bool CanSeePlayer()
        {
            Vector3 eye = transform.position + Vector3.up * (crouched ? 1.12f : 1.55f), target = player.transform.position + Vector3.up * 1.25f;
            Vector3 delta = target - eye;
            if (delta.magnitude > visionDistance || Vector3.Angle(transform.forward, delta) > visionHalfAngle) return false;
            return Physics.Raycast(eye, delta.normalized, out RaycastHit hit, delta.magnitude + .2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<RaidPlayerController>() == player;
        }
        private void MoveTowards(Vector3 target, float speed)
        {
            Face(target); Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up).normalized;
            if (Physics.Raycast(transform.position + Vector3.up * .7f, direction, .75f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) direction = Quaternion.Euler(0f, 55f, 0f) * direction;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += Physics.gravity.y * Time.deltaTime; controller.Move((direction * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
        }
        private void Face(Vector3 target)
        { Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up); if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 7f); }
        private void Shoot(float distance)
        {
            if (magazine == null || magazine.loadedAmmoCount <= 0) { BeginReload(); return; }
            magazine.loadedAmmoCount--; if (Random.value < .12f) rifle.condition = Mathf.Max(0, rifle.condition - 1);
            Vector3 origin = transform.position + Vector3.up * (crouched ? 1.05f : 1.42f) + transform.forward * .35f;
            Vector3 target = player.transform.position + Vector3.up * 1.15f;
            float arms = 1f - Mathf.Min(bodyHealth["leftArm"] / 60f, bodyHealth["rightArm"] / 60f);
            float spread = Mathf.Lerp(.035f, .14f, Mathf.InverseLerp(5f, visionDistance, distance)) + arms * .09f;
            Vector3 direction = (target - origin).normalized + transform.right * Random.Range(-spread, spread) + Vector3.up * Random.Range(-spread, spread);
            Vector3 end = origin + direction.normalized * visionDistance;
            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, visionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { end = hit.point; if (hit.collider.GetComponentInParent<RaidPlayerController>() == player) DamagePlayer(); }
            DrawTracer(origin, end);
        }
        private void DamagePlayer()
        {
            if (playerHealth == null) return;
            string[] parts = { "chest", "chest", "abdomen", "rightArm", "leftArm", "rightLeg", "leftLeg", "head" };
            string part = parts[Random.Range(0, parts.Length)]; int damage = Random.Range(8, 15);
            ItemInstance protection = RaidContext.Loadout?.items.Find(item => item.equippedSlot == (part == "head" ? "helmet" : "armor"));
            if (protection != null && (part == "head" || part == "chest" || part == "abdomen"))
            { ItemSO data = ItemCatalog.Get(protection.definitionId); damage = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Lerp(.78f, .42f, data.armor.armorClass / 6f))); protection.condition = Mathf.Max(0, protection.condition - Random.Range(1, 4)); }
            playerHealth.ApplyDamage(part, damage, Random.value < .18f, (part.EndsWith("Leg") || part.EndsWith("Arm")) && Random.value < .08f);
        }
        private void UpdatePosture()
        { float height = crouched ? 1.15f : 1.8f; controller.height = Mathf.Lerp(controller.height, height, Time.deltaTime * 8f); controller.center = Vector3.up * controller.height * .5f; }
        private void Die()
        {
            if (state == BotState.Dead) return; state = BotState.Dead; controller.enabled = false;
            CapsuleCollider collider = gameObject.AddComponent<CapsuleCollider>(); collider.height = 1.8f; collider.radius = .35f; collider.center = Vector3.up * .9f;
            Rigidbody body = gameObject.AddComponent<Rigidbody>(); body.mass = 75f; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.interpolation = RigidbodyInterpolation.Interpolate;
            body.AddForce((transform.up - transform.forward * .35f) * 110f, ForceMode.Impulse); body.AddTorque(transform.right * Random.Range(90f, 150f), ForceMode.Impulse);
            gameObject.AddComponent<RaidLootContainer>().FillCorpseLoot(inventory); SetColor(new Color(.12f, .13f, .12f));
        }
        private void CreateBody()
        {
            CreatePart(PrimitiveType.Capsule, "Туловище", "chest", new Vector3(0f,.98f,0f), new Vector3(.62f,.72f,.42f));
            CreatePart(PrimitiveType.Sphere, "Голова", "head", new Vector3(0f,1.68f,0f), Vector3.one * .34f);
            CreatePart(PrimitiveType.Capsule, "Правая рука", "rightArm", new Vector3(-.42f,1.08f,0f), new Vector3(.20f,.52f,.20f));
            CreatePart(PrimitiveType.Capsule, "Левая рука", "leftArm", new Vector3(.42f,1.08f,0f), new Vector3(.20f,.52f,.20f));
            CreatePart(PrimitiveType.Capsule, "Правая нога", "rightLeg", new Vector3(-.18f,.40f,0f), new Vector3(.24f,.58f,.24f));
            CreatePart(PrimitiveType.Capsule, "Левая нога", "leftLeg", new Vector3(.18f,.40f,0f), new Vector3(.24f,.58f,.24f));
            CreatePart(PrimitiveType.Cube, "Оружие", null, new Vector3(.26f,1.08f,.2f), new Vector3(.12f,.12f,.82f));
            renderers = GetComponentsInChildren<Renderer>();
            importedVisual = Resources.Load<GameObject>("Models/Characters/Insurgent/Insurgent_Lite") != null;
            if (importedVisual)
                foreach (Renderer value in renderers) value.enabled = false;
        }
        private void CreatePart(PrimitiveType type, string name, string bodyPart, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(transform, false); part.transform.localPosition = position; part.transform.localScale = scale;
            Collider value = part.GetComponent<Collider>();
            if (string.IsNullOrEmpty(bodyPart)) { if (value != null) Destroy(value); return; }
            RaidBotHitbox hitbox = part.AddComponent<RaidBotHitbox>(); hitbox.owner = this; hitbox.bodyPart = bodyPart;
        }
        private void SetColor(Color color) { if (!importedVisual && renderers != null) foreach (Renderer value in renderers) value.material.color = color; }
        private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x,a.z), new Vector2(b.x,b.z));
        private static void DrawTracer(Vector3 from, Vector3 to)
        { GameObject tracer = new("Трассер бота"); LineRenderer line = tracer.AddComponent<LineRenderer>(); line.positionCount = 2; line.SetPosition(0, from); line.SetPosition(1, to); line.startWidth = .018f; line.endWidth = .005f; line.material = new Material(Shader.Find("Sprites/Default")); line.material.color = new Color(1f,.46f,.12f,.85f); Destroy(tracer, .09f); }
    }

    public sealed class RaidBotHitbox : MonoBehaviour
    {
        public RaidBotController owner;
        public string bodyPart;
        public void ApplyDamage(int amount) { if (owner != null) owner.ApplyBodyDamage(bodyPart, amount); }
    }
}
