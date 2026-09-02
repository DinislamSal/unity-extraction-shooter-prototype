using OfflineExtraction.Core;
using UnityEngine;

namespace OfflineExtraction.UI
{
    public sealed class HubWorldBuilder : MonoBehaviour
    {
        private Material wood, darkWood, metal, blackMetal, fabric, cardboard, ember, stone, brass, glass;

        private void Awake()
        {
            CreateMaterials();
            PrepareScene();
            BuildLivingRoom();
            BuildBedroom();
            BuildCorridor();
            BuildStoragePreview();
            BuildArmoryPreview();
            BuildBunker();
            BuildPlayer();
        }

        private void CreateMaterials()
        {
            Texture2D agedWood = Resources.Load<Texture2D>("Textures/Shelter/aged_wood_albedo");
            Texture2D wornFabric = Resources.Load<Texture2D>("Models/Shelter/Armchair/armchair_fabric_albedo");
            Texture2D fabricNormal = Resources.Load<Texture2D>("Models/Shelter/Armchair/armchair_fabric_normal");
            Texture2D fabricAo = Resources.Load<Texture2D>("Models/Shelter/Armchair/armchair_fabric_ao");
            Texture2D sootStone = Resources.Load<Texture2D>("Textures/Shelter/soot_stone_albedo");
            wood = Material(new Color(.72f, .65f, .56f), agedWood, new Vector2(2.5f, 2.5f), .16f);
            darkWood = Material(new Color(.42f, .38f, .34f), agedWood, new Vector2(3.2f, 3.2f), .1f);
            metal = Material(new Color(.16f, .18f, .18f));
            blackMetal = Material(new Color(.045f, .052f, .052f));
            fabric = Material(Color.white, wornFabric, Vector2.one, .08f);
            if (fabricNormal != null && fabric.HasProperty("_BumpMap"))
            {
                fabric.SetTexture("_BumpMap", fabricNormal); fabric.SetFloat("_BumpScale", .7f); fabric.EnableKeyword("_NORMALMAP");
            }
            if (fabricAo != null && fabric.HasProperty("_OcclusionMap"))
            {
                fabric.SetTexture("_OcclusionMap", fabricAo); fabric.SetFloat("_OcclusionStrength", .75f);
            }
            stone = Material(new Color(.72f, .68f, .62f), sootStone, new Vector2(1.6f, 1.6f), .06f);
            brass = Material(new Color(.38f, .24f, .08f), null, null, .55f);
            glass = Material(new Color(.12f, .22f, .2f), null, null, .72f);
            cardboard = Material(new Color(.34f, .23f, .12f));
            ember = Material(new Color(1f, .18f, .025f));
            if (ember.HasProperty("_EmissionColor"))
            {
                ember.EnableKeyword("_EMISSION");
                ember.SetColor("_EmissionColor", new Color(2.5f, .35f, .025f));
            }
        }

        private void PrepareScene()
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.035f, .04f, .045f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.025f, .028f, .03f);
            RenderSettings.fogDensity = .012f;
        }

        private void BuildLivingRoom()
        {
            Box("Living Floor", new(0, -.15f, 0), new(11, .3f, 9), wood);
            Box("Living Ceiling", new(0, 3.15f, 0), new(11, .3f, 9), darkWood);
            Box("Living South", new(0, 1.5f, -4.5f), new(11, 3.3f, .25f), darkWood);
            Box("Living East", new(5.5f, 1.5f, 0), new(.25f, 3.3f, 9), darkWood);
            Box("Living West A", new(-5.5f, 1.5f, -2.6f), new(.25f, 3.3f, 3.8f), darkWood);
            Box("Living West B", new(-5.5f, 1.5f, 3.2f), new(.25f, 3.3f, 2.6f), darkWood);
            Box("Living North A", new(-3.5f, 1.5f, 4.5f), new(4, 3.3f, .25f), darkWood);
            Box("Living North B", new(3.5f, 1.5f, 4.5f), new(4, 3.3f, .25f), darkWood);

            BuildFireplace();
            Box("Rug", new(1.4f, .025f, -1.35f), new(4.2f, .05f, 3.3f), fabric);
            Vector3 chairPosition = new(1.65f, 0, -1.45f);
            Vector3 fireplacePosition = new(3.9f, 0, -3.92f);
            Quaternion chairRotation = Quaternion.LookRotation((fireplacePosition - chairPosition).normalized, Vector3.up)
                * Quaternion.Euler(0f, 180f, 0f);
            GameObject chair = BuildArmchair(chairPosition, chairRotation);
            Transform seat = new GameObject("Seat Point").transform;
            Bounds chairBounds = RendererBounds(chair);
            // Точка тела находится на подушке, а не в центре полной высоты кресла.
            // Небольшой сдвиг назад усаживает игрока глубже к спинке.
            // Импортированное кресло имеет обратное направление посадки.
            // Разворачиваем только игрока в кресле, не затрагивая ходьбу.
            seat.rotation = chair.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            seat.position = new Vector3(chairBounds.center.x, chairBounds.min.y + chairBounds.size.y * .39f, chairBounds.center.z)
                + seat.forward * .22f;
            seat.SetParent(chair.transform, true);
            InteractionVolume("Armchair Interaction", chair.transform.position + Vector3.up * .75f, new Vector3(2f, 1.9f, 2f), HubAction.Chair, "сесть в кресло", seat);
            BuildRadioTable(new Vector3(.35f, 0, -1.45f));

            GameObject raidDoor = Box("Entrance Door", new(0, 1.25f, -4.32f), new(1.65f, 2.5f, .18f), darkWood);
            AddAction(raidDoor, HubAction.Raid, "выйти в рейд");
        }

        private void BuildFireplace()
        {
            Transform root = new GameObject("Stone Fireplace").transform;
            root.SetParent(transform); root.position = new Vector3(3.9f, 0, -3.92f);
            Part(root, "Left Stone Pier", new(-.78f, .78f, 0), new(.55f, 1.56f, .72f), stone);
            Part(root, "Right Stone Pier", new(.78f, .78f, 0), new(.55f, 1.56f, .72f), stone);
            Part(root, "Stone Header", new(0, 1.52f, 0), new(2.1f, .48f, .72f), stone);
            Part(root, "Stone Hearth", new(0, .1f, .25f), new(2.25f, .2f, 1.18f), stone);
            Part(root, "Firebox Back", new(0, .62f, -.28f), new(1.12f, .82f, .08f), blackMetal);
            Part(root, "Oak Mantel", new(0, 1.86f, .05f), new(2.45f, .18f, .94f), darkWood);
            for (int i = -1; i <= 1; i++)
            {
                GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "Burning Log"; log.transform.SetParent(root);
                log.transform.localPosition = new Vector3(i * .23f, .27f, .08f);
                log.transform.localRotation = Quaternion.Euler(0, 0, 82f + i * 7f);
                log.transform.localScale = new Vector3(.12f, .42f, .12f);
                log.GetComponent<Renderer>().sharedMaterial = darkWood;
            }

            GameObject firePrefab = Resources.Load<GameObject>("VFX/ShelterFire/VFX_Fire_01_Small");
            if (firePrefab != null)
            {
                // Центральное пламя сохраняет прежнюю высоту и насыщенность.
                SpawnFireVfx(firePrefab, root, "Fire Center", new Vector3(0f, .24f, .08f), .68f);

                // Два меньших очага расширяют огонь вдоль поленьев. Используем
                // облегчённую версию без лишнего плотного дыма, чтобы центр
                // не стал перегруженным и пламя не выглядело растянутой картинкой.
                GameObject sideFirePrefab = Resources.Load<GameObject>("VFX/ShelterFire/VFX_Fire_01_Small_Simple") ?? firePrefab;
                SpawnFireVfx(sideFirePrefab, root, "Fire Left", new Vector3(-.29f, .23f, .08f), .5f);
                SpawnFireVfx(sideFirePrefab, root, "Fire Right", new Vector3(.29f, .23f, .08f), .5f);
            }
            else
            {
                // Страховочное свечение, если VFX-пакет будет удалён из проекта.
                Part(root, "Fire Glow · Procedural Fallback", new(0, .5f, -.22f), new(.92f, .5f, .06f), ember);
            }

            GameObject lightObject = new("Living Fire Light");
            lightObject.transform.SetParent(root); lightObject.transform.localPosition = new Vector3(0, .68f, .62f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1f, .24f, .045f); light.range = 6.2f; light.intensity = 3.8f;
            light.shadows = LightShadows.Soft; light.shadowStrength = .82f;
            HubFireFlicker flicker = lightObject.AddComponent<HubFireFlicker>();
            flicker.baseIntensity = 3.8f; flicker.baseRange = 6.2f;
        }

        private static void SpawnFireVfx(GameObject prefab, Transform parent, string name, Vector3 localPosition, float scale)
        {
            GameObject fire = Instantiate(prefab, parent);
            fire.name = name + " · Free Fire VFX URP";
            fire.transform.localPosition = localPosition;
            fire.transform.localRotation = Quaternion.identity;
            fire.transform.localScale = Vector3.one * scale;
            foreach (ParticleSystem particles in fire.GetComponentsInChildren<ParticleSystem>(true))
                particles.Play(true);
        }

        private GameObject BuildArmchair(Vector3 position, Quaternion rotation)
        {
            GameObject imported = Resources.Load<GameObject>("Models/Shelter/FurnitureChair/Fotel");
            if (imported != null)
            {
                GameObject model = Instantiate(imported, Vector3.zero, rotation, transform);
                model.name = "Fireplace Armchair · Furniture FREE Pack";
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds source = RendererBounds(model);
                    float fit = Mathf.Min(1.55f / Mathf.Max(.01f, source.size.x), 1.42f / Mathf.Max(.01f, source.size.y));
                    fit = Mathf.Min(fit, 1.5f / Mathf.Max(.01f, source.size.z));
                    model.transform.localScale *= fit;

                    // Пакет выпущен под Built-in Render Pipeline. Сохраняем
                    // его albedo, normal и metallic-карту на материале URP.
                    foreach (Renderer renderer in renderers)
                    {
                        Material sourceMaterial = renderer.sharedMaterial;
                        if (sourceMaterial == null) continue;
                        Material compatible = Material(Color.white, sourceMaterial.mainTexture, Vector2.one, .34f);
                        if (sourceMaterial.HasProperty("_BumpMap") && compatible.HasProperty("_BumpMap"))
                        {
                            compatible.SetTexture("_BumpMap", sourceMaterial.GetTexture("_BumpMap"));
                            compatible.EnableKeyword("_NORMALMAP");
                        }
                        if (sourceMaterial.HasProperty("_MetallicGlossMap") && compatible.HasProperty("_MetallicGlossMap"))
                        {
                            compatible.SetTexture("_MetallicGlossMap", sourceMaterial.GetTexture("_MetallicGlossMap"));
                            compatible.EnableKeyword("_METALLICSPECGLOSSMAP");
                        }
                        renderer.sharedMaterial = compatible;
                    }

                    Bounds fitted = RendererBounds(model);
                    model.transform.position += new Vector3(position.x - fitted.center.x, position.y - fitted.min.y, position.z - fitted.center.z);
                    fitted = RendererBounds(model);
                    if (model.GetComponentInChildren<Collider>() == null)
                    {
                        BoxCollider collision = model.AddComponent<BoxCollider>();
                        collision.center = model.transform.InverseTransformPoint(fitted.center);
                        Vector3 scale = model.transform.lossyScale;
                        collision.size = new Vector3(fitted.size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)), fitted.size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)), fitted.size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
                    }
                    return model;
                }
                Destroy(model);
            }

            GameObject root = new("Old Armchair · Procedural Fallback"); root.transform.SetParent(transform); root.transform.position = position; root.transform.rotation = rotation;
            Part(root.transform, "Chair Seat", new(0, .52f, 0), new(1.12f, .28f, 1.08f), fabric);
            Part(root.transform, "Chair Back", new(0, 1.18f, .43f), new(1.16f, 1.22f, .28f), fabric, Quaternion.Euler(-7f, 0, 0));
            Part(root.transform, "Chair Left Arm", new(-.68f, .82f, -.02f), new(.25f, .55f, 1.15f), fabric);
            Part(root.transform, "Chair Right Arm", new(.68f, .82f, -.02f), new(.25f, .55f, 1.15f), fabric);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Part(root.transform, "Chair Wooden Leg", new(x * .45f, .18f, z * .38f), new(.13f, .36f, .13f), darkWood);
            return root;
        }

        private static Bounds RendererBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            Renderer first = null;
            foreach (Renderer renderer in renderers)
                if (renderer.enabled) { first = renderer; break; }
            if (first == null) return new Bounds(target.transform.position, Vector3.one);
            Bounds bounds = first.bounds;
            foreach (Renderer renderer in renderers)
                if (renderer.enabled && renderer != first) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private void BuildRadioTable(Vector3 position)
        {
            Transform table = new GameObject("Radio Side Table").transform; table.SetParent(transform); table.position = position;
            table.rotation = Quaternion.Euler(0f, 120f, 0f);
            Part(table, "Table Top", new(0, .76f, 0), new(.9f, .14f, .78f), darkWood);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Part(table, "Table Leg", new(x * .34f, .37f, z * .28f), new(.11f, .74f, .11f), darkWood);

            GameObject radioPrefab = Resources.Load<GameObject>("Models/Shelter/OldRadio/Radio");
            if (radioPrefab != null)
            {
                GameObject radio = Instantiate(radioPrefab, Vector3.zero, Quaternion.identity, table);
                radio.name = "Old Tube Radio · Asset Store";
                // Instantiate с мировой ориентацией компенсирует поворот родителя.
                // Сбрасываем локальный поворот, чтобы радио повторяло поворот тумбочки на 120°.
                radio.transform.localRotation = Quaternion.identity;

                Bounds source = RendererBounds(radio);
                float fit = Mathf.Min(.78f / Mathf.Max(.01f, source.size.x), .52f / Mathf.Max(.01f, source.size.y));
                fit = Mathf.Min(fit, .64f / Mathf.Max(.01f, source.size.z));
                radio.transform.localScale *= fit;

                // Верх столешницы находится на высоте 0.83 м относительно стола.
                // Центрируем модель по X/Z и ставим её нижней гранью точно на поверхность.
                Bounds fitted = RendererBounds(radio);
                radio.transform.position += new Vector3(
                    table.position.x - fitted.center.x,
                    table.position.y + .83f - fitted.min.y,
                    table.position.z - fitted.center.z);

                // Старый пакет использует Standard. В URP сохраняем его текстуры,
                // но переносим их на совместимый материал, чтобы радио не стало розовым.
                foreach (Renderer renderer in radio.GetComponentsInChildren<Renderer>())
                {
                    Material sourceMaterial = renderer.sharedMaterial;
                    if (sourceMaterial == null) continue;
                    Material compatible = Material(Color.white, sourceMaterial.mainTexture, Vector2.one, .42f);
                    if (sourceMaterial.HasProperty("_BumpMap") && compatible.HasProperty("_BumpMap"))
                    {
                        compatible.SetTexture("_BumpMap", sourceMaterial.GetTexture("_BumpMap"));
                        compatible.EnableKeyword("_NORMALMAP");
                    }
                    if (sourceMaterial.HasProperty("_OcclusionMap") && compatible.HasProperty("_OcclusionMap"))
                        compatible.SetTexture("_OcclusionMap", sourceMaterial.GetTexture("_OcclusionMap"));
                    renderer.sharedMaterial = compatible;
                }
            }
            else
            {
                // Временная модель остаётся страховкой, если prefab случайно удалят.
                Transform radio = new GameObject("Old Tube Radio · Procedural Fallback").transform;
                radio.SetParent(table); radio.localPosition = new Vector3(0, .99f, 0);
                Part(radio, "Radio Cabinet", Vector3.zero, new(.72f, .4f, .34f), darkWood);
                Part(radio, "Radio Dial Glass", new(.1f, .05f, -.18f), new(.36f, .12f, .025f), glass);
                Part(radio, "Radio Speaker", new(-.2f, .02f, -.18f), new(.18f, .24f, .025f), blackMetal);
            }
            InteractionVolume("Radio Interaction", table.position + Vector3.up * 1.05f, new Vector3(1.15f, .8f, 1f), HubAction.Radio, "включить или выключить радио");
        }

        private void BuildBedroom()
        {
            Box("Bedroom Floor", new(-8.5f, -.15f, .4f), new(6, .3f, 7), wood);
            Box("Bedroom Ceiling", new(-8.5f, 3.15f, .4f), new(6, .3f, 7), darkWood);
            Box("Bedroom West", new(-11.5f, 1.5f, .4f), new(.25f, 3.3f, 7), darkWood);
            Box("Bedroom North", new(-8.5f, 1.5f, 3.9f), new(6, 3.3f, .25f), darkWood);
            Box("Bedroom South", new(-8.5f, 1.5f, -3.1f), new(6, 3.3f, .25f), darkWood);
            Box("Cardboard Bed A", new(-9f, .03f, .6f), new(2.4f, .06f, 1.4f), cardboard);
            Box("Cardboard Bed B", new(-8.6f, .07f, .15f), new(1.8f, .04f, 1.25f), cardboard);
            PointLight("Bedroom Weak Light", new(-8.5f, 2.6f, .4f), new Color(1f, .65f, .33f), 2f, .65f);
        }

        private void BuildCorridor()
        {
            Box("Corridor Floor", new(0, -.15f, 10), new(3.2f, .3f, 11), wood);
            Box("Corridor Ceiling", new(0, 3.15f, 10), new(3.2f, .3f, 11), darkWood);
            Box("Corridor Left", new(-1.6f, 1.5f, 10), new(.2f, 3.3f, 11), darkWood);
            Box("Corridor Right", new(1.6f, 1.5f, 10), new(.2f, 3.3f, 11), darkWood);
            PointLight("Corridor Bulb", new(0, 2.72f, 9.2f), new Color(1f, .72f, .42f), 5f, 1.15f);

            GameObject storage = DoorWithWindow("Storage Door", new(-1.48f, 1.25f, 7.4f), Quaternion.Euler(0, 90, 0), wood);
            AddAction(storage, HubAction.Storage, "открыть хранилище");
            GameObject armory = DoorWithWindow("Shop Door", new(1.48f, 1.25f, 10.2f), Quaternion.Euler(0, 90, 0), metal);
            AddAction(armory, HubAction.Armory, "открыть магазин");
            // Сплошная торцевая стена и дверная коробка закрывают щели вокруг двери.
            Box("Bunker Door Wall Left", new(-1.225f, 1.5f, 15.4f), new(.75f, 3f, .24f), blackMetal);
            Box("Bunker Door Wall Right", new(1.225f, 1.5f, 15.4f), new(.75f, 3f, .24f), blackMetal);
            Box("Bunker Door Wall Header", new(0, 2.75f, 15.4f), new(1.7f, .5f, .24f), blackMetal);
            Box("Bunker Door Frame Left", new(-.91f, 1.45f, 15.27f), new(.12f, 2.9f, .12f), metal);
            Box("Bunker Door Frame Right", new(.91f, 1.45f, 15.27f), new(.12f, 2.9f, .12f), metal);
            Box("Bunker Door Frame Top", new(0, 2.56f, 15.27f), new(1.94f, .12f, .12f), metal);
            HingedDoor("Bunker Door", new(0, 1.25f, 15.38f), 1.7f, 2.5f, blackMetal);
        }

        private void BuildStoragePreview()
        {
            Box("Storage Preview Floor", new(-4.6f, -.15f, 7.4f), new(5.8f, .3f, 4.8f), darkWood);
            for (int i = 0; i < 3; i++)
            {
                Box("Storage Shelf " + i, new(-5.8f + i * 1.25f, 1f, 8.5f), new(.85f, 2f, .35f), metal);
                Box("Storage Box " + i, new(-5.8f + i * 1.25f, .75f, 8.2f), new(.6f, .5f, .55f), cardboard);
            }
            PointLight("Storage Lamp", new(-4.6f, 2.5f, 7.4f), new Color(1f, .76f, .5f), 3.5f, .75f);
        }

        private void BuildArmoryPreview()
        {
            Box("Shop Preview Floor", new(4.6f, -.15f, 10.2f), new(5.8f, .3f, 4.8f), metal);
            for (int i = 0; i < 3; i++) Box("Weapon Rack " + i, new(5.2f, .75f + i * .55f, 10.8f), new(2.2f, .12f, .25f), blackMetal);
            Box("Weapon Safe A", new(3.3f, .9f, 11.4f), new(.9f, 1.8f, .7f), blackMetal);
            Box("Weapon Safe B", new(4.4f, .9f, 11.4f), new(.9f, 1.8f, .7f), blackMetal);
            PointLight("Shop Lamp", new(4.6f, 2.5f, 10.2f), new Color(1f, .76f, .5f), 3.5f, .75f);
        }

        private void BuildBunker()
        {
            // Лестничный спуск и нижнее железное помещение.
            for (int i = 0; i < 8; i++) Box("Bunker Step " + i, new(0, -.25f - i * .32f, 16.1f + i * .55f), new(2.32f, .28f, .55f), metal);
            // Верхняя площадка соединяет пол за дверью с первой ступенью без щели.
            Box("Bunker Top Landing", new(0, -.14f, 15.65f), new(2.32f, .28f, .7f), metal);
            // Нижняя переходная ступень убирает полуметровый перепад между
            // полом бункера и первой ступенью подъёма.
            Box("Bunker Bottom Step", new(0, -2.77f, 20.5f), new(2.32f, .28f, .55f), metal);
            // Сплошная наклонная поверхность закрывает просветы под ступенями.
            Box("Bunker Stair Underlay", new(0, -1.53f, 18.03f), new(2.32f, .2f, 5.1f), blackMetal, Quaternion.Euler(30.2f, 0, 0));
            // Закрытый лестничный коридор.
            Box("Stair Corridor Left", new(-1.25f, -.2f, 17.95f), new(.22f, 5.8f, 5.5f), blackMetal);
            Box("Stair Corridor Right", new(1.25f, -.2f, 17.95f), new(.22f, 5.8f, 5.5f), blackMetal);
            Box("Stair Corridor Ceiling", new(0, 2.6f, 17.95f), new(2.7f, .22f, 5.5f), blackMetal);
            Box("Bunker Floor", new(0, -3.0f, 23), new(12, .3f, 8), metal);
            // Потолок поднят и над лестничным спуском, и над основной комнатой.
            Box("Bunker Ceiling", new(0, 2.6f, 23), new(12, .25f, 8), blackMetal);
            Box("Bunker Left", new(-6, -.2f, 23), new(.25f, 5.8f, 8), blackMetal);
            Box("Bunker Right", new(6, -.2f, 23), new(.25f, 5.8f, 8), blackMetal);
            Box("Bunker End", new(0, -.2f, 27), new(12, 5.8f, .25f), blackMetal);
            // Передняя стена закрывает бункер со стороны лестницы, сохраняя проём.
            Box("Bunker Front Left", new(-3.68f, -.2f, 19.02f), new(4.64f, 5.8f, .25f), blackMetal);
            Box("Bunker Front Right", new(3.68f, -.2f, 19.02f), new(4.64f, 5.8f, .25f), blackMetal);
            Box("Bunker Front Header", new(0, 2f, 19.02f), new(2.6f, 1.2f, .25f), blackMetal);
            GameObject generator = Box("Broken Generator", new(-3.9f, -2.0f, 21.2f), new(2.1f, 1.7f, 1.4f), blackMetal);
            AddAction(generator, HubAction.Bunker, "осмотреть генератор и улучшения бункера");
            Box("Fuel Shelf", new(-5.25f, -1.75f, 24.4f), new(.65f, 2.1f, 2.5f), metal);
            for (int i = 0; i < 4; i++) Box("Fuel Slot " + i, new(-5.05f, -2.55f + (i / 2) * .85f, 23.85f + (i % 2) * 1.0f), new(.35f, .65f, .55f), cardboard);
            GameObject workbench = Box("Abandoned Workbench", new(0, -2.25f, 26.28f), new(3.5f, 1.1f, 1.15f), darkWood);
            AddAction(workbench, HubAction.Workbench, "использовать верстак");
            Transform display = new GameObject("Workbench Weapon Display").transform;
            // Независимая мировая точка над столешницей: оружие падает на стол под действием физики.
            display.SetParent(transform); display.position = new Vector3(0, -1.52f, 26.28f); display.rotation = Quaternion.identity;
            for (int i = 0; i < 3; i++) Box("Broken Trophy Shelf " + i, new(3.6f, -2.1f + i * .75f, 26.5f), new(3.2f, .12f, .45f), darkWood, Quaternion.Euler(0, 0, i == 1 ? 5 : -3));
            // Свеча генератора закреплена выше на левой стене.
            Candle(new(-5.78f, -.65f, 21.4f));
            Candle(new(1.25f, -1.52f, 26.12f));
            // Свеча трофейной зоны закреплена на правой стене перед полками,
            // чтобы корпус и держатель не проходили сквозь полки.
            Candle(new(5.72f, -1.35f, 25.75f));
        }

        private void BuildPlayer()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new("Hub Camera");
                camera = cameraObject.AddComponent<Camera>(); cameraObject.tag = "MainCamera";
            }
            GameObject player = new("Shelter Player");
            player.transform.position = new Vector3(0, .05f, -3.1f);
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 1.75f; character.radius = .32f; character.center = new Vector3(0, .875f, 0);
            character.stepOffset = .42f;
            character.slopeLimit = 52f;
            character.skinWidth = .06f;
            camera.transform.SetParent(player.transform, false);
            // Камера немного вынесена перед лицом: голова модели не перекрывает обзор,
            // а грудь и ноги остаются видимыми при взгляде вниз.
            camera.transform.localPosition = new Vector3(0, 1.64f, .08f);
            camera.transform.localRotation = Quaternion.identity;
            camera.fieldOfView = 72f;
            // Маленькая ближняя плоскость не пересекает стены, когда игрок
            // подходит вплотную и поворачивает камеру вдоль поверхности.
            camera.nearClipPlane = .025f;
            player.AddComponent<HubPlayerController>();
            HumanoidMotionDriver body = player.AddComponent<HumanoidMotionDriver>();
            body.firstPersonBody = true;
            body.targetHeight = 1.75f;
            HubWorkbenchUI workbench = player.AddComponent<HubWorkbenchUI>();
            GameObject display = GameObject.Find("Workbench Weapon Display");
            if (display != null) workbench.weaponDisplay = display.transform;
            player.AddComponent<HubInteraction>();
        }

        private void HingedDoor(string name, Vector3 center, float width, float height, Material material)
        {
            GameObject hinge = new(name + " Hinge"); hinge.transform.SetParent(transform);
            hinge.transform.position = center + Vector3.left * width * .5f;
            GameObject leaf = Box(name, center, new Vector3(width, height, .22f), material);
            leaf.transform.SetParent(hinge.transform, true);
            AddAction(hinge, HubAction.Door, "открыть дверь");
        }

        private GameObject DoorWithWindow(string name, Vector3 position, Quaternion rotation, Material material)
        {
            GameObject door = Box(name, position, new(1.7f, 2.5f, .18f), material, rotation);
            GameObject window = Box(name + " Window", position + rotation * new Vector3(0, .35f, -.12f), new(.85f, .55f, .04f), blackMetal, rotation);
            window.transform.SetParent(door.transform, true);
            return door;
        }

        private void Candle(Vector3 position)
        {
            Box("Candle Wall Holder", position + new Vector3(0, -.18f, .12f), new(.42f, .07f, .4f), metal);
            Box("Candle", position, new(.09f, .28f, .09f), cardboard);
            Box("Candle Flame", position + Vector3.up * .19f, new(.06f, .11f, .06f), ember);
            Light light = PointLight("Candle Light", position + Vector3.up * .25f, new Color(1f, .45f, .12f), 2.3f, .55f);
            HubFireFlicker flicker = light.gameObject.AddComponent<HubFireFlicker>();
            flicker.baseIntensity = .55f; flicker.baseRange = 2.3f; flicker.speed = 5.5f;
        }

        private void AddAction(GameObject target, HubAction action, string prompt, Transform seat = null)
        {
            HubInteractable interactable = target.AddComponent<HubInteractable>();
            interactable.action = action; interactable.prompt = prompt; interactable.seatPoint = seat;
        }

        private void InteractionVolume(string name, Vector3 position, Vector3 size, HubAction action, string prompt, Transform seat = null)
        {
            GameObject volume = new(name); volume.transform.SetParent(transform); volume.transform.position = position;
            BoxCollider collider = volume.AddComponent<BoxCollider>(); collider.size = size; collider.isTrigger = true;
            AddAction(volume, action, prompt, seat);
        }

        private GameObject Box(string name, Vector3 position, Vector3 scale, Material material, Quaternion rotation = default)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(transform); go.transform.position = position;
            go.transform.rotation = rotation == default ? Quaternion.identity : rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private GameObject Part(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, Quaternion rotation = default)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation == default ? Quaternion.identity : rotation;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private Light PointLight(string name, Vector3 position, Color color, float range, float intensity)
        {
            GameObject go = new(name); go.transform.SetParent(transform); go.transform.position = position;
            Light light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.range = range; light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .72f;
            return light;
        }

        private static Material Material(Color color, Texture texture = null, Vector2? tiling = null, float smoothness = .2f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { color = color };
            if (texture != null)
            {
                material.mainTexture = texture;
                material.mainTextureScale = tiling ?? Vector2.one;
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }
    }
}
