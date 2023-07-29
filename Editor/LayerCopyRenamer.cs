using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace Air.LayerCopy
{
    // Renames parameters using the function set to renameFunction.
    public class ParameterRenamer
    {
        // Function used to rename parameters, returns a new names based on an input name.
        public System.Func<string, string> renameFunction;

        // Tracks parameters renamed by the renameFunction.
        private Dictionary<string, string> renamedParameterNames = new Dictionary<string, string>();

        // Process parameter callback to be passed in LayerCopy.Copy.
        // Passes parameter names to renameFunction to perform renaming.
        public AnimatorControllerParameter PreProcessParameter(in AnimatorControllerParameter parameter)
        {
            string nName = renameFunction(parameter.name);
            if (nName != parameter.name)
            {
                renamedParameterNames.Add(parameter.name, nName);

                AnimatorControllerParameter nParam = new AnimatorControllerParameter
                {
                    name = nName,
                    type = parameter.type
                };

                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Trigger:
                    case AnimatorControllerParameterType.Bool:
                        nParam.defaultBool = parameter.defaultBool;
                        break;
                    case AnimatorControllerParameterType.Int:
                        nParam.defaultInt = parameter.defaultInt;
                        break;
                    case AnimatorControllerParameterType.Float:
                        nParam.defaultFloat = parameter.defaultFloat;
                        break;
                    default:
                        break;
                }
                return nParam;
            }
            return null;
        }

        // Process state callback to be passed in LayerCopy.Copy.
        // Updates state paremeters to match new parameter names.
        public void PostprocessState(AnimatorState state)
        {
            if (renamedParameterNames.ContainsKey(state.cycleOffsetParameter))
                state.cycleOffsetParameter = renamedParameterNames[state.cycleOffsetParameter];

            if (renamedParameterNames.ContainsKey(state.mirrorParameter))
                state.mirrorParameter = renamedParameterNames[state.mirrorParameter];

            if (renamedParameterNames.ContainsKey(state.speedParameter))
                state.speedParameter = renamedParameterNames[state.speedParameter];

            if (renamedParameterNames.ContainsKey(state.timeParameter))
                state.timeParameter = renamedParameterNames[state.timeParameter];
        }

        // Process transitions callback to be passed in LayerCopy.Copy.
        // Updates transitions to match new parameter names.
        public void PostProcessTransitions(AnimatorTransitionBase[] transitions,
            System.Func<AnimatorState, AnimatorTransitionBase> addTransitionToState, System.Func<AnimatorStateMachine, AnimatorTransitionBase> addTransitionToMachine,
            System.Func<AnimatorTransitionBase> addTransitionToExit, System.Action<AnimatorTransitionBase> removeTransition, System.Action<AnimatorTransitionBase, AnimatorTransitionBase> copyTransition)
        {
            foreach (AnimatorTransitionBase tranistion in transitions)
            {
                foreach (AnimatorCondition condition in tranistion.conditions)
                {
                    if (renamedParameterNames.ContainsKey(condition.parameter))
                    {
                        tranistion.RemoveCondition(condition);
                        tranistion.AddCondition(condition.mode, condition.threshold, renamedParameterNames[condition.parameter]);
                    }
                }
            }
        }

        // Process blend tree callback to be passed in LayerCopy.Copy.
        // Updates blend tree parameters to match new parameter names.
        public void PostProcessBlendTree(ref bool externalAsset, ref BlendTree blendTree, System.Action<BlendTree> prepareExternalForEmbed)
        {
            if (externalAsset)
                throw new System.NotImplementedException("Modifications to external blendtree required copying the asset file");

            if (renamedParameterNames.ContainsKey(blendTree.blendParameter))
                blendTree.blendParameter = renamedParameterNames[blendTree.blendParameter];

            if (renamedParameterNames.ContainsKey(blendTree.blendParameterY))
                blendTree.blendParameterY = renamedParameterNames[blendTree.blendParameterY];

            ChildMotion[] motions = blendTree.children; //returns copy

            for (int i = 0; i < motions.Length; i++)
            {
                if (renamedParameterNames.ContainsKey(motions[i].directBlendParameter))
                    motions[i].directBlendParameter = renamedParameterNames[motions[i].directBlendParameter];
            }

            blendTree.children = motions;
        }

        // Process state machine behaviour callback to be passed in LayerCopy.Copy.
        // Updates behaviour parameters to match new parameter names.
        public void PostProcessStateMachineBehaviour(StateMachineBehaviour behaviour)
        {
            switch (behaviour)
            {
#if VRC_SDK_VRCSDK3
                case VRC.SDK3.Avatars.Components.VRCAnimatorLayerControl animLayerControl:
                case VRC.SDK3.Avatars.Components.VRCAnimatorLocomotionControl locControl:
                case VRC.SDK3.Avatars.Components.VRCAnimatorTemporaryPoseSpace tempPoseSpace:
                case VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl trackControl:
                case VRC.SDK3.Avatars.Components.VRCPlayableLayerControl playLayerControl:
                    break;
                case VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver paramDriver:
                    foreach (var param in paramDriver.parameters)
                    {
                        if (renamedParameterNames.ContainsKey(param.name))
                            param.name = renamedParameterNames[param.name];
                        if (renamedParameterNames.ContainsKey(param.source))
                            param.source = renamedParameterNames[param.source];
                    }
                    break;
#endif
                default:
                    Debug.LogWarning("Unkown StateMachineBehaviour Found");
                    break;
            }
        }
    }
}
