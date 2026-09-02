using System.Collections.Generic;
using UnityEngine;

namespace OfflineExtraction.Core
{
    /// <summary>Подключает импортированную humanoid-модель и базовые движения без ссылок из сцены.</summary>
    public sealed class HumanoidMotionDriver : MonoBehaviour
    {
        public bool firstPersonBody;
        public bool seated;
        public float targetHeight = 1.75f;

        private Animator animator;
        private AnimatorOverrideController overrides;
        private AnimationClip idle, walk, run, active;
        private CharacterController character;
        private OfflineExtraction.UI.HubPlayerController hubPlayer;
        private Camera firstPersonCamera;
        private Transform cameraAnchor;
        private Transform visual;
        private Transform leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg;
        private Vector3 standingLocalPosition;

        private void Awake()
        {
            character = GetComponent<CharacterController>();
            hubPlayer = GetComponent<OfflineExtraction.UI.HubPlayerController>();
            firstPersonCamera = GetComponentInChildren<Camera>();
            GameObject source = Resources.Load<GameObject>("Models/Characters/Insurgent/Insurgent_Lite");
            if (source == null) return;

            GameObject instance = Instantiate(source, transform);
            instance.name = firstPersonBody ? "Тело игрока Insurgent" : "Тело бота Insurgent";
            visual = instance.transform;
            visual.localPosition = Vector3.zero;
            // Направление модели уже совпадает с направлением контроллера.
            // Не разворачиваем корневой объект, иначе персонаж идёт спиной вперёд.
            visual.localRotation = Quaternion.identity;
            FitToCharacter(instance);
            ConvertMaterials(instance);
            standingLocalPosition = visual.localPosition;

            animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            }
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("Animations/HumanBasicMotions/HumanBasicMotionsScene");
            idle = LoadClip("Animations/HumanBasicMotions/HumanM_Idle01");
            walk = LoadClip("Animations/HumanBasicMotions/HumanM_WalkForward");
            run = LoadClip("Animations/HumanBasicMotions/HumanM_RunForward");
            if (animator != null && controller != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                overrides = new AnimatorOverrideController(controller);
                animator.runtimeAnimatorController = overrides;
                for (int layer = 1; layer < animator.layerCount; layer++) animator.SetLayerWeight(layer, 0f);
                SetClip(idle, true);
            }
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            visual.localPosition = seated
                ? standingLocalPosition + new Vector3(0f, -.82f, -.08f)
                : standingLocalPosition;
            if (seated)
            {
                SetClip(idle);
                ApplySittingPose();
                if (firstPersonBody) UpdateFirstPersonHead();
                return;
            }
            float speed = character != null && character.enabled
                ? Vector3.ProjectOnPlane(character.velocity, Vector3.up).magnitude
                : 0f;
            SetClip(speed > 2.15f ? run : speed > .12f ? walk : idle);
            if (firstPersonBody) UpdateFirstPersonHead();
        }

        private void UpdateFirstPersonHead()
        {
            if (animator == null || firstPersonCamera == null) return;
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone == null) return;

            if (cameraAnchor == null)
            {
                GameObject anchorObject = new("Камера на голове");
                cameraAnchor = anchorObject.transform;
                cameraAnchor.SetParent(headBone, false);
                firstPersonCamera.transform.SetParent(cameraAnchor, false);
                firstPersonCamera.transform.localPosition = Vector3.zero;
                firstPersonCamera.transform.localRotation = Quaternion.identity;
            }

            float pitch = hubPlayer == null ? 0f : hubPlayer.LookPitch;
            float yaw = seated && hubPlayer != null ? hubPlayer.SeatedLookYaw : 0f;

            // Поворачиваем голову вместе со взглядом. Дельта применяется после
            // Animator, поэтому не накапливается от кадра к кадру.
            headBone.rotation = Quaternion.AngleAxis(yaw, transform.up)
                * Quaternion.AngleAxis(pitch * .72f, transform.right)
                * headBone.rotation;

            // Якорь действительно является дочерним объектом головы, но его
            // мировая ориентация выравнивается по игроку: костные оси FBX не
            // искажают управление мышью. Точка вынесена перед лицом.
            cameraAnchor.position = headBone.position + transform.forward * .15f + Vector3.up * .025f;
            cameraAnchor.rotation = transform.rotation;
        }

        private void FitToCharacter(GameObject instance)
        {
            Renderer[] values = instance.GetComponentsInChildren<Renderer>(true);
            if (values.Length == 0) return;
            Bounds bounds = values[0].bounds;
            for (int i = 1; i < values.Length; i++) bounds.Encapsulate(values[i].bounds);
            float scale = targetHeight / Mathf.Max(.01f, bounds.size.y);
            visual.localScale = Vector3.one * scale;
            bounds = values[0].bounds;
            for (int i = 1; i < values.Length; i++) bounds.Encapsulate(values[i].bounds);
            visual.position += Vector3.up * (transform.position.y - bounds.min.y);
        }

        private static AnimationClip LoadClip(string path)
        {
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(path);
            foreach (AnimationClip clip in clips)
                if (!clip.name.StartsWith("__preview__")) return clip;
            return clips.Length > 0 ? clips[0] : null;
        }

        private void ApplySittingPose()
        {
            // Поза строится в мировых направлениях и потому не зависит от
            // локальных осей костей конкретной humanoid-модели.
            Vector3 thighDirection = (transform.forward * .88f + Vector3.down * .38f).normalized;
            AimBone(leftUpperLeg, leftLowerLeg, thighDirection);
            AimBone(rightUpperLeg, rightLowerLeg, thighDirection);

            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            AimBone(leftLowerLeg, leftFoot, Vector3.down);
            AimBone(rightLowerLeg, rightFoot, Vector3.down);
        }

        private static void AimBone(Transform bone, Transform child, Vector3 desiredDirection)
        {
            if (bone == null || child == null) return;
            Vector3 currentDirection = child.position - bone.position;
            if (currentDirection.sqrMagnitude < .0001f) return;
            bone.rotation = Quaternion.FromToRotation(currentDirection.normalized, desiredDirection.normalized) * bone.rotation;
        }

        private static void ConvertMaterials(GameObject instance)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) return;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] values = renderer.materials;
                for (int i = 0; i < values.Length; i++)
                {
                    Material source = values[i];
                    if (source == null || source.shader == urp) continue;
                    Texture albedo = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    Texture normal = source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null;
                    Texture metallic = source.HasProperty("_MetallicGlossMap") ? source.GetTexture("_MetallicGlossMap") : null;
                    Texture occlusion = source.HasProperty("_OcclusionMap") ? source.GetTexture("_OcclusionMap") : null;
                    Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    float smoothness = source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : .35f;
                    float metal = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f;

                    Material converted = new(urp) { name = source.name + " (URP Runtime)" };
                    converted.SetColor("_BaseColor", color);
                    converted.SetFloat("_Smoothness", smoothness);
                    converted.SetFloat("_Metallic", metal);
                    if (albedo != null) converted.SetTexture("_BaseMap", albedo);
                    if (normal != null)
                    {
                        converted.SetTexture("_BumpMap", normal);
                        converted.EnableKeyword("_NORMALMAP");
                    }
                    if (metallic != null)
                    {
                        converted.SetTexture("_MetallicGlossMap", metallic);
                        converted.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }
                    if (occlusion != null) converted.SetTexture("_OcclusionMap", occlusion);
                    values[i] = converted;
                }
                renderer.materials = values;
            }
        }

        private void SetClip(AnimationClip clip, bool immediate = false)
        {
            if (animator == null || overrides == null || clip == null || clip == active) return;
            active = clip;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clip);
            overrides.ApplyOverrides(pairs);
            animator.CrossFade("BaseAnimation", immediate ? 0f : .16f, 0);
        }
    }
}
