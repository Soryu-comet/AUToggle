using UnityEditor;
using UnityEngine;
using UnityEditor; // Added for SerializedObject, AssetDatabase, etc.
using VRC.SDK3.Avatars.Components;
using System.IO;
using System.Linq;
using UnityEditor.Animations; // For AnimatorController, AnimationClip, AnimatorState, etc.
using VRC.SDK3.Avatars.ScriptableObjects; // For VRCExpressionsMenu, VRCExpressionParameters
using nadena.dev.modular_avatar.core; // For MA components
using UnityEditor.Callbacks;
using SoryuShop.AUToggle; // For AUToggleInfo
using System; // For Guid
using VRC.SDKBase; // For IEditorOnly
using VRC.SDK3.Dynamics.PhysBone.Components; // For VRCPhysBone
using System.Collections.Generic; // For List
using UnityEngine.Animations; // For Constraint components

namespace SoryuShop.AUToggle
{
    public class AUToggleEditor : EditorWindow
    {
        private const string BaseGeneratedPath = "Assets/SoryuShop/AUToggle/Generated";
        private const string MenuPathBase = "GameObject/AUToggle/";
        private const string SetupMenuName = MenuPathBase + "Setup AUToggle";
        private const string UninstallMenuName = MenuPathBase + "Uninstall AUToggle";

        // Target object and avatar for the specific window instance
        private GameObject _instanceTargetObject;
        private VRCAvatarDescriptor _instanceAvatarDescriptor;

        // UI related variables - now properties
        private string _menuName = "";
        private Texture2D _icon;
        private bool _defaultState = true; // true: ON, false: OFF
        private bool _isStateSaved = true;

        private bool _isSetupComplete = false;
        // For completion screen - now properties
        private string _completedObjectName = "";
        private string _completedParameterName = "";
        private string _completedFolderPath = "";

        // Renderer
        private AUToggleIMGUIRenderer _guiRenderer;

        // Styles - will be initialized and passed to the renderer
        private GUIStyle _titleStyle = null;
        private GUIStyle _largeButtonStyle = null;
        private GUIStyle _toggleButtonStyle = null;
        private GUIStyle _headerStyle = null;
        private GUIStyle _sectionTitleStyle = null;
        private GUIStyle _previewBoxStyle = null;

        // Properties for AUToggleIMGUIRenderer to access/modify
        public GameObject TargetObject => _instanceTargetObject;
        public VRCAvatarDescriptor AvatarDescriptor => _instanceAvatarDescriptor;
        public string MenuName { get => _menuName; set => _menuName = value; }
        public Texture2D Icon { get => _icon; set => _icon = value; }
        public bool DefaultState { get => _defaultState; set => _defaultState = value; }
        public bool IsStateSaved { get => _isStateSaved; set => _isStateSaved = value; }
        public bool IsSetupComplete => _isSetupComplete;
        public string CompletedObjectName => _completedObjectName;
        public string CompletedParameterName => _completedParameterName;
        public string CompletedFolderPath => _completedFolderPath;


        // Window management
        public static void ShowWindow(GameObject targetObject)
        {
            AUToggleEditor window = GetWindow<AUToggleEditor>("AUToggle");
            
            window._instanceTargetObject = targetObject;
            window._instanceAvatarDescriptor = null;
            if (targetObject != null)
            {
                window._instanceAvatarDescriptor = targetObject.GetComponentInParent<VRCAvatarDescriptor>();
                window._menuName = targetObject.name; // Default menu name to object name
            }
            else
            {
                window._menuName = ""; // Clear menu name if no target
            }
            window._isSetupComplete = false; // Reset setup complete flag

            window.titleContent = new GUIContent("AUToggle おーとぐる"); // Window title
            window.minSize = new Vector2(400, 520);
            window.Show();
        }

        // --- Context Menu Items ---
        [MenuItem(SetupMenuName, false, 0)]
        private static void SetupAUToggleMenu()
        {
            GameObject selectedObject = Selection.activeGameObject;
            ShowWindow(selectedObject); 
        }

        [MenuItem(SetupMenuName, true)]
        private static bool ValidateSetupAUToggleMenu()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null) return false;
            // Validation should check against the specific instance's target if possible,
            // but for a static menu item, this is the best we can do.
            // The actual check if AUToggleInfo exists is better done inside ShowWindow or OnEnable.
            return selectedObject.GetComponent<AUToggleInfo>() == null;
        }

        [MenuItem(UninstallMenuName, false, 0)]
        private static void UninstallAUToggleMenu()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Debug.LogWarning("AUToggle: No GameObject selected for uninstall.");
                return;
            }

            if (EditorUtility.DisplayDialog("AUToggle アンインストール確認",
                $"'{selectedObject.name}' からAUToggleの関連コンポーネントをアンインストールしますか？\n" +
                $"(生成されたアセットフォルダは保持されます。この操作はUndo可能です)",
                "アンインストール実行", "キャンセル"))
            {
                PerformUninstall(selectedObject);
            }
        }

        [MenuItem(UninstallMenuName, true)]
        private static bool ValidateUninstallAUToggleMenu()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null) return false;
            return selectedObject.GetComponent<AUToggleInfo>() != null;
        }
        
        private static void PerformUninstall(GameObject targetObject)
        {
            AUToggleInfo info = targetObject.GetComponent<AUToggleInfo>();
            if (info == null)
            {
                Debug.LogWarning($"[AUToggle] Uninstall target '{targetObject.name}' does not have AUToggleInfo component. Skipping uninstall.");
                return;
            }

            string objectName = targetObject.name;
            string folderPathToKeep = info.generatedFolderPath; // Renamed for clarity

            Undo.SetCurrentGroupName("Uninstall AUToggle from " + objectName);
            int group = Undo.GetCurrentGroup();

            RemoveComponent<ModularAvatarMenuInstaller>(targetObject);
            RemoveComponent<ModularAvatarParameters>(targetObject);
            RemoveComponent<ModularAvatarMenuItem>(targetObject);
            RemoveComponent<ModularAvatarMergeAnimator>(targetObject);
            
            Undo.DestroyObjectImmediate(info);
            Debug.Log($"[AUToggle] Removed AUToggleInfo and MA components from '{objectName}'.");

            Debug.Log($"[AUToggle] Asset folder '{folderPathToKeep}' was intentionally preserved during uninstall.");
            
            Undo.CollapseUndoOperations(group);
            EditorUtility.DisplayDialog("AUToggle アンインストール完了", $"'{objectName}' からAUToggleの関連コンポーネントをアンインストールしました。\n生成されたアセットフォルダは保持されています。", "OK");
        }

        private static void RemoveComponent<T>(GameObject targetObject) where T : Component
        {
            T component = targetObject.GetComponent<T>();
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }
        }

        private void OnEnable()
        {
            // Initialize GUI Renderer here if it's not already
            if (_guiRenderer == null)
            {
                _guiRenderer = new AUToggleIMGUIRenderer(this);
            }
            // Styles are initialized in OnGUI before drawing, to ensure GUI.skin is ready.
        }
        
        private void InitializeStylesIfNeeded() // Renamed for clarity
        {
            if (_titleStyle == null) // Check one, assume all need init if one is null
            {
                _titleStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 5, 5)
                };
                _headerStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(0, 0, 3, 3)
                };
                _sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    padding = new RectOffset(0, 0, 2, 2)
                };
                _largeButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15, fontStyle = FontStyle.Bold, fixedHeight = 35,
                    margin = new RectOffset(5, 5, 5, 5)
                };
                _toggleButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 5, 5)
                };
                _previewBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10), margin = new RectOffset(0, 0, 5, 5)
                };
            }
        }

        private void OnGUI()
        {
            InitializeStylesIfNeeded();
            
            if (_guiRenderer == null) // Should be initialized in OnEnable, but as a fallback
            {
                _guiRenderer = new AUToggleIMGUIRenderer(this);
            }
            // Pass styles to the renderer
            _guiRenderer.InitializeStyles(_titleStyle, _largeButtonStyle, _toggleButtonStyle, _headerStyle, _sectionTitleStyle, _previewBoxStyle);

            _guiRenderer.OnGUI(); // Delegate GUI drawing to the renderer
        }

        public List<string> GetExistingMAComponentNames()
        {
            List<string> existingComponents = new List<string>();
            if (TargetObject == null) return existingComponents;

            if (TargetObject.GetComponent<ModularAvatarParameters>() != null) existingComponents.Add("Parameters");
            if (TargetObject.GetComponent<ModularAvatarMenuItem>() != null) existingComponents.Add("Menu Item");
            if (TargetObject.GetComponent<ModularAvatarMergeAnimator>() != null) existingComponents.Add("Merge Animator");
            if (TargetObject.GetComponent<ModularAvatarMenuInstaller>() != null) existingComponents.Add("Menu Installer");
            return existingComponents;
        }
        
        public void CloseWindow()
        {
            this.Close();
        }

        public void SetupLogic() // Public so renderer can call it
        {
            if (TargetObject == null || AvatarDescriptor == null)
            {
                Debug.LogError("AUToggle: Target Object or Avatar Descriptor is missing. Cannot proceed with setup.");
                return;
            }

            // --- Generate Names and Paths ---
            string objectName = TargetObject.name;
            string safeObjectName = string.Join("_", objectName.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrEmpty(safeObjectName)) safeObjectName = "GameObject"; // Fallback name

            string hash = Guid.NewGuid().ToString().Substring(0, 8); // Use instance field
            string generatedParameterName = $"AUToggle_{safeObjectName}_{hash}";
            
            // Ensure base generated directory exists
            if (!AssetDatabase.IsValidFolder(BaseGeneratedPath))
            {
                string parentDir = Path.GetDirectoryName(BaseGeneratedPath);
                string grandParentDir = Path.GetDirectoryName(parentDir);
                if (!AssetDatabase.IsValidFolder(grandParentDir))
                {
                    AssetDatabase.CreateFolder(Path.GetDirectoryName(grandParentDir), Path.GetFileName(grandParentDir));
                }
                if (!AssetDatabase.IsValidFolder(parentDir))
                {
                    AssetDatabase.CreateFolder(grandParentDir, Path.GetFileName(parentDir));
                }
                AssetDatabase.CreateFolder(parentDir, Path.GetFileName(BaseGeneratedPath));
                AssetDatabase.Refresh();
            }
            
            string itemSpecificFolderPath = Path.Combine(BaseGeneratedPath, generatedParameterName);
            if (AssetDatabase.IsValidFolder(itemSpecificFolderPath))
            {
                Debug.LogError($"AUToggle: Folder '{itemSpecificFolderPath}' already exists. Halting setup. Please rename the target object or clean up the Generated folder.");
                return;
            }
            AssetDatabase.CreateFolder(BaseGeneratedPath, generatedParameterName);

            string animatorControllerName = $"AC_{safeObjectName}_{hash}.controller";
            string animatorControllerPath = Path.Combine(itemSpecificFolderPath, animatorControllerName);

            string onClipName = "ON.anim";
            string offClipName = "OFF.anim";
            string onClipPath = Path.Combine(itemSpecificFolderPath, onClipName);
            string offClipPath = Path.Combine(itemSpecificFolderPath, offClipName);

            Debug.Log($"AUToggle: Parameter Name: {generatedParameterName}");
            Debug.Log($"AUToggle: Item Folder Path: {itemSpecificFolderPath}");
            Debug.Log($"AUToggle: Animator Controller Path: {animatorControllerPath}");

            AnimatorController animatorController = AnimatorController.CreateAnimatorControllerAtPath(animatorControllerPath);
            if (animatorController == null)
            {
                Debug.LogError($"AUToggle: Failed to create AnimatorController at {animatorControllerPath}");
                AssetDatabase.DeleteAsset(itemSpecificFolderPath);
                return;
            }
            Undo.RegisterCreatedObjectUndo(animatorController, "Create AUToggle Animator Controller");
            
            animatorController.AddParameter(generatedParameterName, AnimatorControllerParameterType.Bool);

            AnimationClip onClip = new AnimationClip();
            AssetDatabase.CreateAsset(onClip, onClipPath);
            Undo.RegisterCreatedObjectUndo(onClip, "Create AUToggle ON Clip");

            AnimationClip offClip = new AnimationClip();
            AssetDatabase.CreateAsset(offClip, offClipPath);
            Undo.RegisterCreatedObjectUndo(offClip, "Create AUToggle OFF Clip");
            
            AnimatorStateMachine rootStateMachine = animatorController.layers[0].stateMachine;

            AnimatorState onState = rootStateMachine.AddState("ON_State");
            onState.motion = onClip;
            Undo.RegisterCreatedObjectUndo(onState, "Create ON State");

            AnimatorState offState = rootStateMachine.AddState("OFF_State");
            offState.motion = offClip;
            Undo.RegisterCreatedObjectUndo(offState, "Create OFF State");

            if (DefaultState) // Use property
            {
                rootStateMachine.defaultState = onState;
            }
            else
            {
                rootStateMachine.defaultState = offState;
            }

            AnimatorStateTransition offToOnTransition = offState.AddTransition(onState);
            offToOnTransition.AddCondition(AnimatorConditionMode.If, 0, generatedParameterName);
            offToOnTransition.hasExitTime = false;
            offToOnTransition.duration = 0;
            Undo.RegisterCreatedObjectUndo(offToOnTransition, "Create OffToOn Transition");

            AnimatorStateTransition onToOffTransition = onState.AddTransition(offState);
            onToOffTransition.AddCondition(AnimatorConditionMode.IfNot, 0, generatedParameterName);
            onToOffTransition.hasExitTime = false;
            onToOffTransition.duration = 0;
            Undo.RegisterCreatedObjectUndo(onToOffTransition, "Create OnToOff Transition");

            SetAnimationCurves(onClip, offClip, TargetObject); // Use property
            
            ModularAvatarParameters maParams = Undo.AddComponent<ModularAvatarParameters>(TargetObject); // Use property
            ParameterConfig paramConfig = new ParameterConfig
            {
                nameOrPrefix = generatedParameterName,
                syncType = ParameterSyncType.Bool,
                saved = IsStateSaved, // Use property
                defaultValue = DefaultState ? 1f : 0f, // Use property
                hasExplicitDefaultValue = true, 
                internalParameter = false,
                isPrefix = false,
                localOnly = false,
                remapTo = ""
            };
            maParams.parameters.Add(paramConfig);
            EditorUtility.SetDirty(maParams);

            ModularAvatarMenuItem maMenuItem = Undo.AddComponent<ModularAvatarMenuItem>(TargetObject); // Use property
            maMenuItem.Control = new VRCExpressionsMenu.Control
            {
                name = string.IsNullOrEmpty(MenuName) ? TargetObject.name : MenuName, // Use properties
                icon = Icon, // Use property
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = generatedParameterName },
                value = 1
            };
            maMenuItem.isSaved = IsStateSaved; // Use property
            maMenuItem.isSynced = true;
            maMenuItem.isDefault = DefaultState; // Use property
            maMenuItem.automaticValue = true;
            EditorUtility.SetDirty(maMenuItem);

            ModularAvatarMergeAnimator maMergeAnimator = Undo.AddComponent<ModularAvatarMergeAnimator>(TargetObject); // Use property
            maMergeAnimator.animator = animatorController;
            maMergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            maMergeAnimator.deleteAttachedAnimator = true;
            maMergeAnimator.pathMode = MergeAnimatorPathMode.Relative;
            maMergeAnimator.matchAvatarWriteDefaults = false;
            EditorUtility.SetDirty(maMergeAnimator);

            ModularAvatarMenuInstaller maMenuInstaller = Undo.AddComponent<ModularAvatarMenuInstaller>(TargetObject); // Use property
            EditorUtility.SetDirty(maMenuInstaller);

            AUToggleInfo info = Undo.AddComponent<AUToggleInfo>(TargetObject); // Use property
            info.parameterName = generatedParameterName;
            info.generatedFolderPath = itemSpecificFolderPath;
            EditorUtility.SetDirty(info);

            EditorUtility.SetDirty(onClip);
            EditorUtility.SetDirty(offClip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("AUToggle: Setup process completed successfully!");
            
            _completedObjectName = TargetObject.name; // Use property
            _completedParameterName = generatedParameterName;
            _completedFolderPath = itemSpecificFolderPath;
            
            _isSetupComplete = true;
            Repaint();
        }

        private enum AnimationPropertyType { IsActive, Enabled }

        private struct AnimationTarget
        {
            public GameObject targetGameObject; 
            public Type componentType;      
            public AnimationPropertyType propertyType;
            public string relativePath;
        }

        private List<AnimationTarget> GetAnimationTargets(GameObject rootTargetObject, bool isLC22_1)
        {
            List<AnimationTarget> targets = new List<AnimationTarget>();
            HashSet<GameObject> processedGameObjectsForIsActive = new HashSet<GameObject>();

            if (!isLC22_1)
            {
                Debug.Log($"[AUToggle] '{rootTargetObject.name}' is L.C.22-0. Targeting root m_IsActive.");
                targets.Add(new AnimationTarget
                {
                    targetGameObject = rootTargetObject,
                    componentType = typeof(GameObject),
                    propertyType = AnimationPropertyType.IsActive,
                    relativePath = ""
                });
                return targets;
            }

            Debug.Log($"[AUToggle] '{rootTargetObject.name}' is L.C.22-1. Analyzing child GameObjects and Components.");
            HashSet<GameObject> gameObjectsToToggleActive = new HashSet<GameObject>();
            List<Component> allRelevantComponents = new List<Component>();
            allRelevantComponents.AddRange(rootTargetObject.GetComponentsInChildren<Renderer>(true));
            allRelevantComponents.AddRange(rootTargetObject.GetComponentsInChildren<ParticleSystem>(true));
            allRelevantComponents.AddRange(rootTargetObject.GetComponentsInChildren<Light>(true));
            allRelevantComponents.AddRange(rootTargetObject.GetComponentsInChildren<Collider>(true));
            allRelevantComponents.AddRange(rootTargetObject.GetComponentsInChildren<Animator>(true)); 

            foreach (Component comp in allRelevantComponents.Distinct())
            {
                if (comp == null) continue;
                if (comp is Transform) continue;

                GameObject childGo = comp.gameObject;
                if (childGo == rootTargetObject) continue;

                bool onlyIEditorOnly = true;
                bool hasPrimaryTargetableComponent = false;
                foreach(Component c in childGo.GetComponents<Component>())
                {
                    if (c == null || c is Transform) continue;
                    if (c is Renderer || c is ParticleSystem || c is Light || c is Collider || c is Animator) {
                        hasPrimaryTargetableComponent = true;
                        break; 
                    }
                    if (!(c is IEditorOnly)) {
                        onlyIEditorOnly = false;
                    }
                }
                if (onlyIEditorOnly && !hasPrimaryTargetableComponent) {
                     Debug.Log($"[AUToggle] L.C.22-1: Skipping m_IsActive for '{childGo.name}' as it primarily contains IEditorOnly components without other direct animation targets.");
                     continue;
                }
                gameObjectsToToggleActive.Add(childGo);
            }

            foreach (GameObject go in gameObjectsToToggleActive)
            {
                if (!processedGameObjectsForIsActive.Contains(go))
                {
                    string path = AnimationUtility.CalculateTransformPath(go.transform, rootTargetObject.transform);
                    targets.Add(new AnimationTarget
                    {
                        targetGameObject = go,
                        componentType = typeof(GameObject),
                        propertyType = AnimationPropertyType.IsActive,
                        relativePath = path
                    });
                    processedGameObjectsForIsActive.Add(go);
                    Debug.Log($"[AUToggle] L.C.22-1: Targeting '{go.name}' (path: '{path}') for m_IsActive.");
                }
            }
            return targets.Distinct().ToList();
        }


        private void SetAnimationCurves(AnimationClip onClip, AnimationClip offClip, GameObject rootTargetObject)
        {
            Undo.RecordObject(onClip, "Set ON Clip Curves");
            Undo.RecordObject(offClip, "Set OFF Clip Curves");

            ClearAllCurves(onClip);
            ClearAllCurves(offClip);

            bool isComplexObject = CheckIfComplexObject(rootTargetObject);
            List<AnimationTarget> animationTargets = GetAnimationTargets(rootTargetObject, isComplexObject);

            foreach (var animTarget in animationTargets)
            {
                AnimationCurve onCurve = AnimationCurve.Constant(0, 0, 1f);
                AnimationCurve offCurve = AnimationCurve.Constant(0, 0, 0f);
                EditorCurveBinding binding;

                if (animTarget.propertyType == AnimationPropertyType.IsActive)
                {
                    binding = EditorCurveBinding.FloatCurve(animTarget.relativePath, typeof(GameObject), "m_IsActive");
                }
                else
                {
                    binding = EditorCurveBinding.FloatCurve(animTarget.relativePath, animTarget.componentType, "m_Enabled");
                }
                
                AnimationUtility.SetEditorCurve(onClip, binding, onCurve);
                AnimationUtility.SetEditorCurve(offClip, binding, offCurve);
            }
        }
        
        private bool CheckIfComplexObject(GameObject rootTargetObject)
        {
            if (rootTargetObject == null) return false;
            if (rootTargetObject.GetComponentInChildren<VRCPhysBone>(true) != null)
            {
                return true;
            }

            Transform[] children = rootTargetObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name.Equals("Armature", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // This method seems unused after recent changes to GetAnimationTargets, consider removing if confirmed.
        private List<Component> GetLC221TargetComponents(GameObject targetObject)
        {
            List<Component> componentsToAnimate = new List<Component>();
            componentsToAnimate.AddRange(targetObject.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            componentsToAnimate.AddRange(targetObject.GetComponentsInChildren<MeshRenderer>(true));
            componentsToAnimate.AddRange(targetObject.GetComponentsInChildren<ParticleSystem>(true));
            componentsToAnimate.AddRange(targetObject.GetComponentsInChildren<VRCPhysBone>(true));
            Component[] allComponents = targetObject.GetComponentsInChildren<Component>(true);
            foreach (Component component in allComponents)
            {
                if (component == null) continue;
                if (component is Transform) continue;
                if (component is IEditorOnly) continue;
                if (component is Renderer || component is ParticleSystem || component is VRCPhysBone) continue;
                if (!componentsToAnimate.Contains(component))
                {
                    if (component is Light || component is Collider || component is Behaviour)
                    {
                        componentsToAnimate.Add(component);
                    }
                }
            }
            return componentsToAnimate.Distinct().ToList();
        }
        
        private void ClearAllCurves(AnimationClip clip)
        {
            clip.ClearCurves();
        }
        // Removed DrawCompletionScreen, DrawSeparator, and other Draw* methods as they are now in AUToggleIMGUIRenderer
    }
}