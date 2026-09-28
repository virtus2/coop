using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class SetupWeaponAnimationSystem
{
    private const string WEAPONS_ANIM_DIR = "Assets/Animations/Weapons";
    private const string OVERRIDES_DIR = "Assets/Animations/Overrides";
    private const string CONTROLLER_PATH = "Assets/Animations/DummyAnimatorController.controller";
    private const string MASK_PATH = "Assets/Animations/UpperBodyMask.mask";
    private const string PREFAB_PATH = "Assets/Prefabs/PlayerDummyPrefab.prefab";

    [MenuItem("Tools/Weapons/Setup Complete Animation System")]
    public static void Setup()
    {
        Debug.Log("[SetupWeaponAnimationSystem] 무기 애니메이션 시스템 구축 시작...");

        EnsureDirectories();
        ConfigureFBXImportSettings();

        // FBX 클립 캐싱
        AnimationClip standingIdle = GetClip("standing_idle.fbx");
        AnimationClip standingWalkFwd = GetClip("standing_walk_forward.fbx");
        AnimationClip standingWalkBack = GetClip("standing_walk_back.fbx");
        AnimationClip standingWalkLeft = GetClip("standing_walk_left.fbx");
        AnimationClip standingWalkRight = GetClip("standing_walk_right(1).fbx");
        if (standingWalkRight == null) standingWalkRight = GetClip("standing_walk_right(2).fbx");
        AnimationClip rifleSprint = GetClip("rifle_sprint.fbx");

        AnimationClip rifleIdle = GetClip("rifle_idle_1.fbx");
        AnimationClip rifleFire = GetClip("rifle_fire_single.fbx");
        AnimationClip rifleReload = GetClip("rifle_walk_reload.fbx");

        AnimationClip shotgunIdle = GetClip("shotgun_aim_idle.fbx");
        AnimationClip shotgunFire = GetClip("shotgun_fire_pump.fbx");

        AnimationClip oneHandIdle = GetClip("1hand_idle.fbx");
        AnimationClip oneHandSlashRight = GetClip("1hand_attack_slash_right.fbx");
        AnimationClip oneHandSlashLeft = GetClip("1hand_attack_slash_left.fbx");

        Debug.Log($"[SetupWeaponAnimationSystem] 클립 로드 확인 - Idle:{standingIdle != null}, WalkFwd:{standingWalkFwd != null}, RifleFire:{rifleFire != null}, 1HandRight:{oneHandSlashRight != null}");

        // 1. Controller 구축
        AnimatorController controller = BuildBaseAnimatorController(
            standingIdle,
            standingWalkFwd,
            standingWalkBack,
            standingWalkLeft,
            standingWalkRight,
            rifleSprint,
            rifleFire,
            rifleReload,
            oneHandSlashRight,
            oneHandSlashLeft
        );

        // 2. Override Controllers 생성
        var rifleOverride = CreateRifleOverride(controller, rifleIdle, rifleFire, rifleReload);
        var shotgunOverride = CreateShotgunOverride(controller, shotgunIdle, shotgunFire, rifleReload);
        var meleeOverride = CreateOneHandMeleeOverride(controller, oneHandIdle, oneHandSlashRight, oneHandSlashLeft);

        // 3. ItemData ScriptableObjects에 연결
        AssignOverridesToItemData(rifleOverride, shotgunOverride, meleeOverride);

        // 4. PlayerDummyPrefab 프리팹에 ClientNetworkAnimator 및 Animator 검증
        SetupPlayerDummyPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SetupWeaponAnimationSystem] 무기 애니메이션 시스템 구축 완료!");
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder(OVERRIDES_DIR))
        {
            AssetDatabase.CreateFolder("Assets/Animations", "Overrides");
        }
    }

    private static void ConfigureFBXImportSettings()
    {
        // 반복 재생(Loop)되어야 하는 FBX 파일 목록
        var loopFbxSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "standing_idle.fbx",
            "standing_walk_forward.fbx",
            "standing_walk_back.fbx",
            "standing_walk_left.fbx",
            "standing_walk_right(1).fbx",
            "standing_walk_right(2).fbx",
            "rifle_sprint.fbx",
            "rifle_idle_1.fbx",
            "shotgun_aim_idle.fbx",
            "1hand_idle.fbx"
        };

        // 전체 대상 무기 애니메이션 FBX 파일 목록 (루트 모션 완전 제거 및 인플레이스 포즈 베이크)
        string[] allWeaponFbxFiles = new string[]
        {
            "standing_idle.fbx",
            "standing_walk_forward.fbx",
            "standing_walk_back.fbx",
            "standing_walk_left.fbx",
            "standing_walk_right(1).fbx",
            "standing_walk_right(2).fbx",
            "rifle_sprint.fbx",
            "rifle_idle_1.fbx",
            "rifle_fire_single.fbx",
            "rifle_walk_reload.fbx",
            "shotgun_aim_idle.fbx",
            "shotgun_fire_pump.fbx",
            "1hand_idle.fbx",
            "1hand_attack_slash_right.fbx",
            "1hand_attack_slash_left.fbx"
        };

        foreach (string fileName in allWeaponFbxFiles)
        {
            string path = $"{WEAPONS_ANIM_DIR}/{fileName}";
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            bool isLoopTarget = loopFbxSet.Contains(fileName);
            bool modified = false;

            foreach (var c in clips)
            {
                // 1. 루프 설정
                if (c.loopTime != isLoopTarget)
                {
                    c.loopTime = isLoopTarget;
                    modified = true;
                }

                // 2. 루트 모션 완전 배제: Rotation, Position(Y), Position(XZ) 전부 Bake Into Pose
                if (!c.lockRootRotation)
                {
                    c.lockRootRotation = true;
                    modified = true;
                }
                if (!c.lockRootHeightY)
                {
                    c.lockRootHeightY = true;
                    modified = true;
                }
                if (!c.lockRootPositionXZ)
                {
                    c.lockRootPositionXZ = true;
                    modified = true;
                }

                if (!c.keepOriginalOrientation)
                {
                    c.keepOriginalOrientation = true;
                    modified = true;
                }
                if (!c.keepOriginalPositionXZ)
                {
                    c.keepOriginalPositionXZ = true;
                    modified = true;
                }

                // Y축(높이): 발바닥(Feet)을 지면(Y=0)에 완벽하게 스냅
                if (!c.heightFromFeet)
                {
                    c.heightFromFeet = true;
                    modified = true;
                }
                if (c.keepOriginalPositionY)
                {
                    c.keepOriginalPositionY = false;
                    modified = true;
                }

                if (isLoopTarget && !c.loopPose)
                {
                    c.loopPose = true;
                    modified = true;
                }
            }

            if (modified)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                Debug.Log($"[SetupWeaponAnimationSystem] 루트 모션 포즈 베이크 및 발 높이 보정 완료: {fileName} (Loop: {isLoopTarget})");
            }
        }
    }

    private static AnimationClip GetClip(string fileName)
    {
        string path = $"{WEAPONS_ANIM_DIR}/{fileName}";
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in assets)
        {
            if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }

    private static AnimatorController BuildBaseAnimatorController(
        AnimationClip standingIdle,
        AnimationClip standingWalkFwd,
        AnimationClip standingWalkBack,
        AnimationClip standingWalkLeft,
        AnimationClip standingWalkRight,
        AnimationClip rifleSprint,
        AnimationClip rifleFire,
        AnimationClip rifleReload,
        AnimationClip oneHandSlashRight,
        AnimationClip oneHandSlashLeft)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
        }

        // 파라미터 등록
        EnsureParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "MoveY", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "IsSprinting", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "AimPitch", AnimatorControllerParameterType.Float);
        EnsureParameter(controller, "WeaponType", AnimatorControllerParameterType.Int);

        EnsureParameter(controller, "Fire", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "FireSpeed", AnimatorControllerParameterType.Float, 1.0f);

        EnsureParameter(controller, "Reload", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "ReloadSpeed", AnimatorControllerParameterType.Float, 1.0f);

        EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        EnsureParameter(controller, "AttackSide", AnimatorControllerParameterType.Int, 0);
        EnsureParameter(controller, "AttackSpeed", AnimatorControllerParameterType.Float, 1.0f);

        // ==========================================
        // 1. Base Layer (Locomotion)
        // ==========================================
        AnimatorControllerLayer baseLayer = controller.layers[0];
        baseLayer.name = "Base Layer";
        AnimatorStateMachine baseStateMachine = baseLayer.stateMachine;

        // 기존 스테이트 정리
        ClearStateMachine(baseStateMachine);

        // 2D Blend Tree 생성
        BlendTree blendTree;
        AnimatorState locomotionState = controller.CreateBlendTreeInController("Locomotion", out blendTree, 0);
        locomotionState.writeDefaultValues = true;
        blendTree.blendType = BlendTreeType.SimpleDirectional2D;
        blendTree.blendParameter = "MoveX";
        blendTree.blendParameterY = "MoveY";

        if (standingIdle != null) blendTree.AddChild(standingIdle, new Vector2(0f, 0f));
        if (standingWalkFwd != null) blendTree.AddChild(standingWalkFwd, new Vector2(0f, 1f));
        if (standingWalkBack != null) blendTree.AddChild(standingWalkBack, new Vector2(0f, -1f));
        if (standingWalkLeft != null) blendTree.AddChild(standingWalkLeft, new Vector2(-1f, 0f));
        if (standingWalkRight != null) blendTree.AddChild(standingWalkRight, new Vector2(1f, 0f));

        baseStateMachine.defaultState = locomotionState;

        // Sprint State
        AnimatorState sprintState = baseStateMachine.AddState("Sprint", new Vector3(300, 200, 0));
        sprintState.motion = rifleSprint != null ? rifleSprint : standingWalkFwd;
        sprintState.writeDefaultValues = true;

        // Transitions: Locomotion <-> Sprint
        var toSprint = locomotionState.AddTransition(sprintState);
        toSprint.AddCondition(AnimatorConditionMode.If, 0, "IsSprinting");
        toSprint.hasExitTime = false;
        toSprint.duration = 0.15f;

        var toLoco = sprintState.AddTransition(locomotionState);
        toLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");
        toLoco.hasExitTime = false;
        toLoco.duration = 0.15f;

        // ==========================================
        // 2. UpperBody Layer (Action Layer)
        // ==========================================
        AvatarMask upperMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASK_PATH);
        AnimatorControllerLayer upperLayer;
        if (controller.layers.Length < 2)
        {
            controller.AddLayer("UpperBody");
        }

        // 레이어 참조 갱신
        var layers = controller.layers;
        upperLayer = layers[1];
        upperLayer.name = "UpperBody";
        upperLayer.avatarMask = upperMask;
        upperLayer.defaultWeight = 1.0f;
        upperLayer.iKPass = true;
        layers[1] = upperLayer;
        controller.layers = layers;

        AnimatorStateMachine upperStateMachine = upperLayer.stateMachine;
        ClearStateMachine(upperStateMachine);

        // Upper_Idle (Default)
        AnimatorState upperIdle = upperStateMachine.AddState("Upper_Idle", new Vector3(300, 50, 0));
        upperIdle.motion = standingIdle;
        upperIdle.writeDefaultValues = true;
        upperStateMachine.defaultState = upperIdle;

        // Upper_Fire
        AnimatorState upperFire = upperStateMachine.AddState("Upper_Fire", new Vector3(550, -50, 0));
        upperFire.motion = rifleFire;
        upperFire.speedParameterActive = true;
        upperFire.speedParameter = "FireSpeed";
        upperFire.writeDefaultValues = true;

        var anyToFire = upperStateMachine.AddAnyStateTransition(upperFire);
        anyToFire.AddCondition(AnimatorConditionMode.If, 0, "Fire");
        anyToFire.hasExitTime = false;
        anyToFire.duration = 0.05f;
        anyToFire.canTransitionToSelf = true;

        var fireToIdle = upperFire.AddTransition(upperIdle);
        fireToIdle.hasExitTime = true;
        fireToIdle.exitTime = 0.8f;
        fireToIdle.duration = 0.1f;

        // Upper_Reload
        AnimatorState upperReload = upperStateMachine.AddState("Upper_Reload", new Vector3(550, 50, 0));
        upperReload.motion = rifleReload;
        upperReload.speedParameterActive = true;
        upperReload.speedParameter = "ReloadSpeed";
        upperReload.writeDefaultValues = true;

        var anyToReload = upperStateMachine.AddAnyStateTransition(upperReload);
        anyToReload.AddCondition(AnimatorConditionMode.If, 0, "Reload");
        anyToReload.hasExitTime = false;
        anyToReload.duration = 0.1f;
        anyToReload.canTransitionToSelf = false;

        var reloadToIdle = upperReload.AddTransition(upperIdle);
        reloadToIdle.hasExitTime = true;
        reloadToIdle.exitTime = 0.95f;
        reloadToIdle.duration = 0.15f;

        // Upper_Attack_Right (AttackSide == 0)
        AnimatorState upperAttackRight = upperStateMachine.AddState("Upper_Attack_Right", new Vector3(550, 150, 0));
        upperAttackRight.motion = oneHandSlashRight;
        upperAttackRight.speedParameterActive = true;
        upperAttackRight.speedParameter = "AttackSpeed";
        upperAttackRight.writeDefaultValues = true;

        var anyToAttackR = upperStateMachine.AddAnyStateTransition(upperAttackRight);
        anyToAttackR.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttackR.AddCondition(AnimatorConditionMode.Equals, 0, "AttackSide");
        anyToAttackR.hasExitTime = false;
        anyToAttackR.duration = 0.05f;
        anyToAttackR.canTransitionToSelf = true;

        var attackRToIdle = upperAttackRight.AddTransition(upperIdle);
        attackRToIdle.hasExitTime = true;
        attackRToIdle.exitTime = 0.8f;
        attackRToIdle.duration = 0.1f;

        // Upper_Attack_Left (AttackSide == 1)
        AnimatorState upperAttackLeft = upperStateMachine.AddState("Upper_Attack_Left", new Vector3(550, 250, 0));
        upperAttackLeft.motion = oneHandSlashLeft != null ? oneHandSlashLeft : oneHandSlashRight;
        upperAttackLeft.speedParameterActive = true;
        upperAttackLeft.speedParameter = "AttackSpeed";
        upperAttackLeft.writeDefaultValues = true;

        var anyToAttackL = upperStateMachine.AddAnyStateTransition(upperAttackLeft);
        anyToAttackL.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttackL.AddCondition(AnimatorConditionMode.Equals, 1, "AttackSide");
        anyToAttackL.hasExitTime = false;
        anyToAttackL.duration = 0.05f;
        anyToAttackL.canTransitionToSelf = true;

        var attackLToIdle = upperAttackLeft.AddTransition(upperIdle);
        attackLToIdle.hasExitTime = true;
        attackLToIdle.exitTime = 0.8f;
        attackLToIdle.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ClearStateMachine(AnimatorStateMachine sm)
    {
        foreach (var trans in sm.anyStateTransitions)
        {
            sm.RemoveAnyStateTransition(trans);
        }
        foreach (var cs in sm.states)
        {
            sm.RemoveState(cs.state);
        }
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type, float defaultFloat = 0f)
    {
        foreach (var p in controller.parameters)
        {
            if (p.name == name) return;
        }

        var param = new AnimatorControllerParameter
        {
            name = name,
            type = type,
            defaultFloat = defaultFloat
        };
        controller.AddParameter(param);
    }

    private static AnimatorOverrideController CreateRifleOverride(
        AnimatorController baseController,
        AnimationClip idle,
        AnimationClip fire,
        AnimationClip reload)
    {
        string path = $"{OVERRIDES_DIR}/Rifle_Override.overrideController";
        var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(overrideController, path);
        }
        else
        {
            overrideController.runtimeAnimatorController = baseController;
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            string originalName = overrides[i].Key.name;
            if (originalName.Contains("standing_idle") || originalName.Contains("Upper_Idle"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, idle);
            }
            else if (originalName.Contains("rifle_fire") || originalName.Contains("Upper_Fire"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, fire);
            }
            else if (originalName.Contains("rifle_walk_reload") || originalName.Contains("Upper_Reload"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, reload);
            }
        }

        overrideController.ApplyOverrides(overrides);
        EditorUtility.SetDirty(overrideController);
        return overrideController;
    }

    private static AnimatorOverrideController CreateShotgunOverride(
        AnimatorController baseController,
        AnimationClip idle,
        AnimationClip fire,
        AnimationClip reload)
    {
        string path = $"{OVERRIDES_DIR}/Shotgun_Override.overrideController";
        var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(overrideController, path);
        }
        else
        {
            overrideController.runtimeAnimatorController = baseController;
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            string originalName = overrides[i].Key.name;
            if (originalName.Contains("standing_idle") || originalName.Contains("Upper_Idle"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, idle);
            }
            else if (originalName.Contains("rifle_fire") || originalName.Contains("Upper_Fire"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, fire);
            }
            else if (originalName.Contains("rifle_walk_reload") || originalName.Contains("Upper_Reload"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, reload);
            }
        }

        overrideController.ApplyOverrides(overrides);
        EditorUtility.SetDirty(overrideController);
        return overrideController;
    }

    private static AnimatorOverrideController CreateOneHandMeleeOverride(
        AnimatorController baseController,
        AnimationClip idle,
        AnimationClip slashRight,
        AnimationClip slashLeft)
    {
        string path = $"{OVERRIDES_DIR}/OneHandMelee_Override.overrideController";
        var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(overrideController, path);
        }
        else
        {
            overrideController.runtimeAnimatorController = baseController;
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            string originalName = overrides[i].Key.name;
            if (originalName.Contains("standing_idle") || originalName.Contains("Upper_Idle"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, idle);
            }
            else if (originalName.Contains("1hand_attack_slash_right") || originalName.Contains("Upper_Attack_Right"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, slashRight);
            }
            else if (originalName.Contains("1hand_attack_slash_left") || originalName.Contains("Upper_Attack_Left"))
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, slashLeft);
            }
        }

        overrideController.ApplyOverrides(overrides);
        EditorUtility.SetDirty(overrideController);
        return overrideController;
    }

    private static void AssignOverridesToItemData(
        AnimatorOverrideController rifleOverride,
        AnimatorOverrideController shotgunOverride,
        AnimatorOverrideController meleeOverride)
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemData", new string[] { "Assets/Resources/ItemData" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (itemData == null) continue;

            if (itemData is GunItemData gun)
            {
                if (gun.IsShotgun)
                {
                    itemData.SetAnimatorOverride(shotgunOverride);
                    Debug.Log($"[SetupWeaponAnimationSystem] ItemData 바인딩: {itemData.name} -> Shotgun_Override");
                }
                else
                {
                    itemData.SetAnimatorOverride(rifleOverride);
                    Debug.Log($"[SetupWeaponAnimationSystem] ItemData 바인딩: {itemData.name} -> Rifle_Override");
                }
                EditorUtility.SetDirty(itemData);
            }
            else if (itemData is MeleeItemData || itemData.ActionType == ItemActionType.MeleeWeapon)
            {
                itemData.SetAnimatorOverride(meleeOverride);
                Debug.Log($"[SetupWeaponAnimationSystem] ItemData 바인딩: {itemData.name} -> OneHandMelee_Override");
                EditorUtility.SetDirty(itemData);
            }
        }
    }

    private static void SetupPlayerDummyPrefab(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        if (root == null)
        {
            Debug.LogError($"[SetupWeaponAnimationSystem] 프리팹을 로드할 수 없습니다: {PREFAB_PATH}");
            return;
        }

        Animator animator = root.GetComponent<Animator>();
        if (animator == null)
        {
            animator = root.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        CharacterController cc = root.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.skinWidth = 0.08f;
        }

        ClientNetworkAnimator netAnimator = root.GetComponent<ClientNetworkAnimator>();
        if (netAnimator == null)
        {
            netAnimator = root.AddComponent<ClientNetworkAnimator>();
        }

        // SerializedObject를 통해 Animator 필드 및 Animator 파라미터 동기화
        SerializedObject so = new SerializedObject(netAnimator);
        SerializedProperty animProp = so.FindProperty("m_Animator");
        if (animProp != null)
        {
            animProp.objectReferenceValue = animator;
        }
        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("[SetupWeaponAnimationSystem] PlayerDummyPrefab에 ClientNetworkAnimator 및 Animator 설정 완료!");
    }
}
