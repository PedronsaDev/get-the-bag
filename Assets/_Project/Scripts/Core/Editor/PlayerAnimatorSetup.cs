using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project.Core.Editor
{
    public static class PlayerAnimatorSetup
    {
        private const string ControllerPath = "Assets/_Project/Art/Animations/Player/Player Animation Controller.controller";

        [MenuItem("Tools/Animation/Rebuild Player Animator Controller")]
        public static void RebuildPlayerAnimator()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller)
            {
                Debug.LogError($"[PlayerAnimatorSetup] Could not load controller at: {ControllerPath}");
                return;
            }

            Undo.RecordObject(controller, "Rebuild Player Animator");

            // 1. Ensure all parameters exist with correct types
            EnsureParameters(controller);

            // 2. Clear existing states and transitions on Base Layer
            var rootStateMachine = controller.layers[0].stateMachine;

            // Remove old states
            var oldStates = new List<ChildAnimatorState>(rootStateMachine.states);
            foreach (var childState in oldStates)
            {
                rootStateMachine.RemoveState(childState.state);
            }

            // Remove old any-state transitions
            var oldAnyTransitions = new List<AnimatorStateTransition>(rootStateMachine.anyStateTransitions);
            foreach (var trans in oldAnyTransitions)
            {
                rootStateMachine.RemoveAnyStateTransition(trans);
            }

            // Remove old entry transitions
            var oldEntryTransitions = new List<AnimatorTransition>(rootStateMachine.entryTransitions);
            foreach (var trans in oldEntryTransitions)
            {
                rootStateMachine.RemoveEntryTransition(trans);
            }

            // 3. Load Animation Clips
            var idleClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Idle.fbx");
            var walkFwdClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Walk/Walking.fbx");
            var walkBwdClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Walk/Walking Backwards.fbx");
            var walkLftClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Walk/Left Strafe Walk.fbx");
            var walkRgtClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Walk/Right Strafe Walk.fbx");

            var sprintClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Two Cycle Sprint.fbx");
            var slideClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Slide.fbx");

            var crouchIdleClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Crouch/Crouch Idle.fbx");
            var crouchFwdClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Crouch/Walk Crouching Forward.fbx");
            var crouchBwdClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Crouch/Walk Crouching Backward.fbx");
            var crouchLftClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Crouch/Walk Crouching Left.fbx");
            var crouchRgtClip = GetClipFromFbx("Assets/_Project/Art/Animations/Player/Crouch/Walk Crouching Right.fbx");

            // 4. Create Standing Locomotion 2D Blend Tree
            var standingState = rootStateMachine.AddState("Standing Locomotion", new Vector3(300, 100, 0));
            var standingTree = new BlendTree
            {
                name = "Standing Locomotion Tree",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "HorizontalAxis",
                blendParameterY = "VerticalAxis"
            };

            if (idleClip) standingTree.AddChild(idleClip, new Vector2(0f, 0f));
            if (walkFwdClip) standingTree.AddChild(walkFwdClip, new Vector2(0f, 1f));
            if (walkBwdClip) standingTree.AddChild(walkBwdClip, new Vector2(0f, -1f));
            if (walkLftClip) standingTree.AddChild(walkLftClip, new Vector2(-1f, 0f));
            if (walkRgtClip) standingTree.AddChild(walkRgtClip, new Vector2(1f, 0f));

            standingState.motion = standingTree;
            AssetDatabase.AddObjectToAsset(standingTree, controller);

            // 5. Create Crouch Locomotion 2D Blend Tree
            var crouchState = rootStateMachine.AddState("Crouch Locomotion", new Vector3(300, 250, 0));
            var crouchTree = new BlendTree
            {
                name = "Crouch Locomotion Tree",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "HorizontalAxis",
                blendParameterY = "VerticalAxis"
            };

            if (crouchIdleClip) crouchTree.AddChild(crouchIdleClip, new Vector2(0f, 0f));
            if (crouchFwdClip) crouchTree.AddChild(crouchFwdClip, new Vector2(0f, 1f));
            if (crouchBwdClip) crouchTree.AddChild(crouchBwdClip, new Vector2(0f, -1f));
            if (crouchLftClip) crouchTree.AddChild(crouchLftClip, new Vector2(-1f, 0f));
            if (crouchRgtClip) crouchTree.AddChild(crouchRgtClip, new Vector2(1f, 0f));

            crouchState.motion = crouchTree;
            AssetDatabase.AddObjectToAsset(crouchTree, controller);

            // 6. Create Sprinting State
            var sprintState = rootStateMachine.AddState("Sprinting", new Vector3(600, 100, 0));
            sprintState.motion = sprintClip;

            // 7. Create Sliding State
            var slideState = rootStateMachine.AddState("Sliding", new Vector3(600, 250, 0));
            slideState.motion = slideClip;

            // Set default entry state
            rootStateMachine.defaultState = standingState;

            // 8. Create Clean Transitions

            // Standing <-> Crouch
            var standToCrouch = standingState.AddTransition(crouchState);
            standToCrouch.hasExitTime = false;
            standToCrouch.duration = 0.2f;
            standToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

            var crouchToStand = crouchState.AddTransition(standingState);
            crouchToStand.hasExitTime = false;
            crouchToStand.duration = 0.2f;
            crouchToStand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

            // Standing <-> Sprinting
            var standToSprint = standingState.AddTransition(sprintState);
            standToSprint.hasExitTime = false;
            standToSprint.duration = 0.2f;
            standToSprint.AddCondition(AnimatorConditionMode.If, 0, "IsSprinting");

            var sprintToStand = sprintState.AddTransition(standingState);
            sprintToStand.hasExitTime = false;
            sprintToStand.duration = 0.25f;
            sprintToStand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");

            // Standing -> Sliding
            var standToSlide = standingState.AddTransition(slideState);
            standToSlide.hasExitTime = false;
            standToSlide.duration = 0.15f;
            standToSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");

            // Sprinting -> Sliding
            var sprintToSlide = sprintState.AddTransition(slideState);
            sprintToSlide.hasExitTime = false;
            sprintToSlide.duration = 0.15f;
            sprintToSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");

            // Crouch -> Sliding
            var crouchToSlide = crouchState.AddTransition(slideState);
            crouchToSlide.hasExitTime = false;
            crouchToSlide.duration = 0.15f;
            crouchToSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");

            // Sliding -> Standing (when slide stops and not crouching)
            var slideToStand = slideState.AddTransition(standingState);
            slideToStand.hasExitTime = false;
            slideToStand.duration = 0.2f;
            slideToStand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");
            slideToStand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

            // Sliding -> Crouch (when slide stops and crouching held)
            var slideToCrouch = slideState.AddTransition(crouchState);
            slideToCrouch.hasExitTime = false;
            slideToCrouch.duration = 0.2f;
            slideToCrouch.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");
            slideToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[PlayerAnimatorSetup] Player Animator Controller successfully rebuilt into clean 2D Blend Tree architecture!");
        }

        private static void EnsureParameters(AnimatorController controller)
        {
            EnsureParam(controller, "HorizontalAxis", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "VerticalAxis", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "MoveSpeed", AnimatorControllerParameterType.Float);
            EnsureParam(controller, "AnimationState", AnimatorControllerParameterType.Int);
            EnsureParam(controller, "LocomotionPhase", AnimatorControllerParameterType.Int);
            EnsureParam(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "IsSprinting", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "IsCrouching", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "IsSliding", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "IsDowned", AnimatorControllerParameterType.Bool);
            EnsureParam(controller, "Idle", AnimatorControllerParameterType.Bool);
        }

        private static void EnsureParam(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name)
                {
                    if (p.type != type)
                        controller.RemoveParameter(p);
                    else
                        return;
                    break;
                }
            }

            controller.AddParameter(name, type);
        }

        private static AnimationClip GetClipFromFbx(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }

            Debug.LogWarning($"[PlayerAnimatorSetup] Could not extract AnimationClip from FBX: {fbxPath}");
            return null;
        }
    }
}
