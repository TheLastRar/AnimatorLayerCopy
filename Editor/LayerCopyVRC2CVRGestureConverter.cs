using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace Air.LayerCopy
{
    public class VRC2CVRGestureConverter
    {
        private struct Range
        {
            public Range(float parLow, float parHigh)
            {
                low = parLow;
                high = parHigh;
            }

            public float low;
            public float high;
        }

        //Are gestures in src animator int type (as used by VRC)
        private bool vrc2cvrGesturesConvertable = false;

        // Process parameter callback to be passed in LayerCopy.Copy.
        // Validates that existing Gesture* parameters are int, then changes the type to float
        public AnimatorControllerParameter PreProcessParameter(in AnimatorControllerParameter parameter)
        {
            if (parameter.name == "GestureLeft" || parameter.name == "GestureRight")
            {
                vrc2cvrGesturesConvertable = true;
                if (parameter.type == AnimatorControllerParameterType.Int)
                    return new AnimatorControllerParameter() { name = parameter.name, type = AnimatorControllerParameterType.Float, defaultFloat = 0f };
            }
            return null;
        }

        // Process parameter callback to be passed in LayerCopy.Copy.
        // Converts transitions to use float comparisons, along with remapping values to match CVR
        public void PostProcessTransitions(AnimatorTransitionBase[] transitions,
            System.Func<AnimatorState, AnimatorTransitionBase> addTransitionToState, System.Func<AnimatorStateMachine, AnimatorTransitionBase> addTransitionToMachine,
            System.Func<AnimatorTransitionBase> addTransitionToExit, System.Action<AnimatorTransitionBase> removeTransition, System.Action<AnimatorTransitionBase, AnimatorTransitionBase> copyTransition)
        {
            if (!vrc2cvrGesturesConvertable)
                return;

            //NotEqual is tricky.
            //For a NotEqual condition, we need to duplicate the entire transitios, with one tansition With a Greater condition, and one with Less condition.
            //However, we also need to handle multiple NotEqual conditions, so we need to track ranges of valid values and duplicate conditions based on that.

            //Where we mix NotEqual Left & Right conditions, we would loop though valid ranges of right values for each valid range of left values.

            //Greater and Less are also tricky, as CVR guestures are orded differently to VRC.
            //Implement as a a set of NotEqual ranges, and handle using the same logic as for NotEqual.


            //TODO: Compare across conditions for pre-split transitions as an optimisation

            foreach (AnimatorTransitionBase transition in transitions)
            {
                List<Range> rangeLeft = new List<Range>() { new Range(float.NegativeInfinity, float.PositiveInfinity) };
                List<Range> rangeRight = new List<Range>() { new Range(float.NegativeInfinity, float.PositiveInfinity) };

                bool hasGesture = false;

                foreach (AnimatorCondition condition in transition.conditions)
                {
                    if (condition.parameter == "GestureLeft" || condition.parameter == "GestureRight")
                    {
                        hasGesture = true;
                        //Removing multiple conditions errors out
                        transition.RemoveCondition(condition);
                        float threshold = condition.threshold;

                        //VRC Gestures range  0 - 7.
                        //CVR Gestures range -1 - 6.

                        //As params are reshuffled, we would need to exclude specific ranges to emulate Greater/Less.
                        //Values to exclude with greater than.
                        List<Range> rangeNotGreater = new List<Range>();
                        //values to exclude with less than.
                        List<Range> rangeNotLess = new List<Range>();

                        //Remap values and build exclusion ranges.
                        //Behaviour of out of range values are not defined.
                        switch (threshold)
                        {
                            case 0:
                                threshold = 0;
                                rangeNotGreater.Add(new Range(0, 0)); //Idle
                                rangeNotGreater.Add(new Range(float.NegativeInfinity, -2)); //Open Hand

                                rangeNotLess.Add(new Range(float.NegativeInfinity, float.PositiveInfinity)); //Eveything
                                break;
                            case 1: //Fist
                                threshold = 1;
                                rangeNotGreater.Add(new Range(0, 1)); //Idle, Fist
                                rangeNotGreater.Add(new Range(float.NegativeInfinity, -2)); //Open Hand

                                rangeNotLess.Add(new Range(1, float.PositiveInfinity));
                                rangeNotLess.Add(new Range(float.NegativeInfinity, -1)); //Open Hand
                                break;
                            case 2: //Open
                                threshold = -1;
                                rangeNotGreater.Add(new Range(float.NegativeInfinity, 1));

                                rangeNotLess.Add(new Range(2, float.PositiveInfinity)); //Fist & Idle not excluded
                                rangeNotLess.Add(new Range(float.NegativeInfinity, -1)); //Open Hand
                                break;
                            case 3: //Point
                                threshold = 4;
                                //Don't exclude HandGun & Thumbs Up
                                rangeNotGreater.Add(new Range(float.NegativeInfinity, 1));
                                rangeNotGreater.Add(new Range(4, 4));

                                //Also Exclude Hand Gun & Thumbs Up
                                rangeNotLess.Add(new Range(2, float.PositiveInfinity));
                                break;
                            case 4: //Victory
                                threshold = 5;
                                //Don't exclude HandGun & Thumbs Up
                                rangeNotGreater.Add(new Range(float.NegativeInfinity, 1));
                                rangeNotGreater.Add(new Range(4, 5));

                                rangeNotLess.Add(new Range(5, float.PositiveInfinity));
                                //Also Exclude Hand Gun & Thumbs Up, but don't exclude Point
                                rangeNotLess.Add(new Range(2, 3));
                                break;
                            case 5: //RocknRoll
                                threshold = 6;
                                //Don't exclude HandGun & Thumbs Up
                                rangeNotGreater.Add(new Range(float.NegativeInfinity, 1));
                                rangeNotGreater.Add(new Range(4, float.PositiveInfinity));

                                rangeNotLess.Add(new Range(6, float.PositiveInfinity));
                                //Also Exclude Hand Gun & Thumbs Up, but don't exclude Point
                                rangeNotLess.Add(new Range(2, 3));
                                break;
                            case 6: //HandGun
                                threshold = 3;
                                //Don't exclude Thumbs Up
                                rangeNotGreater.Add(new Range(float.NegativeInfinity, 1));
                                rangeNotGreater.Add(new Range(3, float.PositiveInfinity));

                                rangeNotLess.Add(new Range(2, 3));
                                break;
                            case 7: //Thumbs Up
                                threshold = 2;
                                rangeNotGreater.Add(new Range(float.NegativeInfinity, float.PositiveInfinity)); //Eveything

                                rangeNotLess.Add(new Range(2, 2)); //Not Thumbs Up
                                break;
                        }

                        //We add 0.1 for float accuracy
                        void ApplyExcludedRanges(List<Range> ranges, List<Range> exclusions)
                        {
                            //Different offsets to match expected used values of Greater / Less
                            foreach (Range excluded in exclusions)
                                if (excluded.low == excluded.high)
                                    ExcludeRange(ranges, excluded.low - 0.1f, excluded.high + 0.1f);
                                else
                                    ExcludeRange(ranges, excluded.low - 0.9f, excluded.high + 0.1f);
                        }

                        switch (condition.mode)
                        {
                            case AnimatorConditionMode.Greater:
                                //if (condition.parameter == "GestureLeft")
                                //    SetRangeMin(rangeLeft, threshold + 0.1f);
                                //else
                                //    SetRangeMin(rangeRight, threshold + 0.1f);
                                if (condition.parameter == "GestureLeft")
                                    ApplyExcludedRanges(rangeLeft, rangeNotGreater);
                                else
                                    ApplyExcludedRanges(rangeRight, rangeNotGreater);

                                break;
                            case AnimatorConditionMode.Less:
                                //if (condition.parameter == "GestureLeft")
                                //    SetRangeMax(rangeLeft, threshold - 0.1f);
                                //else
                                //    SetRangeMax(rangeRight, threshold - 0.1f);
                                if (condition.parameter == "GestureLeft")
                                    ApplyExcludedRanges(rangeLeft, rangeNotLess);
                                else
                                    ApplyExcludedRanges(rangeRight, rangeNotLess);

                                break;
                            case AnimatorConditionMode.Equals:
                                if (condition.parameter == "GestureLeft")
                                {
                                    SetRangeMin(rangeLeft, threshold - 0.1f);
                                    SetRangeMax(rangeLeft, threshold + 0.1f);
                                }
                                else
                                {
                                    SetRangeMin(rangeRight, threshold - 0.1f);
                                    SetRangeMax(rangeRight, threshold + 0.1f);
                                }
                                break;
                            case AnimatorConditionMode.NotEqual:
                                if (condition.parameter == "GestureLeft")
                                    ExcludeRange(rangeLeft, threshold - 0.1f, threshold + 0.1f);
                                else
                                    ExcludeRange(rangeRight, threshold - 0.1f, threshold + 0.1f);
                                break;
                            default:
                                throw new System.NotImplementedException("Unexpected AnimatorConditionMode VRC2CVR");
                        }
                    }
                }

                if (hasGesture)
                {
                    //Remove unreacable ranges
                    List<Range> UnreachableRanges = new List<Range>();
                    foreach (Range range in rangeLeft)
                        if (System.Math.Floor(range.low) == System.Math.Floor(range.high))
                            UnreachableRanges.Add(range);

                    foreach (Range range in UnreachableRanges)
                        rangeLeft.Remove(range);

                    UnreachableRanges.Clear();
                    foreach (Range range in rangeRight)
                        if (System.Math.Floor(range.low) == System.Math.Floor(range.high))
                            UnreachableRanges.Add(range);

                    foreach (Range range in UnreachableRanges)
                        rangeRight.Remove(range);


                    //Add new transitions
                    foreach (Range rangeL in rangeLeft)
                    {
                        foreach (Range rangeR in rangeRight)
                        {
                            AnimatorTransitionBase nTransition;
                            if (transition.destinationState != null)
                                nTransition = addTransitionToState(transition.destinationState);
                            else if (transition.destinationStateMachine != null)
                                nTransition = addTransitionToMachine(transition.destinationStateMachine);
                            else if (addTransitionToExit != null && transition.isExit)
                                nTransition = addTransitionToExit();
                            else
                                throw new System.NotSupportedException("Unkown StateMachine Transition type");

                            copyTransition(transition, nTransition);

                            if (rangeL.low > float.NegativeInfinity)
                                nTransition.AddCondition(AnimatorConditionMode.Greater, rangeL.low, "GestureLeft");
                            if (rangeL.high < float.PositiveInfinity)
                                nTransition.AddCondition(AnimatorConditionMode.Less, rangeL.high, "GestureLeft");

                            if (rangeR.low > float.NegativeInfinity)
                                nTransition.AddCondition(AnimatorConditionMode.Greater, rangeR.low, "GestureRight");
                            if (rangeR.high < float.PositiveInfinity)
                                nTransition.AddCondition(AnimatorConditionMode.Less, rangeR.high, "GestureRight");
                        }
                    }
                    //Delete old
                    removeTransition(transition);
                }
            }
        }

        #region RangeFunctions
        private void SetRangeMin(List<Range> ranges, float min)
        {
            int invalidRanges = 0;
            for (int i = 0; i < ranges.Count; i++)
            {
                //Range is above condition
                if (ranges[i].low >= min)
                    break;
                //Range is within condition
                if (ranges[i].low < min && ranges[i].high > min)
                {
                    ranges[i] = new Range(min, ranges[i].high);
                    break;
                }
                //Range is below condition
                if (ranges[i].high <= min)
                    invalidRanges++;
            }
            for (int i = invalidRanges; i > 0; i--)
                ranges.RemoveAt(i - 1);
        }

        private void SetRangeMax(List<Range> ranges, float max)
        {
            int invalidRanges = 0;
            for (int i = ranges.Count - 1; i >= 0; i--)
            {
                //Range is below condition
                if (ranges[i].high <= max)
                    break;
                //Range is within condition
                if (ranges[i].high > max && ranges[i].low < max)
                {
                    ranges[i] = new Range(ranges[i].low, max);
                    break;
                }
                //Range is above condition
                if (ranges[i].low >= max)
                    invalidRanges++;
            }
            for (int i = ranges.Count; i > ranges.Count - invalidRanges; i--)
                ranges.RemoveAt(i - 1);
        }

        private void ExcludeRange(List<Range> ranges, float low, float high)
        {
            int rangeLow = -1;
            int rangeHigh = -1;

            // Find ranges that contain the low and high value.
            for (int i = 0; i < ranges.Count; i++)
            {
                if (ranges[i].low <= low && ranges[i].high > low)
                    rangeLow = i;
                if (ranges[i].low < high && ranges[i].high >= high)
                    rangeHigh = i;
            }

            // Are ranges different?
            if (rangeLow != rangeHigh)
            {
                // low and high are in different ranges.
                // Adjust each range to exclude values bettween low & high.
                if (rangeLow != -1)
                    ranges[rangeLow] = new Range(ranges[rangeLow].low, low);

                if (rangeHigh != -1)
                    ranges[rangeHigh] = new Range(high, ranges[rangeHigh].high);
            }
            else if (rangeLow != -1)
            {
                // Both low and high within the same range.
                // Are we at the edge of the range?
                Range totalRange = ranges[rangeLow];
                if (low != totalRange.low && high != totalRange.high)
                {
                    // Noth low and high are mid range.
                    // Split range to exclude values bettween low & high.
                    ranges[rangeLow] = new Range(high, totalRange.high);
                    ranges.Insert(rangeLow, new Range(totalRange.low, low));
                }
                else if (low == totalRange.low)
                    // low aligns with bottom of range.
                    // Adjust range to clear values below high.
                    ranges[rangeLow] = new Range(high, totalRange.high);
                else //(high == totalRange.high)
                     // high aligns with top of range.
                     // Adjust range to clear values above low.
                    ranges[rangeLow] = new Range(totalRange.low, low);
            }
        }
        #endregion
    }
}
