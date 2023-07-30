using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

public class ProcessorMulti : CopyProcessor
{
    readonly CopyProcessor[] copyProcessors;
    Dictionary<System.Type, CopyProcessor> remapper = new Dictionary<System.Type, CopyProcessor>();

    public ProcessorMulti(CopyProcessor[] processors)
    {
        copyProcessors = processors;
    }

    public override AnimatorControllerParameter ParameterPreProcess(in AnimatorControllerParameter parameter)
    {
        AnimatorControllerParameter finalParamater = parameter;
        bool modified = false;

        foreach (var processor in copyProcessors)
        {
            AnimatorControllerParameter ret = processor.ParameterPreProcess(finalParamater);
            if (ret != null)
            {
                modified = true;
                finalParamater = ret;
            }
        }

        return modified ? finalParamater : null;
    }

    public override void ParameterListInspectFinal(AnimatorControllerParameter[] parameters)
    {
        foreach (var processor in copyProcessors)
            processor.ParameterListInspectFinal(parameters);
    }

    public override void StatePostProcess(AnimatorState state)
    {
        foreach (var processor in copyProcessors)
            processor.StatePostProcess(state);
    }

    public override void BlendTreePostProcess(ref bool externalAsset, ref BlendTree blendTree, System.Action<BlendTree> prepareExternalForEmbed)
    {
        foreach (var processor in copyProcessors)
            processor.BlendTreePostProcess(ref externalAsset, ref blendTree, prepareExternalForEmbed);
    }

    public override AnimationClip AnimationClipPreProcess(in AnimationClip animationClip)
    {
        AnimationClip finalClip = animationClip;
        bool modified = false;

        foreach (var processor in copyProcessors)
        {
            AnimationClip ret = processor.AnimationClipPreProcess(finalClip);
            if (ret != null)
            {
                modified = true;
                finalClip = ret;
            }
        }

        return modified ? finalClip : null;
    }

    public override void TransitionPostProcess(AnimatorTransitionBase[] transitions,
        System.Func<AnimatorState, AnimatorTransitionBase> addTransitionToState, System.Func<AnimatorStateMachine, AnimatorTransitionBase> addTransitionToMachine, System.Func<AnimatorTransitionBase> addTransitionToExit,
        System.Action<AnimatorTransitionBase> removeTransition, System.Action<AnimatorTransitionBase, AnimatorTransitionBase> copyTransition)
    {
        foreach (var processor in copyProcessors)
            processor.TransitionPostProcess(transitions, addTransitionToState, addTransitionToMachine, addTransitionToExit, removeTransition, copyTransition);
    }

    // Chaining remappers is not supported
    public override System.Type RemapStateMachineBehaviourType(System.Type type)
    {
        foreach (var processor in copyProcessors)
        {
            System.Type ret = processor.RemapStateMachineBehaviourType(type);
            if (ret != null)
            {
                remapper.Add(type, processor);
                return ret;
            }
        }

        return null;
    }

    public override void RemapStateMachineBehaviourCopy(in StateMachineBehaviour oldBehaviour, StateMachineBehaviour newBehaviour)
    {
        remapper[oldBehaviour.GetType()].RemapStateMachineBehaviourCopy(oldBehaviour, newBehaviour);
    }

    public override void StateMachineBehaviourPostProcess(StateMachineBehaviour behaviour)
    {
        foreach (var processor in copyProcessors)
            processor.StateMachineBehaviourPostProcess(behaviour);
    }
}