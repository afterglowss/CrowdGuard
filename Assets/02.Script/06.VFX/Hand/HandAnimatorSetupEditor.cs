using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;


namespace CrowdGuard.Editor.Hand
{
    /// <summary>
    /// 손 애니메이션 에셋을 자동으로 생성하는 에디터 유틸리티입니다.
    /// Menu: Tools > CrowdGuard > Setup Hand Animator
    /// </summary>
    public static class HandAnimatorSetupEditor
    {
        private const string OutputPath = "Assets/02.Script/Player/Hand/Generated";

        [MenuItem("Tools/CrowdGuard/Setup Hand Animator")]
        public static void SetupHandAnimator()
        {
            if (!AssetDatabase.IsValidFolder(OutputPath))
            {
                AssetDatabase.CreateFolder("Assets/02.Script/Player/Hand", "Generated");
            }

            // 오른손
            var rOpen = CreateClip("HandOpen_R", GetBonePaths("R"), Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity);
            var rClose = CreateClip("HandClose_R", GetBonePaths("R"),
                Quaternion.Euler(80f, 0f, 0f),   // Proximal: X축 +80도
                Quaternion.Euler(80f, 0f, 0f),   // Intermediate
                Quaternion.Euler(60f, 0f, 0f),   // Distal
                Quaternion.Euler(20f, -40f, 0f)  // Thumb
            );
            var rController = CreateAnimatorController("HandAnimator_R", rOpen, rClose);

            // 왼손
            var lOpen = CreateClip("HandOpen_L", GetBonePaths("L"), Quaternion.identity, Quaternion.identity, Quaternion.identity, Quaternion.identity);
            var lClose = CreateClip("HandClose_L", GetBonePaths("L"),
                Quaternion.Euler(80f, 0f, 0f),
                Quaternion.Euler(80f, 0f, 0f),
                Quaternion.Euler(60f, 0f, 0f),
                Quaternion.Euler(20f, 40f, 0f)   // 왼손 엄지는 Y축 반전
            );
            var lController = CreateAnimatorController("HandAnimator_L", lOpen, lClose);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[HandAnimatorSetup] 완료! 좌/우 Animator Controller 생성됨: " + OutputPath);
            EditorUtility.DisplayDialog(
                "Hand Animator 생성 완료",
                "생성된 에셋:\n" +
                "- HandOpen_R/L.anim, HandClose_R/L.anim\n" +
                "- HandAnimator_R.controller, HandAnimator_L.controller\n\n" +
                $"경로: {OutputPath}\n\n" +
                "Left → HandAnimator_L, Right → HandAnimator_R 할당하세요.",
                "확인"
            );
        }

        /// <summary>
        /// 좌/우 구분에 따른 뼈대 경로 배열을 반환합니다.
        /// </summary>
        private static string[][] GetBonePaths(string side)
        {
            // [0] Proximal (4손가락), [1] Intermediate (4손가락), [2] Distal (4손가락), [3] Thumb
            return new[]
            {
                new[]
                {
                    $"{side}_Wrist/{side}_IndexMetacarpal/{side}_IndexProximal",
                    $"{side}_Wrist/{side}_MiddleMetacarpal/{side}_MiddleProximal",
                    $"{side}_Wrist/{side}_RingMetacarpal/{side}_RingProximal",
                    $"{side}_Wrist/{side}_LittleMetacarpal/{side}_LittleProximal",
                },
                new[]
                {
                    $"{side}_Wrist/{side}_IndexMetacarpal/{side}_IndexProximal/{side}_IndexIntermediate",
                    $"{side}_Wrist/{side}_MiddleMetacarpal/{side}_MiddleProximal/{side}_MiddleIntermediate",
                    $"{side}_Wrist/{side}_RingMetacarpal/{side}_RingProximal/{side}_RingIntermediate",
                    $"{side}_Wrist/{side}_LittleMetacarpal/{side}_LittleProximal/{side}_LittleIntermediate",
                },
                new[]
                {
                    $"{side}_Wrist/{side}_IndexMetacarpal/{side}_IndexProximal/{side}_IndexIntermediate/{side}_IndexDistal",
                    $"{side}_Wrist/{side}_MiddleMetacarpal/{side}_MiddleProximal/{side}_MiddleIntermediate/{side}_MiddleDistal",
                    $"{side}_Wrist/{side}_RingMetacarpal/{side}_RingProximal/{side}_RingIntermediate/{side}_RingDistal",
                    $"{side}_Wrist/{side}_LittleMetacarpal/{side}_LittleProximal/{side}_LittleIntermediate/{side}_LittleDistal",
                },
                new[]
                {
                    $"{side}_Wrist/{side}_ThumbMetacarpal/{side}_ThumbProximal",
                },
            };
        }

        private static AnimationClip CreateClip(string clipName, string[][] bonePaths,
            Quaternion proximalRot, Quaternion midRot, Quaternion distalRot, Quaternion thumbRot)
        {
            var clip = new AnimationClip { name = clipName, frameRate = 30f };

            SetBoneRotation(clip, bonePaths[0], proximalRot);
            SetBoneRotation(clip, bonePaths[1], midRot);
            SetBoneRotation(clip, bonePaths[2], distalRot);
            SetBoneRotation(clip, bonePaths[3], thumbRot);

            AssetDatabase.CreateAsset(clip, $"{OutputPath}/{clipName}.anim");
            return clip;
        }

        private static AnimatorController CreateAnimatorController(
            string controllerName, AnimationClip openClip, AnimationClip closeClip)
        {
            var controllerPath = $"{OutputPath}/{controllerName}.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            controller.AddParameter("Grip", AnimatorControllerParameterType.Float);

            var rootStateMachine = controller.layers[0].stateMachine;

            BlendTree blendTree;
            var blendState = controller.CreateBlendTreeInController("Hand Blend", out blendTree);
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Grip";
            blendTree.AddChild(openClip, 0f);
            blendTree.AddChild(closeClip, 1f);

            rootStateMachine.defaultState = blendState;
            return controller;
        }

        private static void SetBoneRotation(AnimationClip clip, string[] bonePaths, Quaternion rotation)
        {
            foreach (var bonePath in bonePaths)
            {
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.x",
                    AnimationCurve.Constant(0f, 0f, rotation.x));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.y",
                    AnimationCurve.Constant(0f, 0f, rotation.y));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.z",
                    AnimationCurve.Constant(0f, 0f, rotation.z));
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.w",
                    AnimationCurve.Constant(0f, 0f, rotation.w));
            }
        }
    }
}
#endif

