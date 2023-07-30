using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Air.LayerCopy
{
    public static class LayerCopy
    {
        /// <summary>
        /// Copies selected layers from parSrcAnimator to parDstAnimator.
        /// </summary>
        /// <param name="parSrcAnimator">Source animator controller</param>
        /// <param name="parDstAnimator">Destination animator controller</param>
        /// <param name="processor"> Can be null.
        /// Class to modify the copied animator. See CopyProcessor
        /// </param>
        public static void Copy(AnimatorController parSrcAnimator, AnimatorController parDstAnimator, Dictionary<string, bool> selectedLayers, CopyProcessor processor = null)
        {
            if (parSrcAnimator == null | parDstAnimator == null)
                return;

            List<string> usedParameterNames = new List<string>();
            List<string> usedLayerNames = new List<string>();

            //Find Used Params
            foreach (AnimatorControllerLayer layer in parSrcAnimator.layers)
            {
                if (!selectedLayers[layer.name])
                    continue;

                CollectParameters(usedParameterNames, usedLayerNames, layer.stateMachine);

                //Also check for synced layers
                if (layer.syncedLayerIndex != -1)
                    usedLayerNames.Add(parSrcAnimator.layers[layer.syncedLayerIndex].name);
            }

            //Check params
            List<AnimatorControllerParameter> neededParameters = new List<AnimatorControllerParameter>();
            foreach (string paramName in usedParameterNames)
            {
                //Find Param in src
                AnimatorControllerParameter srcParam = null;
                foreach (AnimatorControllerParameter p in parSrcAnimator.parameters)
                {
                    if (p.name == paramName)
                    {
                        srcParam = p;
                        break;
                    }
                }
                if (srcParam == null)
                {
                    Debug.LogWarning($"Used Parameter \"{paramName}\" not found in Source Animator!?!");
                    continue;
                }

                //Preprocess
                AnimatorControllerParameter processedParam = processor?.ParameterPreProcess(srcParam);
                if (processedParam != null)
                    srcParam = processedParam;

                //Find Param in dst
                AnimatorControllerParameter dstParam = null;
                foreach (AnimatorControllerParameter p in parDstAnimator.parameters)
                {
                    if (p.name == srcParam.name)
                    {
                        dstParam = p;
                        break;
                    }
                }

                //Check DstParam
                if (dstParam == null)
                    neededParameters.Add(srcParam);
                else if (dstParam.type == srcParam.type)
                {
                    //Check Default values, log if different
                    switch (dstParam.type)
                    {
                        case AnimatorControllerParameterType.Trigger:
                        case AnimatorControllerParameterType.Bool:
                            if (dstParam.defaultBool != srcParam.defaultBool)
                                Debug.LogWarning($"Paramter \"{srcParam.name}\" has differing default values, using destination value");
                            break;
                        case AnimatorControllerParameterType.Int:
                            if (dstParam.defaultInt != srcParam.defaultInt)
                                Debug.LogWarning($"Paramter \"{srcParam.name}\" has differing default values, using destination value");
                            break;
                        case AnimatorControllerParameterType.Float:
                            if (dstParam.defaultFloat != srcParam.defaultFloat)
                                Debug.LogWarning($"Paramter \"{srcParam.name}\" has differing default values, using destination value");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    Debug.LogError($"Parameter \"{srcParam.name}\" exists in destination animator, but with different type");
                    return;
                }
            }

            //Check layers
            foreach (string layerName in usedLayerNames)
            {
                if (!selectedLayers[layerName])
                {
                    Debug.LogError("A layer is required, but not selected (target of synced layer?)");
                    return;
                }
            }

            //Copy Params
            foreach (AnimatorControllerParameter param in neededParameters)
            {
                AnimatorControllerParameter nParam = new AnimatorControllerParameter
                {
                    name = param.name,
                    type = param.type
                };

                switch (param.type)
                {
                    case AnimatorControllerParameterType.Trigger:
                    case AnimatorControllerParameterType.Bool:
                        nParam.defaultBool = param.defaultBool;
                        break;
                    case AnimatorControllerParameterType.Int:
                        nParam.defaultInt = param.defaultInt;
                        break;
                    case AnimatorControllerParameterType.Float:
                        nParam.defaultFloat = param.defaultFloat;
                        break;
                    default:
                        break;
                }

                parDstAnimator.AddParameter(nParam);
            }

            processor?.ParameterListInspectFinal(parDstAnimator.parameters);

            //Note that the layers class is recreated in AnimatorController.layers, and thus won't be equal to a second instance gotten from the second array
            Dictionary<AnimatorControllerLayer, AnimatorControllerLayer> layerMapping = new Dictionary<AnimatorControllerLayer, AnimatorControllerLayer>();

            //Copy Layer
            AnimatorControllerLayer[] srcLayers = parSrcAnimator.layers;
            List<AnimatorControllerLayer> newLayers = new List<AnimatorControllerLayer>();
            foreach (AnimatorControllerLayer layer in srcLayers)
            {
                if (!selectedLayers[layer.name])
                    continue;

                AnimatorControllerLayer newLayer = new AnimatorControllerLayer
                {
                    avatarMask = layer.avatarMask,
                    blendingMode = layer.blendingMode,
                    defaultWeight = srcLayers[0] == layer ? 1f : layer.defaultWeight,
                    iKPass = layer.iKPass,
                    name = parDstAnimator.MakeUniqueLayerName(layer.name),
                    stateMachine = new AnimatorStateMachine
                    {
                        name = parDstAnimator.MakeUniqueLayerName(layer.name)
                    }
                };
                AnimatorStateMachine newStateMachine = newLayer.stateMachine;

                AssetDatabase.AddObjectToAsset(newStateMachine, AssetDatabase.GetAssetPath(parDstAnimator));
                newStateMachine.hideFlags = HideFlags.HideInHierarchy;

                DeepCopy(layer.stateMachine, newStateMachine, processor);

                newLayers.Add(newLayer);
                layerMapping.Add(layer, newLayer);
            }

            //Setup Synced Layers
            foreach (AnimatorControllerLayer layer in srcLayers)
            {
                if (!selectedLayers[layer.name])
                    continue;

                if (layer.syncedLayerIndex == -1)
                    continue;

                AnimatorControllerLayer nLayer = layerMapping[layer];

                AnimatorControllerLayer oLinkedLayer = srcLayers[layer.syncedLayerIndex];
                AnimatorControllerLayer nLinkedLayer = layerMapping[oLinkedLayer];

                //Compute new Index
                int index = newLayers.IndexOf(nLinkedLayer) + parDstAnimator.layers.Length;
                //Set synced properties
                nLayer.syncedLayerIndex = index;
                nLayer.syncedLayerAffectsTiming = layer.syncedLayerAffectsTiming;

                //Set Layer overrides
                Dictionary<AnimatorState, AnimatorState> stateMapping = new Dictionary<AnimatorState, AnimatorState>();
                BuildStateMapping(stateMapping, oLinkedLayer.stateMachine, nLinkedLayer.stateMachine);

                foreach (KeyValuePair<AnimatorState, AnimatorState> kv in stateMapping)
                {
                    Motion oMotion = layer.GetOverrideMotion(kv.Key);
                    Motion nMotion = DeepCopyMotion(oLinkedLayer.stateMachine, nLinkedLayer.stateMachine, oMotion, processor);

                    nLayer.SetOverrideMotion(kv.Value, nMotion);

                    //TODO: behaviours
                    nLayer.SetOverrideBehaviours(kv.Value, null);
                }
            }

            //Add layers
            foreach (AnimatorControllerLayer layer in newLayers)
                parDstAnimator.AddLayer(layer);

            EditorUtility.SetDirty(parDstAnimator);
            AssetDatabase.SaveAssets();
            //Hope it works
        }

        private static void CollectParameters(List<string> paramList, List<string> layerList, AnimatorStateMachine stateMachine)
        {
            //AnyState Transitions
            InspectTrainsitions(paramList, stateMachine.anyStateTransitions);

            //Entry Transitions
            InspectTrainsitions(paramList, stateMachine.entryTransitions);

            //Behaviours
            foreach (StateMachineBehaviour behaviour in stateMachine.behaviours)
                InspectStateBehaviour(paramList, layerList, behaviour);

            //states
            foreach (ChildAnimatorState state in stateMachine.states)
            {
                //State Parameters
                if (state.state.cycleOffsetParameterActive)
                    if (!paramList.Contains(state.state.cycleOffsetParameter))
                        paramList.Add(state.state.cycleOffsetParameter);

                if (state.state.mirrorParameterActive)
                    if (!paramList.Contains(state.state.mirrorParameter))
                        paramList.Add(state.state.mirrorParameter);

                if (state.state.speedParameterActive)
                    if (!paramList.Contains(state.state.speedParameter))
                        paramList.Add(state.state.speedParameter);

                if (state.state.timeParameterActive)
                    if (!paramList.Contains(state.state.timeParameter))
                        paramList.Add(state.state.timeParameter);

                foreach (StateMachineBehaviour behaviour in state.state.behaviours)
                    InspectStateBehaviour(paramList, layerList, behaviour);

                //Blend Trees
                InspectMotion(paramList, state.state.motion);

                //Trainsitions
                InspectTrainsitions(paramList, state.state.transitions);
            }

            //Child StateMachines
            foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
            {
                CollectParameters(paramList, layerList, childStateMachine.stateMachine);

                //Trainsitions
                InspectTrainsitions(paramList, stateMachine.GetStateMachineTransitions(childStateMachine.stateMachine));
            }
        }

        private static void InspectTrainsitions(List<string> paramList, AnimatorTransitionBase[] transitions)
        {
            foreach (AnimatorTransitionBase transition in transitions)
            {
                foreach (AnimatorCondition condition in transition.conditions)
                {
                    if (!paramList.Contains(condition.parameter))
                        paramList.Add(condition.parameter);
                }
            }
        }

        private static void InspectMotion(List<string> paramList, Motion motion)
        {
            if (motion is BlendTree)
                InspectBlendTree(paramList, motion as BlendTree);
        }

        private static void InspectBlendTree(List<string> paramList, BlendTree tree)
        {
            switch (tree.blendType)
            {
                case BlendTreeType.Direct:
                    for (int i = 0; i < tree.children.Length; i++)
                        if (!paramList.Contains(tree.children[i].directBlendParameter))
                            paramList.Add(tree.children[i].directBlendParameter);
                    break;
                case BlendTreeType.Simple1D:
                    if (!paramList.Contains(tree.blendParameter))
                        paramList.Add(tree.blendParameter);
                    break;
                case BlendTreeType.FreeformCartesian2D:
                case BlendTreeType.FreeformDirectional2D:
                case BlendTreeType.SimpleDirectional2D:
                    if (!paramList.Contains(tree.blendParameter))
                        paramList.Add(tree.blendParameter);

                    if (!paramList.Contains(tree.blendParameterY))
                        paramList.Add(tree.blendParameterY);

                    break;
            }

            for (int i = 0; i < tree.children.Length; i++)
                InspectMotion(paramList, tree.children[i].motion);
        }

        private static void InspectStateBehaviour(List<string> paramList, List<string> layerList, StateMachineBehaviour behaviour)
        {
            switch (behaviour)
            {
#if VRC_SDK_VRCSDK3
                case VRC.SDK3.Avatars.Components.VRCAnimatorLayerControl animLayerControl:
                    //This can set layers outside of the src animatior
                    //so ignore and log a warning
                    Debug.LogWarning("VRCAnimatorLayerControl Found, If it controlling a layer from the source animatior, then the Layer index will need to be updated manually");
                    break;
                case VRC.SDK3.Avatars.Components.VRCAnimatorLocomotionControl locControl:
                case VRC.SDK3.Avatars.Components.VRCAnimatorTemporaryPoseSpace tempPoseSpace:
                case VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl trackControl:
                    break;
                case VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver paramDriver:
                    foreach (var param in paramDriver.parameters)
                    {
                        if (!paramList.Contains(param.name))
                            paramList.Add(param.name);
                        if (param.type == VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy)
                        {
                            if (!paramList.Contains(param.name))
                                paramList.Add(param.name);
                        }
                    }
                    break;
                case VRC.SDK3.Avatars.Components.VRCPlayableLayerControl playLayerControl:
                    break;
#endif
                default:
                    Debug.LogWarning("Unkown StateMachineBehaviour Found");
                    break;
            }
        }

        private static void DeepCopy(AnimatorStateMachine srcStateMachine, AnimatorStateMachine dstStateMachine, CopyProcessor processor)
        {
            Dictionary<AnimatorState, AnimatorState> stateMapping = new Dictionary<AnimatorState, AnimatorState>();
            Dictionary<AnimatorStateMachine, AnimatorStateMachine> machineMapping = new Dictionary<AnimatorStateMachine, AnimatorStateMachine>();

            //Position Default nodes
            dstStateMachine.entryPosition = srcStateMachine.entryPosition;
            dstStateMachine.anyStatePosition = srcStateMachine.anyStatePosition;
            dstStateMachine.exitPosition = srcStateMachine.exitPosition;
            dstStateMachine.parentStateMachinePosition = srcStateMachine.parentStateMachinePosition;

            //Copy Behaviours
            foreach (StateMachineBehaviour oBehaviour in srcStateMachine.behaviours)
            {
                System.Type oType = oBehaviour.GetType();
                System.Type nType = null;
                nType = processor?.RemapStateMachineBehaviourType(oType);

                StateMachineBehaviour nBehaviour;
                if (nType == null || oType == nType)
                {
                    nBehaviour = dstStateMachine.AddStateMachineBehaviour(oType);
                    CopyStateBehaviour(oBehaviour, nBehaviour);
                }
                else
                {
                    nBehaviour = dstStateMachine.AddStateMachineBehaviour(nType);
                    processor.RemapStateMachineBehaviourCopy(oBehaviour, nBehaviour);
                }

                processor?.StateMachineBehaviourPostProcess(nBehaviour);
            }

            //Copy States
            foreach (ChildAnimatorState state in srcStateMachine.states)
            {
                AnimatorState nState = dstStateMachine.AddState(state.state.name, state.position);
                AnimatorState oState = state.state;

                DeepCopyState(srcStateMachine, dstStateMachine, oState, nState, processor);

                //Check if default state
                if (srcStateMachine.defaultState == oState)
                    dstStateMachine.defaultState = nState;

                //Post process
                processor?.StatePostProcess(nState);

                stateMapping.Add(oState, nState);
            }

            //Copy Statemachines
            foreach (ChildAnimatorStateMachine machine in srcStateMachine.stateMachines)
            {
                AnimatorStateMachine nMachine = dstStateMachine.AddStateMachine(machine.stateMachine.name, machine.position);
                AnimatorStateMachine oMachine = machine.stateMachine;

                DeepCopy(oMachine, nMachine, processor);

                machineMapping.Add(oMachine, nMachine);
            }

            //Copy Entry Transitions
            foreach (AnimatorTransition oTransition in srcStateMachine.entryTransitions)
            {
                AnimatorTransition nTransition;
                if (oTransition.destinationState != null)
                    nTransition = dstStateMachine.AddEntryTransition(stateMapping[oTransition.destinationState]);
                else
                    nTransition = dstStateMachine.AddEntryTransition(machineMapping[oTransition.destinationStateMachine]);

                CopyTransition(oTransition, nTransition);
            }
            //PostProcess
            processor?.TransitionPostProcess(dstStateMachine.entryTransitions, dstStateMachine.AddEntryTransition, dstStateMachine.AddEntryTransition, null,
                x => dstStateMachine.RemoveEntryTransition((AnimatorTransition)x), (s, d) => CopyTransition((AnimatorTransition)s, (AnimatorTransition)d));


            //Copy AnyState Transitions
            foreach (AnimatorStateTransition oTransition in srcStateMachine.anyStateTransitions)
            {
                AnimatorStateTransition nTransition;
                if (oTransition.destinationState != null)
                    nTransition = dstStateMachine.AddAnyStateTransition(stateMapping[oTransition.destinationState]);
                else
                    nTransition = dstStateMachine.AddAnyStateTransition(machineMapping[oTransition.destinationStateMachine]);

                CopyStateTransition(oTransition, nTransition);
            }
            //PostProcess
            processor?.TransitionPostProcess(dstStateMachine.anyStateTransitions, dstStateMachine.AddAnyStateTransition, dstStateMachine.AddAnyStateTransition, null,
                x => dstStateMachine.RemoveAnyStateTransition((AnimatorStateTransition)x), (s, d) => CopyStateTransition((AnimatorStateTransition)s, (AnimatorStateTransition)d));

            //State Transitions
            foreach (ChildAnimatorState state in srcStateMachine.states)
            {
                AnimatorState nState = stateMapping[state.state];
                AnimatorState oState = state.state;

                foreach (AnimatorStateTransition oTransition in oState.transitions)
                {
                    AnimatorStateTransition nTransition;
                    if (oTransition.destinationState != null)
                        nTransition = nState.AddTransition(stateMapping[oTransition.destinationState]);
                    else if (oTransition.destinationStateMachine != null)
                        nTransition = nState.AddTransition(machineMapping[oTransition.destinationStateMachine]);
                    else if (oTransition.isExit)
                        nTransition = nState.AddExitTransition();
                    else
                        throw new System.NotSupportedException("Unkown State Transition type");

                    CopyStateTransition(oTransition, nTransition);
                }
                //PostProcess
                processor?.TransitionPostProcess(nState.transitions, nState.AddTransition, nState.AddTransition, nState.AddExitTransition,
                    x => nState.RemoveTransition((AnimatorStateTransition)x), (s, d) => CopyStateTransition((AnimatorStateTransition)s, (AnimatorStateTransition)d));
            }

            //StateMachine Transitions
            foreach (ChildAnimatorStateMachine machine in srcStateMachine.stateMachines)
            {
                AnimatorStateMachine nMachine = machineMapping[machine.stateMachine];
                AnimatorStateMachine oMachine = machine.stateMachine;

                AnimatorTransition[] transitions = srcStateMachine.GetStateMachineTransitions(oMachine);
                foreach (AnimatorTransition oTransition in transitions)
                {
                    AnimatorTransition nTransition;
                    if (oTransition.destinationState != null)
                        nTransition = dstStateMachine.AddStateMachineTransition(nMachine, stateMapping[oTransition.destinationState]);
                    else if (oTransition.destinationStateMachine != null)
                        nTransition = dstStateMachine.AddStateMachineTransition(nMachine, machineMapping[oTransition.destinationStateMachine]);
                    else if (oTransition.isExit)
                        nTransition = dstStateMachine.AddStateMachineExitTransition(nMachine);
                    else
                        throw new System.NotSupportedException("Unkown StateMachine Transition type");

                    CopyTransition(oTransition, nTransition);
                }
                //PostProcess
                processor?.TransitionPostProcess(dstStateMachine.GetStateMachineTransitions(nMachine),
                    x => dstStateMachine.AddStateMachineTransition(nMachine, x), x => dstStateMachine.AddStateMachineTransition(nMachine, x), () => dstStateMachine.AddStateMachineExitTransition(nMachine),
                    x => dstStateMachine.RemoveStateMachineTransition(nMachine, (AnimatorTransition)x), (s, d) => CopyTransition((AnimatorTransition)s, (AnimatorTransition)d));
            }
        }

        static void DeepCopyState(AnimatorStateMachine srcStateMachine, AnimatorStateMachine dstStateMachine, AnimatorState srcState, AnimatorState dstState, CopyProcessor processor)
        {
            foreach (StateMachineBehaviour oBehaviour in srcState.behaviours)
            {
                System.Type oType = oBehaviour.GetType();
                System.Type nType = null;
                nType = processor?.RemapStateMachineBehaviourType(oType);

                StateMachineBehaviour nBehaviour;
                if (nType == null || oType == nType)
                {
                    nBehaviour = dstState.AddStateMachineBehaviour(oType);
                    CopyStateBehaviour(oBehaviour, nBehaviour);
                }
                else
                {
                    nBehaviour = dstState.AddStateMachineBehaviour(nType);
                    processor.RemapStateMachineBehaviourCopy(oBehaviour, nBehaviour);
                }

                processor?.StateMachineBehaviourPostProcess(nBehaviour);
            }

            dstState.motion = DeepCopyMotion(srcStateMachine, dstStateMachine, srcState.motion, processor);

            //cycleOffset
            dstState.cycleOffset = srcState.cycleOffset;
            dstState.cycleOffsetParameter = srcState.cycleOffsetParameter;
            dstState.cycleOffsetParameterActive = srcState.cycleOffsetParameterActive;

            dstState.iKOnFeet = srcState.iKOnFeet;

            //Mirror
            dstState.mirror = srcState.mirror;
            dstState.mirrorParameter = srcState.mirrorParameter;
            dstState.mirrorParameterActive = dstState.mirrorParameterActive;

            //Speed
            dstState.speed = srcState.speed;
            dstState.speedParameter = srcState.speedParameter;
            dstState.speedParameterActive = srcState.speedParameterActive;

            dstState.tag = srcState.tag;

            //Time
            dstState.timeParameter = srcState.timeParameter;
            dstState.timeParameterActive = srcState.timeParameterActive;

            dstState.writeDefaultValues = srcState.writeDefaultValues;
        }

        //AnimatorTransition adds nothing to AnimatorTransitionBase
        static void CopyTransition(AnimatorTransition srcTransition, AnimatorTransition dstTransition) => CopyTransitionBase(srcTransition, dstTransition);

        static void CopyStateTransition(AnimatorStateTransition srcTransition, AnimatorStateTransition dstTransition)
        {
            CopyTransitionBase(srcTransition, dstTransition);

            dstTransition.canTransitionToSelf = srcTransition.canTransitionToSelf;
            dstTransition.duration = srcTransition.duration;
            dstTransition.exitTime = srcTransition.exitTime;
            dstTransition.hasExitTime = srcTransition.hasExitTime;
            dstTransition.hasFixedDuration = srcTransition.hasFixedDuration;
            dstTransition.interruptionSource = srcTransition.interruptionSource;
            //isExit
            //mute
            //name
            dstTransition.offset = srcTransition.offset;
            dstTransition.orderedInterruption = srcTransition.orderedInterruption;
            //Solo
        }

        static void CopyTransitionBase(AnimatorTransitionBase srcTransition, AnimatorTransitionBase dstTransition)
        {
            dstTransition.name = srcTransition.name;
            //IsExit
            dstTransition.mute = srcTransition.mute;
            dstTransition.solo = srcTransition.solo;

            foreach (AnimatorCondition condition in srcTransition.conditions)
                dstTransition.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }

        static Motion DeepCopyMotion(AnimatorStateMachine srcStateMachine, AnimatorStateMachine dstStateMachine, Motion motion, CopyProcessor processor)
        {
            if (motion == null)
                return null;
            else if (motion is AnimationClip clip)
            {
                AnimationClip processedClip = processor?.AnimationClipPreProcess(clip);
                if (processedClip != null)
                    clip = processedClip;
                return clip;
            }
            else if (motion is BlendTree tree)
            {
                //Is path 
                bool external = AssetDatabase.GetAssetPath(srcStateMachine) != AssetDatabase.GetAssetPath(motion);

                if (!external)
                    tree = DeepCopyBlendTree(srcStateMachine, dstStateMachine, tree, processor);
                //else tree is input motion

                processor?.BlendTreePostProcess(ref external, ref tree, (x) => DeepCopyBlendTree(srcStateMachine, dstStateMachine, x, processor));

                if (!external)
                    AssetDatabase.AddObjectToAsset(tree, dstStateMachine);

                return tree;
            }
            else
            {
                Debug.LogError("Unkown Motion Type");
                return null;
            }
        }

        static BlendTree DeepCopyBlendTree(AnimatorStateMachine srcStateMachine, AnimatorStateMachine dstStateMachine, BlendTree tree, CopyProcessor processor)
        {
            BlendTree nTree = new BlendTree
            {
                blendParameter = tree.blendParameter,
                blendParameterY = tree.blendParameterY,
                blendType = tree.blendType,
                name = tree.name,
                maxThreshold = tree.maxThreshold,
                minThreshold = tree.minThreshold,
                useAutomaticThresholds = tree.useAutomaticThresholds,
                //May meed to be done after save?
                hideFlags = tree.hideFlags
            };

            ChildMotion[] motions = tree.children; //returns copy

            for (int i = 0; i < tree.children.Length; i++)
                motions[i].motion = DeepCopyMotion(srcStateMachine, dstStateMachine, motions[i].motion, processor);

            nTree.children = motions;
            return nTree;
        }

        static void CopyStateBehaviour(StateMachineBehaviour srcBehaviour, StateMachineBehaviour dstBehaviour)
        {
            System.Type type = srcBehaviour.GetType();

            //Reflection fun, may be slow
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                bool hasHideInInspector = false;
                foreach (var attributeData in field.CustomAttributes)
                    if (attributeData.AttributeType == typeof(HideInInspector))
                        hasHideInInspector = true;

                if (hasHideInInspector)
                {
                    Debug.Log($"{field.Name} Has Hide In Inspector Attribute, Skipping");
                    return;
                }

                CopyByReflection(field.GetValue(srcBehaviour), (x) => field.SetValue(dstBehaviour, x), field.FieldType);
            }
            //Apply corrections if needed?
        }

        static void CopyByReflection(object srcObject, System.Action<object> setDstObject, System.Type type)
        {
            if (type.IsArray)
                throw new System.NotImplementedException();

            if (type.IsPointer)
                throw new System.NotImplementedException();

            if (type.IsByRef)
                throw new System.NotImplementedException();

            if (type == typeof(string))
            {
                setDstObject(srcObject);
                return;
            }

            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    //Copy the list
                    ConstructorInfo constructor = type.GetConstructor(System.Array.Empty<System.Type>());
                    MethodInfo toArray = type.GetMethod("ToArray");
                    MethodInfo add = type.GetMethod("Add");

                    System.Type itemType = type.GetGenericArguments()[0];

                    object[] array = (object[])toArray.Invoke(srcObject, null);
                    object newList = constructor.Invoke(null);

                    foreach (object oObject in array)
                        CopyByReflection(oObject, (x) => add.Invoke(newList, new object[] { x }), itemType);

                    setDstObject(newList);
                    return;
                }
                else
                    throw new System.NotImplementedException();
            }

            if (type.IsClass)
            {
                if (srcObject == null)
                {
                    setDstObject(null);
                    return;
                }

                object nObject = System.Activator.CreateInstance(type);
                if (nObject == null)
                    throw new System.NotImplementedException();

                foreach (var objectField in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    bool hasHideInInspector = false;
                    foreach (var attributeData in objectField.CustomAttributes)
                        if (attributeData.AttributeType == typeof(HideInInspector))
                            hasHideInInspector = true;

                    if (hasHideInInspector)
                    {
                        Debug.Log($"{objectField.Name} Has Hide In Inspector Attribute, Skipping");
                        return;
                    }

                    CopyByReflection(objectField.GetValue(srcObject), (x) => objectField.SetValue(nObject, x), objectField.FieldType);
                }

                setDstObject(nObject);
                return;
            }

            //Assume Value type
            setDstObject(srcObject);
        }

        //Used for Synced Layers, Matches name
        static void BuildStateMapping(Dictionary<AnimatorState, AnimatorState> mapping, AnimatorStateMachine srcStateMachine, AnimatorStateMachine dstStateMachine)
        {
            foreach (ChildAnimatorState state in srcStateMachine.states)
            {
                AnimatorState oState = state.state;
                AnimatorState nState = null;
                foreach (ChildAnimatorState state2 in dstStateMachine.states)
                {
                    if (state.state.name == state2.state.name)
                    {
                        nState = state2.state;
                        break;
                    }
                }

                mapping.Add(oState, nState);
            }

            foreach (ChildAnimatorStateMachine machine in srcStateMachine.stateMachines)
            {
                AnimatorStateMachine oMachine = machine.stateMachine;
                AnimatorStateMachine nMachine = null;
                foreach (ChildAnimatorStateMachine machine2 in dstStateMachine.stateMachines)
                {
                    if (machine.stateMachine.name == machine2.stateMachine.name)
                    {
                        nMachine = machine2.stateMachine;
                        break;
                    }
                }

                BuildStateMapping(mapping, oMachine, nMachine);
            }
        }
    }
}
