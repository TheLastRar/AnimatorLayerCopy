using System.Reflection;
using UnityEngine;
using Type = System.Type;

namespace Air.LayerCopy
{
    public class VRC2CVRDriverConverter : CopyProcessor
    {
        // As a package, we can't reference scripts in Assembly-CSharp
        // We will need to pull the type out of thin air
        Type cvrDriverType;
        FieldInfo cvrEnterTaskList;
        MethodInfo cvrEnterTaskAdd;

        Type cvrDriverTaskType;
        Type cvrParamType;
        Type cvrOperator;
        Type cvrSourceType;
        FieldInfo taskTargetType;
        FieldInfo taskTargetName;
        FieldInfo taskOp;
        FieldInfo taskAType;
        FieldInfo taskAValue;
        FieldInfo taskAMax;
        FieldInfo taskAParamType;
        FieldInfo taskAName;
        FieldInfo taskBType;
        FieldInfo taskBValue;
        //FieldInfo taskBMax;
        //FieldInfo taskBParamType;
        //FieldInfo taskBName;

        AnimatorControllerParameter[] parameterList;

        // CVR's AnimatorDriver lacks a local only toggle
        public VRC2CVRDriverConverter()
        {
#if VRC_SDK_VRCSDK3 && CVR_CCK_EXISTS
            cvrDriverType = Type.GetType("ABI.CCK.Components.AnimatorDriver, Assembly-CSharp", true);
            cvrEnterTaskList = cvrDriverType.GetField("EnterTasks");
            cvrEnterTaskAdd = cvrEnterTaskList.FieldType.GetMethod("Add");

            cvrDriverTaskType = Type.GetType("ABI.CCK.Components.AnimatorDriverTask, Assembly-CSharp", true);
            taskTargetType = cvrDriverTaskType.GetField("targetType");
            taskTargetName = cvrDriverTaskType.GetField("targetName");
            taskOp = cvrDriverTaskType.GetField("op");
            taskAType = cvrDriverTaskType.GetField("aType");
            taskAValue = cvrDriverTaskType.GetField("aValue");
            taskAMax = cvrDriverTaskType.GetField("aMax");
            taskAParamType = cvrDriverTaskType.GetField("aParamType");
            taskAName = cvrDriverTaskType.GetField("aName");

            taskBType = cvrDriverTaskType.GetField("bType");
            taskBValue = cvrDriverTaskType.GetField("bValue");
            //taskBMax = cvrDriverTaskType.GetField("bMax");
            //taskBParamType = cvrDriverTaskType.GetField("bParamType");
            //taskBName = cvrDriverTaskType.GetField("bName");

            cvrParamType = taskTargetType.FieldType;
            cvrOperator = taskOp.FieldType;
            cvrSourceType = taskAType.FieldType;
#endif
        }

        public override void ParameterListInspectFinal(AnimatorControllerParameter[] parameters) { parameterList = parameters; }

        // Behaviour remapper callback to be passed in LayerCopy.Copy.
        // Remaps VRC paramater drivers with CVR ones
        public override Type RemapStateMachineBehaviourType(Type type)
        {
            switch (type)
            {
#if VRC_SDK_VRCSDK3 && CVR_CCK_EXISTS
                case var _ when type == typeof(VRC.SDK3.Avatars.Components.VRCAnimatorLayerControl):
                case var _ when type == typeof(VRC.SDK3.Avatars.Components.VRCAnimatorLocomotionControl):
                case var _ when type == typeof(VRC.SDK3.Avatars.Components.VRCAnimatorTemporaryPoseSpace):
                case var _ when type == typeof(VRC.SDK3.Avatars.Components.VRCAnimatorTrackingControl):
                case var _ when type == typeof(VRC.SDK3.Avatars.Components.VRCPlayableLayerControl):
                    return null;
                case var _ when type == typeof(VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver):
                    Type ret = cvrDriverType;
                    if (ret == null)
                        throw new System.Exception("Failed to get CVR AnimatorDriver type");
                    return ret;
#endif
                default:
                    return null;
            }
        }

        // Process parameter callback to be passed in LayerCopy.Copy.
        // Converts VRC Drivers to CVR Drivers
        // We can convert eveything except copy with range convert
        // Maybe it can be done with successive operations
        // We also need to magic out the paramater type, VRC dosn't store it but CVR needs it
        public override void RemapStateMachineBehaviourCopy(in StateMachineBehaviour oldBehaviour, StateMachineBehaviour newBehaviour)
        {
#if VRC_SDK_VRCSDK3 && CVR_CCK_EXISTS
            Type nType = newBehaviour.GetType();
            var vrcDriver = (VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver)oldBehaviour;

            // Copy isEnabled?
            foreach (var parms in vrcDriver.parameters)
            {
                object newTask = System.Activator.CreateInstance(cvrDriverTaskType);
                taskTargetType.SetValue(newTask, ControllerTypeToCVRType(GetParamaterType(parms.name)));
                taskTargetName.SetValue(newTask, parms.name);

                switch (parms.type)
                {
                    case VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set:
                        taskOp.SetValue(newTask, cvrOperator.GetField("Set").GetValue(cvrOperator));

                        taskAType.SetValue(newTask, cvrSourceType.GetField("Static").GetValue(cvrSourceType));
                        taskAValue.SetValue(newTask, parms.value);
                        break;
                    case VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Add:
                        taskOp.SetValue(newTask, cvrOperator.GetField("Addition").GetValue(cvrOperator));

                        taskAType.SetValue(newTask, cvrSourceType.GetField("Parameter").GetValue(cvrSourceType));
                        taskAParamType.SetValue(newTask, ControllerTypeToCVRType(GetParamaterType(parms.name)));
                        taskAName.SetValue(newTask, parms.name);

                        taskBType.SetValue(newTask, cvrSourceType.GetField("Static").GetValue(cvrSourceType));
                        taskBValue.SetValue(newTask, parms.value);
                        break;
                    case VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Random:
                        AnimatorControllerParameterType dstType = GetParamaterType(parms.name);

                        if (dstType == AnimatorControllerParameterType.Bool | dstType == AnimatorControllerParameterType.Trigger)
                        {
                            taskOp.SetValue(newTask, cvrOperator.GetField("LessEqual").GetValue(cvrOperator));

                            taskAType.SetValue(newTask, cvrSourceType.GetField("Random").GetValue(cvrSourceType));
                            taskAValue.SetValue(newTask, 0f);
                            taskAMax.SetValue(newTask, 1f);

                            taskBType.SetValue(newTask, cvrSourceType.GetField("Static").GetValue(cvrSourceType));
                            taskBValue.SetValue(newTask, parms.chance);
                        }
                        else
                        {
                            taskOp.SetValue(newTask, cvrOperator.GetField("Set").GetValue(cvrOperator));

                            taskAType.SetValue(newTask, cvrSourceType.GetField("Random").GetValue(cvrSourceType));
                            taskAValue.SetValue(newTask, parms.valueMin);
                            taskAMax.SetValue(newTask, parms.valueMax);
                        }
                        break;
                    case VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Copy:
                        if (parms.convertRange & (parms.destMax != parms.sourceMax | parms.destMin != parms.sourceMax))
                        {
                            // We will need to add a temp param to handle range conversions
                            // Escpecially if destination type differs from source type
                            // However it is too late to do so here
                            throw new System.NotImplementedException("Copy With Range Conversion");
                        }
                        else
                        {
                            taskOp.SetValue(newTask, cvrOperator.GetField("Set").GetValue(cvrOperator));

                            taskAType.SetValue(newTask, cvrSourceType.GetField("Parameter").GetValue(cvrSourceType));
                            taskAParamType.SetValue(newTask, ControllerTypeToCVRType(GetParamaterType(parms.source)));
                            taskAName.SetValue(newTask, parms.source);
                        }

                        break;
                    default:
                        throw new System.NotImplementedException($"Unkown type {parms.type}");
                }

                cvrEnterTaskAdd.Invoke(cvrEnterTaskList.GetValue(newBehaviour), new[] { newTask });
            }
#endif
        }

        private AnimatorControllerParameterType GetParamaterType(string paramName)
        {
            foreach (AnimatorControllerParameter param in parameterList)
            {
                if (param.name == paramName)
                    return param.type;
            }
            throw new System.Exception("Failed to get paramater type");
        }

        private object ControllerTypeToCVRType(AnimatorControllerParameterType type)
        {
            switch(type) 
            {
                case AnimatorControllerParameterType.Float:
                    return cvrParamType.GetField("Float").GetValue(cvrParamType);
                case AnimatorControllerParameterType.Int:
                    return cvrParamType.GetField("Int").GetValue(cvrParamType);
                case AnimatorControllerParameterType.Bool:
                    return cvrParamType.GetField("Bool").GetValue(cvrParamType);
                case AnimatorControllerParameterType.Trigger:
                    return cvrParamType.GetField("Trigger").GetValue(cvrParamType);
                default:
                    throw new System.NotImplementedException($"Unkown type {type}");
            }
        }
    }
}
