using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components; // For VRCAvatarDescriptor
using System.Collections.Generic; // For List (if needed for DrawExistingMAComponentsWarning logic)

namespace SoryuShop.AUToggle
{
    public class AUToggleIMGUIRenderer
    {
        private AUToggleEditor _editorWindow;

        // Styles - These will be initialized by the editor window or passed in
        private GUIStyle _titleStyle;
        private GUIStyle _largeButtonStyle;
        private GUIStyle _toggleButtonStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _previewBoxStyle;

        // Scroll position
        private Vector2 _scrollPosition;

        public AUToggleIMGUIRenderer(AUToggleEditor editorWindow)
        {
            _editorWindow = editorWindow;
        }

        public void InitializeStyles(
            GUIStyle titleStyle, GUIStyle largeButtonStyle, GUIStyle toggleButtonStyle,
            GUIStyle headerStyle, GUIStyle sectionTitleStyle, GUIStyle previewBoxStyle)
        {
            _titleStyle = titleStyle;
            _largeButtonStyle = largeButtonStyle;
            _toggleButtonStyle = toggleButtonStyle;
            _headerStyle = headerStyle;
            _sectionTitleStyle = sectionTitleStyle;
            _previewBoxStyle = previewBoxStyle;
        }

        // Helper to create a Texture2D with a single color (Copied from AUToggleEditor)
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public void OnGUI()
        {
            // Styles should be initialized before this is called, e.g., in AUToggleEditor.OnEnable or OnGUI before calling this.
            // Or, ensure AUToggleEditor passes them in if they are instance-specific.
            // For simplicity, assuming styles are ready. If not, they would need to be initialized here or passed.

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (_editorWindow.IsSetupComplete) // Accessing property from editor window
            {
                DrawCompletionScreen();
            }
            else
            {
                DrawSettingsScreen();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsScreen()
        {
            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            DrawHeader();
            DrawSeparator();
            DrawInformationArea();
            DrawSeparator();

            bool canProceed = _editorWindow.AvatarDescriptor != null && _editorWindow.TargetObject != null;
            EditorGUI.BeginDisabledGroup(!canProceed);

            DrawSettingsArea();
            DrawSeparator();
            DrawPreviewArea();
            DrawSeparator();

            EditorGUI.EndDisabledGroup();

            bool isAvatarRoot = _editorWindow.TargetObject != null && _editorWindow.TargetObject.GetComponent<VRCAvatarDescriptor>() == _editorWindow.AvatarDescriptor;

            if (_editorWindow.TargetObject == null)
            {
                DrawWarningArea("対象オブジェクトが選択されていません。", MessageType.Error);
            }
            else if (isAvatarRoot)
            {
                DrawWarningArea("アバターのルートオブジェクトにはセットアップできません。\nアバター内のアイテムオブジェクトを選択してください。", MessageType.Error);
                canProceed = false;
            }
            else if (_editorWindow.AvatarDescriptor == null)
            {
                DrawWarningArea("選択されたオブジェクトはアバターの子供ではありません。\nアバター内のオブジェクトを選択してください。", MessageType.Warning);
            }

            if (canProceed && !isAvatarRoot)
            {
                DrawExistingMAComponentsWarning();
            }

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!canProceed);
            DrawFooterArea();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("AUToggle おーとぐる", _titleStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawExistingMAComponentsWarning()
        {
            if (_editorWindow.TargetObject == null) return;
            if (_editorWindow.TargetObject.GetComponent<AUToggleInfo>() != null) return;

            List<string> existingComponents = _editorWindow.GetExistingMAComponentNames(); // Delegate to editor window

            if (existingComponents.Count > 0)
            {
                string componentsList = string.Join(", ", existingComponents);
                DrawWarningArea(
                    $"対象オブジェクトには既に以下のMAコンポーネントが存在します:\n{componentsList}\n\n" +
                    "AUToggleはこれらのタイプのコンポーネントを新たに追加・設定します。\n" +
                    "意図しない動作や重複を避けるため、既存のコンポーネントを確認・調整するか、不要であれば削除することを推奨します。",
                    MessageType.Warning);
            }
        }

        private void DrawInformationArea()
        {
            GUILayout.Label("アバター情報", _sectionTitleStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(GUIContent.none, _editorWindow.AvatarDescriptor, typeof(VRCAvatarDescriptor), true);
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                if(_editorWindow.AvatarDescriptor != null) EditorGUIUtility.PingObject(_editorWindow.AvatarDescriptor.gameObject);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("対象オブジェクト情報", _sectionTitleStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(GUIContent.none, _editorWindow.TargetObject, typeof(GameObject), true);
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                if(_editorWindow.TargetObject != null) EditorGUIUtility.PingObject(_editorWindow.TargetObject);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettingsArea()
        {
            GUILayout.Label("設定項目", _headerStyle);

            _editorWindow.MenuName = EditorGUILayout.TextField(new GUIContent("表示名", "VRChatのExpressions Menuに表示される名前です。\nアバター内で分かりやすい名前をつけましょう。"), _editorWindow.MenuName);
            _editorWindow.Icon = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("アイコン", "メニューに表示するアイコン画像（テクスチャ）を指定します。\n空欄のままでも問題なく動作します。"), _editorWindow.Icon, typeof(Texture2D), false, GUILayout.Height(64));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("デフォルト状態", "アバターを読み込んだ時の、このアイテムの初期状態（表示されているか、非表示か）を選びます。"), GUILayout.Width(EditorGUIUtility.labelWidth - 7));

            Color originalBgColor = GUI.backgroundColor;
            GUI.backgroundColor = _editorWindow.DefaultState ? new Color(0.6f, 1f, 0.6f, 1f) : new Color(1f, 0.6f, 0.6f, 1f);

            string toggleText = _editorWindow.DefaultState ? "● デフォルトON" : "○ デフォルトOFF";
            if (GUILayout.Button(toggleText, _toggleButtonStyle, GUILayout.ExpandWidth(true)))
            {
                _editorWindow.DefaultState = !_editorWindow.DefaultState;
                GUI.FocusControl(null);
            }
            GUI.backgroundColor = originalBgColor;
            EditorGUILayout.EndHorizontal();

            _editorWindow.IsStateSaved = EditorGUILayout.Toggle(new GUIContent("状態を保存する", "ここにチェックを入れると、ワールド移動やゲームの再起動をしても、このメニューのON/OFF状態が記憶されます。\nチェックを外すと、アバターがリセットされる度に初期状態に戻ります。"), _editorWindow.IsStateSaved);
        }

        private void DrawPreviewArea()
        {
            GUILayout.Label("プレビュータイトル", _headerStyle);

            EditorGUILayout.BeginVertical(_previewBoxStyle);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(40));

            if (_editorWindow.Icon != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(40, 40, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                GUI.DrawTexture(iconRect, _editorWindow.Icon, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Space(40);
            }

            string previewName = string.IsNullOrEmpty(_editorWindow.MenuName) ?
                                 (_editorWindow.TargetObject != null ? _editorWindow.TargetObject.name : "アイテム名") :
                                 _editorWindow.MenuName;

            GUILayout.Label(new GUIContent(previewName), EditorStyles.label, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinWidth(0));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawWarningArea(string message, MessageType messageType)
        {
            EditorGUILayout.HelpBox(message, messageType);
        }

        private void DrawFooterArea()
        {
            Color setupButtonOriginalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f, 1f);
            if (GUILayout.Button(new GUIContent("Setup AUToggle", "現在の設定を確定し、アイテムの着脱メニューをアバターに自動でセットアップします。"), _largeButtonStyle))
            {
                _editorWindow.SetupLogic(); // Call method on editor window
            }
            GUI.backgroundColor = setupButtonOriginalColor;
        }

        private void DrawCompletionScreen()
        {
            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(20, 20, 20, 20), alignment = TextAnchor.MiddleCenter });
            GUILayout.FlexibleSpace();

            GUILayout.Label("セットアップ完了！", _titleStyle);
            EditorGUILayout.Space(15);

            EditorGUILayout.LabelField($"オブジェクト「{_editorWindow.CompletedObjectName}」への設定が完了しました。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("生成されたパラメータ名:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_editorWindow.CompletedParameterName, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("アセット保存先フォルダ:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_editorWindow.CompletedFolderPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space(5);
            if (GUILayout.Button("フォルダを開く", GUILayout.Height(25)))
            {
                EditorUtility.RevealInFinder(_editorWindow.CompletedFolderPath);
            }

            EditorGUILayout.Space(20);

            if (GUILayout.Button("閉じる", _largeButtonStyle))
            {
                _editorWindow.CloseWindow(); // Call method on editor window
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawSeparator(int height = 1, int spaceBefore = 5, int spaceAfter = 10)
        {
            GUILayout.Space(spaceBefore);
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            rect.height = height;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            GUILayout.Space(spaceAfter);
        }
    }
}