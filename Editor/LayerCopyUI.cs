using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Air.LayerCopy
{
    public class LayerCopyUI : EditorWindow
    {
        private Vector2 scroll;

        AnimatorController srcAnimator;
        AnimatorController dstAnimator;

        bool vrc2cvrGestures;
        bool swapGestures;

        Dictionary<string, bool> selectedLayers = new Dictionary<string, bool>();

        [MenuItem("Air/Layer Copy")]
        static void Init()
        {
            LayerCopyUI window = (LayerCopyUI)GetWindow(typeof(LayerCopyUI), false, "Layer Copy");
            window.Show();
        }

        void OnDisable() { }

        void OnEnable() { }

        void OnGUI()
        {
            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
            {
                alignment = TextAnchor.UpperCenter
            };

            GUILayout.Label("Select Animators", centeredStyle);
            EditorGUILayout.BeginVertical();

            srcAnimator = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("Source Controller", "Controller to copy from"), srcAnimator, typeof(AnimatorController), false);
            dstAnimator = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("Destination Controller", "Controller to copy to"), dstAnimator, typeof(AnimatorController), false);

            GUILayout.Space(8);

            GUILayout.Label("Select Layers to Copy", centeredStyle);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            Dictionary<string, bool> oldSelection = selectedLayers;
            selectedLayers = new Dictionary<string, bool>();

            vrc2cvrGestures = EditorGUILayout.Toggle("VRC To CVR (WIP)", vrc2cvrGestures);
            swapGestures = EditorGUILayout.Toggle("Swap Left/Right Gestures", swapGestures);

            if (srcAnimator != null)
            {
                GUILayout.Label($"Source Animator layer count: {srcAnimator.layers.Length}", centeredStyle);
                foreach (AnimatorControllerLayer layer in srcAnimator.layers)
                {
                    string name = layer.name;
                    bool val = oldSelection.ContainsKey(name) ? oldSelection[name] : false;

                    selectedLayers.Add(name, EditorGUILayout.Toggle(name, val));
                }
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Select All"))
            {
                foreach (AnimatorControllerLayer layer in srcAnimator.layers)
                    selectedLayers[layer.name] = true;
            }
            if (GUILayout.Button("Select None"))
            {
                foreach (AnimatorControllerLayer layer in srcAnimator.layers)
                    selectedLayers[layer.name] = false;
            }

            GUILayout.Space(8);

            if (GUILayout.Button("Copy"))
            {
                if (vrc2cvrGestures)
                {
                    if (swapGestures)
                        Debug.LogError("'VRC To CVR' is not supported with 'Swap Gestures'");
                    else
                    {
                        ProcessorMulti vrc2cvr = new ProcessorMulti(new CopyProcessor[] { new VRC2CVRGestureConverter(), new VRC2CVRDriverConverter() });
                        LayerCopy.Copy(srcAnimator, dstAnimator, selectedLayers, vrc2cvr);
                    }
                }
                else
                {
                    if (swapGestures)
                    {
                        //Use ParameterRenamer
                        ParameterRenamer renamer = new ParameterRenamer();
                        string RenameSwapGestures(string input)
                        {
                            if (input == "GestureRight")
                                return "GestureLeft";
                            if (input == "GestureLeft")
                                return "GestureRight";

                            return input;
                        }
                        renamer.renameFunction = RenameSwapGestures;
                        LayerCopy.Copy(srcAnimator, dstAnimator, selectedLayers, renamer);
                    }
                    else
                        LayerCopy.Copy(srcAnimator, dstAnimator, selectedLayers);
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
