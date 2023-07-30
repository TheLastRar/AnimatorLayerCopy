using UnityEditor.Animations;
using UnityEngine;

public class CopyProcessor
{
    /// <summary>
    /// Method to inspect a source parameter and returns a new, modified parameter to be inserted, input parameter should not be modified.
    /// Returns null to insert a copy of the source parameter as is.
    /// </param>
    public virtual AnimatorControllerParameter ParameterPreProcess(in AnimatorControllerParameter parameter) { return null; }

    /// <summary>
    /// Method to inspect the destination paramater list after copy
    /// </param>
    public virtual void ParameterListInspectFinal(AnimatorControllerParameter[] parameters) { }

    /// <summary>
    /// Method to modify an AnimatorState.
    /// </summary>
    public virtual void StatePostProcess(AnimatorState state) { }

    /// <summary>
    /// Method to modify or replace a blend tree.
    /// For multilayered blend trees, the blend tree in the deepest embedded blend tree is called first.
    /// Child motions of external blend trees are not process unless passed as an argument of <c>prepareExternalForEmbed</c>.
    /// If the blend tree is an external asset (<c>externalAsset == true</c>), than the original blend tree is passed in <c>blendTree</c>.
    /// <c>prepareExternalForEmbed</c> is a helper method to deep copy blend trees
    /// </summary>
    public virtual void BlendTreePostProcess(ref bool externalAsset, ref BlendTree blendTree, System.Action<BlendTree> prepareExternalForEmbed) { }

    /// <summary>
    /// Method to inspect a source AnimationClip and returns a new, replacement animation to be inserted, input animation should not be modified.
    /// For animations in multilayered blend trees, the animations in the deepest embedded blend tree is called first.
    /// Returns null to reference the original AnimationClip
    /// </summary>
    public virtual AnimationClip AnimationClipPreProcess(in AnimationClip animationClip) { return null; }

    /// <summary>
    /// Method to modify transitions.
    /// <c>addTransitionTo*</c> is used to add new transitions to either an AnimatorState/AnimatorStateMachine in the layer, or to the Exit state.
    /// <c>addTransitionToExit</c> may be null.
    /// <c>removeTransition</c> is used to remove a transition.
    /// <c>copyTransition</c> is used a transition settings from src (param 1) to dst (param 2).
    /// </summary>
    public virtual void TransitionPostProcess(AnimatorTransitionBase[] transitions,
        System.Func<AnimatorState, AnimatorTransitionBase> addTransitionToState, System.Func<AnimatorStateMachine, AnimatorTransitionBase> addTransitionToMachine, System.Func<AnimatorTransitionBase> addTransitionToExit,
        System.Action<AnimatorTransitionBase> removeTransition, System.Action<AnimatorTransitionBase, AnimatorTransitionBase> copyTransition)
    { }

    /// <summary>
    /// Method to provide a replacement type
    /// Returns null to not remap
    /// </summary>
    /// <param name="type">Source StateMachineBevahiour Type</param>
    public virtual System.Type RemapStateMachineBehaviourType(System.Type type) { return null; }

    /// <summary>
    /// Called to copy a remaped behaviour
    /// </summary>
    /// <param name="oldBehaviour">Source StateMachineBevahiour</param>
    /// <param name="newBehaviour">Destination StateMachineBevahiour</param>
    public virtual void RemapStateMachineBehaviourCopy(in StateMachineBehaviour oldBehaviour, StateMachineBehaviour newBehaviour) { }

    /// <summary>
    /// Method to modify a state machine behaviour.
    /// </summary>
    public virtual void StateMachineBehaviourPostProcess(StateMachineBehaviour behaviour) { }
}