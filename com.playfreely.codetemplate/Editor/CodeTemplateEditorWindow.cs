#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace PlayFreelyXiYou.GameEditor
{
    public sealed class CodeTemplateEditorWindow : EditorWindow
    {
        private const string MENU_PATH = "Tools/代码CV战士";
        private const string PRIMARY_LIBRARY_ASSET_PATH = "Assets/Code/Editor/Windows/CodeTemplateLibrary.asset";

        private const string LEGACY_LIBRARY_ASSET_PATH =
            "Assets/Code/Editor/CreatUIAndPrefab/CopyScriptsTool/CodeTemplateLibrary.asset";

        private static readonly Color SelectedButtonColor = new Color(0.27f, 0.52f, 0.85f);

        private static readonly string[] CandidateLibraryPaths =
        {
            PRIMARY_LIBRARY_ASSET_PATH,
            LEGACY_LIBRARY_ASSET_PATH
        };


        private CodeTemplateLibrary library;

        private Vector2 leftScroll;
        private Vector2 codeScroll;
        private int selectedIndex = -1;

        private SearchField _searchField;
        private string searchText = string.Empty;
        private readonly List<int> filteredIndices = new List<int>();
        private readonly List<Rect> filteredButtonRects = new List<Rect>();

        private const double LongPressThreshold = 0.35d;
        private int pressedIndex = -1;
        private int pressedDisplayIndex = -1;
        private double pressStartTime;
        private bool longPressTriggered;

        private bool isReordering;
        private int reorderInitialDisplayIndex = -1;
        private int reorderTargetDisplayIndex = -1;
        private float reorderPointerY;
        private bool reorderPointerUpdated;
        private bool reorderChangedDuringDrag;

        private bool hasUnsavedChanges;

        private string statusMessage = string.Empty;
        private double statusMessageTick;

        private GUIStyle codeStyle;
        private GUIStyle highlightStyle;
        private GUIStyle leftButtonStyle;
        private GUIStyle selectedButtonStyle;
        private GUIStyle lineNumberStyle;

        private Texture2D selectedButtonTexture;
        private Texture2D codeBackgroundTexture;

        private string cachedHighlightSource = string.Empty;
        private string cachedHighlightResult = string.Empty;
        private readonly GUIContent highlightContent = new GUIContent();

        private const float LineNumberWidth = 46f;
        private const float CodeEditorMinimumHeight = 240f;
        private const float ReservedHeightForControls = 220f;

        private const string KeywordColorHex = "#569CD6";
        private const string TypeColorHex = "#4EC9B0";
        private const string FuncColorHex = "#38CB97";
        private const string StringColorHex = "#D69D85";
        private const string CommentColorHex = "#6A9955";
        private const string NumberColorHex = "#B5CEA8";
        private const string PreprocessorColorHex = "#C586C0";

        private const char ZeroWidthSpace = '\u200B';

        private static readonly HashSet<string> KeywordSet = new HashSet<string>
        {
            "abstract", "as", "base", "break", "case", "catch", "checked", "class", "const", "continue", "default",
            "delegate", "do", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "for",
            "foreach", "goto", "if", "implicit", "in", "interface", "internal", "is", "lock", "namespace",
            "new", "null", "operator", "out", "override", "params", "private", "protected", "public", "readonly",
            "ref", "return", "sealed", "sizeof", "stackalloc", "static", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "unchecked", "unsafe", "using", "virtual", "volatile", "while", "HotfixEntry",
            "PlayFreelyXiYouGameData", "void", "List","Dictionary"
        };

        private static readonly HashSet<string> TypeKeywordSet = new HashSet<string>
        {
            "bool", "byte", "char", "decimal", "double", "float", "int", "long", "object", "sbyte", "short",
            "string", "uint", "ulong", "ushort", "var", "dynamic", "Event", "Instance"
        };

        private static readonly HashSet<string> FuncKeywordSet = new HashSet<string>
        {
            "OnInit", "OnDestroyObject", "UpdateUserProperty", "OnOpen", "OnClose", "OpenTipsPopUpWindow",
            "OpenHelpUIForm"
        };

        [MenuItem(MENU_PATH)]
        private static void Open()
        {
            var window = GetWindow<CodeTemplateEditorWindow>();
            window.titleContent = new GUIContent("代码CV战士");
            window.minSize = new Vector2(840f, 520f);
            window.Show();
        }

        protected void OnEnable()
        {
            if (_searchField == null)
                _searchField = new SearchField();

            minSize = new Vector2(840f, 520f);
            TryLoadLibrary();
            PrepareStyles();
            EnsureSelectionValid();
        }

        protected void OnDisable()
        {
            if (hasUnsavedChanges)
            {
                SaveLibrary(false);
            }

            if (selectedButtonTexture != null)
            {
                DestroyImmediate(selectedButtonTexture);
                selectedButtonTexture = null;
            }

            if (codeBackgroundTexture != null)
            {
                DestroyImmediate(codeBackgroundTexture);
                codeBackgroundTexture = null;
            }

            selectedButtonStyle = null;
            leftButtonStyle = null;
            codeStyle = null;
            highlightStyle = null;
            lineNumberStyle = null;
            cachedHighlightSource = string.Empty;
            cachedHighlightResult = string.Empty;
            highlightContent.text = string.Empty;
        }

        private void TryLoadLibrary()
        {
            if (library != null)
            {
                library.EnsureInitialized();
                return;
            }

            foreach (string path in CandidateLibraryPaths)
            {
                library = CodeTemplateLibrary.LoadExistingLibrary(path);
                if (library != null)
                {
                    return;
                }
            }

            library = CodeTemplateLibrary.LoadOrCreateLibrary(PRIMARY_LIBRARY_ASSET_PATH);
            library?.EnsureInitialized();
        }

        private bool EnsureLibraryReady()
        {
            TryLoadLibrary();
            return library != null;
        }

        private void PrepareStyles()
        {
            if (codeStyle == null)
            {
                codeStyle = new GUIStyle(EditorStyles.textArea)
                {
                    fontSize = 13,
                    wordWrap = false,
                    richText = false
                };

                Font monoFont = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;
                if (monoFont == null)
                {
                    monoFont = Font.CreateDynamicFontFromOSFont("Consolas", 13);
                }

                if (monoFont != null)
                {
                    codeStyle.font = monoFont;
                }

                if (codeBackgroundTexture == null)
                {
                    codeBackgroundTexture = CreateSolidTexture(new Color(100f, 100f, 100f, 0f));
                }

                ApplyBackgroundTexture(codeStyle, codeBackgroundTexture);
                //codeStyle.cursorColor = new Color(1f, 1f, 1f, 0.9f);
                //codeStyle.selectionColor = new Color(0.25f, 0.49f, 0.9f, 0.35f);
                codeStyle.padding = new RectOffset(6, 6, 6, 6);
                codeStyle.stretchWidth = true;
                codeStyle.stretchHeight = true;
                ApplyTextColor(codeStyle, Color.clear);
            }

            if (highlightStyle == null)
            {
                highlightStyle = new GUIStyle(codeStyle)
                {
                    richText = true,
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };
                ApplyTextColor(highlightStyle, EditorStyles.label.normal.textColor);
                ClearBackgrounds(highlightStyle);
            }


            if (leftButtonStyle == null)
            {
                leftButtonStyle = new GUIStyle(EditorStyles.miniButtonLeft)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 13,
                    fixedHeight = 40f,
                    stretchWidth = true,
                    padding = new RectOffset(12, 8, 6, 6)
                };
            }

            if (selectedButtonStyle == null)
            {
                selectedButtonStyle = new GUIStyle(leftButtonStyle);
                selectedButtonTexture = CreateSolidTexture(SelectedButtonColor);
                ApplySelectedButtonStyle(selectedButtonStyle, selectedButtonTexture);
            }

            if (lineNumberStyle == null)
            {
                lineNumberStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.UpperRight,
                    fontSize = 12,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
                };
                Font monoFont = codeStyle.font;
                if (monoFont != null)
                {
                    lineNumberStyle.font = monoFont;
                }
            }
        }

        protected void OnGUI()
        {
            if (!EnsureLibraryReady())
            {
                EditorGUILayout.HelpBox("模板资源加载失败，请检查资源路径是否存在。", MessageType.Error);
                return;
            }

            PrepareStyles();
            UpdateFilteredIndices();
            EnsureSelectionValid();
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            {
                DrawLeftPanel();
                DrawSeparator();
                DrawRightPanel();
            }
            EditorGUILayout.EndHorizontal();

            HandleMouseLeaveWindow();
            HandleGlobalMouseUp();
            DrawStatusBar();
        }


        private void DrawToolbar()
        {
            GUILayout.Space(10f);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            if (GUILayout.Button("＋ 新增功能模板", EditorStyles.miniButton, GUILayout.Height(34f), GUILayout.Width(210f)))
            {
                AddTemplate();
                UpdateFilteredIndices();
                EnsureSelectionValid();
                Repaint();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            EditorGUI.BeginChangeCheck();

            // 在工具条上绘制搜索框
            string newSearch = _searchField.OnToolbarGUI(
                searchText ?? string.Empty,
                GUILayout.Width(220f));

            if (EditorGUI.EndChangeCheck())
            {
                searchText = newSearch ?? string.Empty;
                CancelReorder(true);
                UpdateFilteredIndices();
                EnsureSelectionValid();
                Repaint();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            EditorGUILayout.EndVertical();
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(260f));
            GUILayout.Space(6f);
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.ExpandHeight(true));
            filteredButtonRects.Clear();
            for (int i = 0; i < filteredIndices.Count; i++)
            {
                filteredButtonRects.Add(DrawTemplateButton(filteredIndices[i], i));
            }

            EditorGUILayout.EndScrollView();
            CheckLongPressActivation();
            ProcessReorderPointer();
            EditorGUILayout.EndVertical();
        }

        private Rect DrawTemplateButton(int index, int displayIndex)
        {
            var template = library.Templates[index];
            if (template == null)
            {
                template = new CodeTemplateLibrary.TemplateInfo();
                library.Templates[index] = template;
                MarkLibraryDirty();
                SaveLibrary(false);
            }

            string displayName = template.TemplateName;
            if (string.IsNullOrEmpty(displayName))
            {
                displayName = $"模板 {index + 1}";
            }

            GUIStyle style = index == selectedIndex ? selectedButtonStyle ?? leftButtonStyle : leftButtonStyle;
            Rect rect = EditorGUILayout.GetControlRect(false, style.fixedHeight, GUILayout.ExpandWidth(true));
            bool isHover = rect.Contains(Event.current.mousePosition);
            bool isDraggingItem = isReordering && reorderInitialDisplayIndex == displayIndex;
            Color backup = GUI.color;
            if (isDraggingItem)
            {
                GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, 0.65f);
            }

            if (Event.current.type == EventType.Repaint)
            {
                style.Draw(rect, new GUIContent(displayName), isHover, false, index == selectedIndex, false);
            }

            GUI.color = backup;
            HandleTemplateButtonEvents(index, displayIndex, rect);
            GUILayout.Space(8f);
            return rect;
        }

        private void HandleTemplateButtonEvents(int actualIndex, int displayIndex, Rect rect)
        {
            Event evt = Event.current;
            if (evt == null)
            {
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                pressedIndex = actualIndex;
                pressedDisplayIndex = displayIndex;
                pressStartTime = EditorApplication.timeSinceStartup;
                longPressTriggered = false;
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && isReordering &&
                reorderInitialDisplayIndex == displayIndex)
            {
                reorderPointerY = evt.mousePosition.y;
                reorderPointerUpdated = true;
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseUp && evt.button == 0 && pressedIndex == actualIndex)
            {
                if (isReordering && longPressTriggered && reorderInitialDisplayIndex == displayIndex)
                {
                    FinishReorder();
                }
                else if (!longPressTriggered && rect.Contains(evt.mousePosition))
                {
                    SelectIndex(actualIndex);
                    CancelReorder(true);
                }
                else
                {
                    CancelReorder(true);
                }

                evt.Use();
            }
        }

        private void CheckLongPressActivation()
        {
            if (pressedIndex < 0 || longPressTriggered || isReordering)
            {
                return;
            }

            if (IsFilteringActive())
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - pressStartTime < LongPressThreshold)
            {
                return;
            }

            if (pressedDisplayIndex < 0 || pressedDisplayIndex >= filteredButtonRects.Count)
            {
                return;
            }

            StartReorder(pressedDisplayIndex);
        }

        private void ProcessReorderPointer()
        {
            if (!isReordering)
            {
                return;
            }

            if (filteredButtonRects.Count == 0)
            {
                return;
            }

            if (reorderPointerUpdated)
            {
                int newTargetIndex = CalculateReorderTargetIndex(reorderPointerY);
                if (newTargetIndex != reorderTargetDisplayIndex)
                {
                    bool reordered = TryApplyLiveReorder(newTargetIndex);
                    if (reordered)
                    {
                        Repaint();
                        reorderPointerUpdated = false;
                        return;
                    }
                    else
                    {
                        reorderTargetDisplayIndex = Mathf.Clamp(newTargetIndex, 0, filteredButtonRects.Count);
                    }
                }

                reorderPointerUpdated = false;
            }

            DrawReorderIndicator();
        }

        private bool TryApplyLiveReorder(int newTargetDisplayIndex)
        {
            if (!EnsureLibraryReady())
            {
                return false;
            }

            if (!isReordering || library.Templates.Count == 0)
            {
                return false;
            }

            if (IsFilteringActive())
            {
                return false;
            }

            if (!ApplyReorderMove(newTargetDisplayIndex))
            {
                return false;
            }

            return true;
        }

        private int CalculateReorderTargetIndex(float pointerY)
        {
            int count = filteredButtonRects.Count;
            if (count == 0)
            {
                return 0;
            }

            for (int i = 0; i < count; i++)
            {
                Rect rect = filteredButtonRects[i];
                float mid = rect.y + rect.height * 0.5f;
                if (pointerY <= mid)
                {
                    return i;
                }
            }

            return count;
        }

        private void DrawReorderIndicator()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (reorderInitialDisplayIndex < 0 || reorderTargetDisplayIndex < 0)
            {
                return;
            }

            if (filteredButtonRects.Count == 0)
            {
                return;
            }

            Rect lineRect;
            if (reorderTargetDisplayIndex >= filteredButtonRects.Count)
            {
                Rect lastRect = filteredButtonRects[filteredButtonRects.Count - 1];
                lineRect = new Rect(lastRect.x, lastRect.yMax + 2f, lastRect.width, 2f);
            }
            else
            {
                Rect targetRect =
                    filteredButtonRects[Mathf.Clamp(reorderTargetDisplayIndex, 0, filteredButtonRects.Count - 1)];
                lineRect = new Rect(targetRect.x, targetRect.y - 2f, targetRect.width, 2f);
            }

            EditorGUI.DrawRect(lineRect, new Color(0.32f, 0.62f, 0.92f, 0.9f));
        }

        private void DrawSeparator()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(4f));
            Rect rect = GUILayoutUtility.GetRect(2f, position.height, GUILayout.Width(2f),
                GUILayout.ExpandHeight(true));
            rect.width = 1.5f;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1f));
            EditorGUILayout.EndVertical();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(6f);
            if (selectedIndex < 0 || selectedIndex >= library.Templates.Count)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.HelpBox("请选择左侧的功能模板或点击上方按钮新增。", MessageType.Info);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            var template = GetSelectedTemplate();

            EditorGUILayout.LabelField("功能名字", EditorStyles.boldLabel);
            GUI.SetNextControlName("TemplateNameField");
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(template.TemplateName ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(library, "修改模板名字");
                template.TemplateName = newName;
                MarkLibraryDirty();
            }

            GUILayout.Space(10f);
            EditorGUILayout.LabelField("代码内容", EditorStyles.boldLabel);
            DrawCodeEditor(template);

            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制代码", GUILayout.Width(100f), GUILayout.Height(28f)))
            {
                CopyCode();
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("格式化代码", GUILayout.Width(110f), GUILayout.Height(28f)))
            {
                FormatCode();
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
            {
                if (GUILayout.Button("保存修改", GUILayout.Width(120f), GUILayout.Height(28f)))
                {
                    SaveLibrary();
                }
            }

            Color backup = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.82f, 0.3f, 0.3f);
            if (GUILayout.Button("删除模板", GUILayout.Width(110f), GUILayout.Height(28f)))
            {
                DeleteTemplate();
            }

            GUI.backgroundColor = backup;
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusBar()
        {
            if (string.IsNullOrEmpty(statusMessage))
            {
                return;
            }

            if (EditorApplication.timeSinceStartup - statusMessageTick > 3d)
            {
                statusMessage = string.Empty;
                return;
            }

            GUILayout.Space(4f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        private void AddTemplate()
        {
            if (!EnsureLibraryReady())
            {
                ShowStatus("模板资源加载失败，无法新增模板。");
                return;
            }

            Undo.RecordObject(library, "新增代码模板");
            string newName = GenerateUniqueName();
            var data = new CodeTemplateLibrary.TemplateInfo
            {
                TemplateName = newName,
                CodeContent = "// 在此输入模板代码\n"
            };
            library.Templates.Add(data);
            MarkLibraryDirty();
            SaveLibrary(false);
            SelectIndex(library.Templates.Count - 1);
            ShowStatus($"已创建模板：{newName}");
        }

        private string GenerateUniqueName()
        {
            if (!EnsureLibraryReady())
            {
                return "新建功能";
            }

            int index = library.Templates.Count + 1;
            HashSet<string> names = new HashSet<string>();
            foreach (var template in library.Templates)
            {
                if (template != null)
                {
                    names.Add(template.TemplateName);
                }
            }

            string candidate = $"新建功能{index}";
            while (names.Contains(candidate))
            {
                index++;
                candidate = $"新建功能{index}";
            }

            return candidate;
        }

        private void SelectIndex(int index)
        {
            if (!EnsureLibraryReady())
            {
                return;
            }

            selectedIndex = index;
            codeScroll = Vector2.zero;
            GUI.FocusControl(null);
        }

        private void StartReorder(int displayIndex)
        {
            if (IsFilteringActive())
            {
                return;
            }

            if (displayIndex < 0 || displayIndex >= filteredButtonRects.Count)
            {
                return;
            }

            longPressTriggered = true;
            isReordering = true;
            reorderInitialDisplayIndex = displayIndex;
            reorderTargetDisplayIndex = displayIndex;
            reorderChangedDuringDrag = false;
            Rect rect = filteredButtonRects[displayIndex];
            reorderPointerY = rect.center.y;
            reorderPointerUpdated = true;
            Repaint();
        }

        private void FinishReorder()
        {
            if (!EnsureLibraryReady())
            {
                CancelReorder(true);
                return;
            }

            if (!isReordering || !longPressTriggered)
            {
                CancelReorder(true);
                return;
            }

            if (IsFilteringActive())
            {
                CancelReorder(true);
                return;
            }

            bool reordered = ApplyReorderMove(reorderTargetDisplayIndex);
            if (reordered || reorderChangedDuringDrag)
            {
                SaveLibrary(false);
                ShowStatus("模板顺序已调整");
            }

            CancelReorder(true);
        }

        private void CancelReorder(bool clearPress)
        {
            isReordering = false;
            longPressTriggered = false;
            reorderInitialDisplayIndex = -1;
            reorderTargetDisplayIndex = -1;
            reorderPointerUpdated = false;
            reorderChangedDuringDrag = false;
            if (clearPress)
            {
                pressedIndex = -1;
                pressedDisplayIndex = -1;
            }

            Repaint();
        }

        private bool ApplyReorderMove(int targetDisplayIndex)
        {
            if (!EnsureLibraryReady())
            {
                return false;
            }

            int count = library.Templates.Count;
            if (count == 0 || reorderInitialDisplayIndex < 0)
            {
                return false;
            }

            int fromIndex = Mathf.Clamp(reorderInitialDisplayIndex, 0, count - 1);
            int insertionIndex = Mathf.Clamp(targetDisplayIndex, 0, count);

            if (insertionIndex > fromIndex)
            {
                insertionIndex--;
            }

            insertionIndex = Mathf.Clamp(insertionIndex, 0, library.Templates.Count);
            if (insertionIndex == fromIndex)
            {
                return false;
            }

            Undo.RecordObject(library, "调整模板顺序");
            var template = library.Templates[fromIndex];
            library.Templates.RemoveAt(fromIndex);
            library.Templates.Insert(insertionIndex, template);

            reorderInitialDisplayIndex = insertionIndex;
            reorderTargetDisplayIndex = insertionIndex;
            pressedIndex = insertionIndex;
            pressedDisplayIndex = insertionIndex;
            selectedIndex = insertionIndex;

            MarkLibraryDirty();
            UpdateFilteredIndices();
            reorderChangedDuringDrag = true;
            return true;
        }

        private void UpdateFilteredIndices()
        {
            filteredIndices.Clear();
            if (!EnsureLibraryReady())
            {
                return;
            }

            string filter = searchText ?? string.Empty;
            string trimmed = filter.Trim();
            bool hasFilter = !string.IsNullOrEmpty(trimmed);
            StringComparison comparison = StringComparison.OrdinalIgnoreCase;

            for (int i = 0; i < library.Templates.Count; i++)
            {
                var template = library.Templates[i];
                if (template == null)
                {
                    template = new CodeTemplateLibrary.TemplateInfo();
                    library.Templates[i] = template;
                    MarkLibraryDirty();
                    SaveLibrary(false);
                }

                if (!hasFilter)
                {
                    filteredIndices.Add(i);
                    continue;
                }

                string name = template.TemplateName ?? string.Empty;
                if (name.IndexOf(trimmed, comparison) >= 0)
                {
                    filteredIndices.Add(i);
                }
            }
        }

        private bool IsFilteringActive()
        {
            return !string.IsNullOrEmpty((searchText ?? string.Empty).Trim());
        }

        private void HandleMouseLeaveWindow()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.MouseLeaveWindow)
            {
                return;
            }

            if (pressedIndex >= 0 || isReordering)
            {
                CancelReorder(true);
            }
        }

        private void HandleGlobalMouseUp()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.MouseUp || evt.button != 0)
            {
                return;
            }

            if (pressedIndex < 0 && !isReordering)
            {
                return;
            }

            if (isReordering && longPressTriggered)
            {
                FinishReorder();
            }
            else
            {
                CancelReorder(true);
            }

            evt.Use();
        }

        private void EnsureSelectionValid()
        {
            if (!EnsureLibraryReady())
            {
                selectedIndex = -1;
                return;
            }

            if (library.Templates.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            if (IsFilteringActive())
            {
                if (filteredIndices.Count == 0)
                {
                    selectedIndex = -1;
                    return;
                }

                if (!filteredIndices.Contains(selectedIndex))
                {
                    selectedIndex = filteredIndices[0];
                }
            }
            else if (selectedIndex < 0 || selectedIndex >= library.Templates.Count)
            {
                selectedIndex = 0;
            }

            if (selectedIndex < 0 || selectedIndex >= library.Templates.Count)
            {
                return;
            }

            var template = library.Templates[selectedIndex];
            if (template == null)
            {
                template = new CodeTemplateLibrary.TemplateInfo();
                library.Templates[selectedIndex] = template;
                MarkLibraryDirty();
                SaveLibrary(false);
            }
        }


        private CodeTemplateLibrary.TemplateInfo GetSelectedTemplate()
        {
            if (!EnsureLibraryReady() || selectedIndex < 0 || selectedIndex >= library.Templates.Count)
            {
                return null;
            }

            var template = library.Templates[selectedIndex];
            if (template == null)
            {
                template = new CodeTemplateLibrary.TemplateInfo();
                library.Templates[selectedIndex] = template;
                MarkLibraryDirty();
            }

            return template;
        }

        private void DeleteTemplate()
        {
            if (!EnsureLibraryReady() || selectedIndex < 0 || selectedIndex >= library.Templates.Count)
            {
                return;
            }

            Undo.RecordObject(library, "删除代码模板");
            string removedName = library.Templates[selectedIndex].TemplateName;
            library.Templates.RemoveAt(selectedIndex);
            MarkLibraryDirty();
            SaveLibrary(false);
            UpdateFilteredIndices();
            EnsureSelectionValid();
            ShowStatus(string.IsNullOrEmpty(removedName) ? "模板已删除" : $"已删除模板：{removedName}");
        }

        private void CopyCode()
        {
            var template = GetSelectedTemplate();
            if (template == null)
            {
                ShowStatus("未找到可复制的模板内容。");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = template.CodeContent ?? string.Empty;
            ShowStatus("代码已复制到剪贴板");
        }

        private void FormatCode()
        {
            var template = GetSelectedTemplate();
            if (template == null)
            {
                ShowStatus("未找到可格式化的模板内容。");
                return;
            }

            string original = template.CodeContent ?? string.Empty;
            string normalizedOriginal = original.Replace("\r\n", "\n").Replace('\r', '\n');
            string formatted = FormatCSharpCode(normalizedOriginal);
            if (formatted == normalizedOriginal)
            {
                ShowStatus("代码已是规范格式");
                return;
            }

            Undo.RecordObject(library, "格式化模板代码");
            template.CodeContent = formatted;
            MarkLibraryDirty();
            EnsureHighlightCache(formatted);
            codeScroll = Vector2.zero;
            ShowStatus("代码已格式化");
        }


        private void DrawCodeEditor(CodeTemplateLibrary.TemplateInfo template)
        {
            float codeEditorHeight = Mathf.Max(CodeEditorMinimumHeight, position.height - ReservedHeightForControls);
            string currentCode = template.CodeContent ?? string.Empty;
            GUIContent codeContent = new GUIContent(string.IsNullOrEmpty(currentCode) ? " " : currentCode);
            float availableWidth = Mathf.Max(120f, position.width - LineNumberWidth - 60f);
            float calculatedHeight = codeStyle.CalcHeight(codeContent, availableWidth);
            float contentHeight = Mathf.Max(codeEditorHeight, calculatedHeight);

            codeScroll = EditorGUILayout.BeginScrollView(codeScroll, GUILayout.Height(codeEditorHeight));
            EditorGUILayout.BeginHorizontal();

            int lineCount = Mathf.Max(1, CountLines(currentCode));
            using (new EditorGUI.DisabledScope(true))
            {
                string lineNumbers = GenerateLineNumbers(lineCount);
                GUILayout.Label(lineNumbers, lineNumberStyle, GUILayout.Width(LineNumberWidth),
                    GUILayout.Height(contentHeight));
            }

            EnsureHighlightCache(currentCode);

            Rect codeRect = GUILayoutUtility.GetRect(codeContent, codeStyle, GUILayout.Height(contentHeight),
                GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.Label(codeRect, highlightContent, highlightStyle);
            }

            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName("TemplateCodeField");
            string newCode = EditorGUI.TextArea(codeRect, currentCode, codeStyle);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(library, "修改模板代码");
                template.CodeContent = newCode;
                MarkLibraryDirty();
                EnsureHighlightCache(newCode);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private static int CountLines(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return 1;
            }

            int count = 1;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }

        private static string FormatCSharpCode(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            string normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            StringBuilder builder = new StringBuilder(source.Length + 64);
            int indentLevel = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (trimmed.Length == 0)
                {
                    if (i < lines.Length - 1)
                    {
                        builder.Append('\n');
                    }

                    continue;
                }

                int leadingClosings = CountLeadingCharacter(trimmed, '}');
                bool isCaseLine = trimmed.StartsWith("case ", StringComparison.Ordinal) ||
                                  trimmed.StartsWith("default:", StringComparison.Ordinal);
                int indent = indentLevel - leadingClosings - (isCaseLine ? 1 : 0);
                indent = Mathf.Max(0, indent);

                builder.Append(' ', indent * 4);
                builder.Append(trimmed);
                if (i < lines.Length - 1)
                {
                    builder.Append('\n');
                }

                int openingCount = CountCharacter(trimmed, '{');
                int closingCount = CountCharacter(trimmed, '}');
                int trailingClosings = Mathf.Max(0, closingCount - leadingClosings);
                int nextIndent = indent + openingCount - trailingClosings;
                if (isCaseLine)
                {
                    nextIndent = Mathf.Max(nextIndent, indent + 1);
                }

                indentLevel = Mathf.Max(0, nextIndent);
            }

            return builder.ToString();
        }

        private static int CountLeadingCharacter(string value, char character)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == character)
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            return count;
        }

        private static int CountCharacter(string value, char character)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == character)
                {
                    count++;
                }
            }

            return count;
        }

        private static string GenerateLineNumbers(int lineCount)
        {
            if (lineCount <= 0)
            {
                return "1";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 1; i <= lineCount; i++)
            {
                builder.Append(i);
                if (i < lineCount)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private void ShowStatus(string message)
        {
            statusMessage = message;
            statusMessageTick = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void MarkLibraryDirty()
        {
            if (library == null)
            {
                return;
            }

            EditorUtility.SetDirty(library);
            hasUnsavedChanges = true;
        }

        private void SaveLibrary(bool showStatus = true)
        {
            if (library == null)
            {
                return;
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            hasUnsavedChanges = false;
            if (showStatus)
            {
                ShowStatus("模板已保存");
            }
        }

        private static void ApplyTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static void ClearBackgrounds(GUIStyle style)
        {
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.onNormal.background = null;
            style.onHover.background = null;
            style.onActive.background = null;
            style.onFocused.background = null;
        }

        private void EnsureHighlightCache(string source)
        {
            if (cachedHighlightSource == source)
            {
                return;
            }

            cachedHighlightSource = source;
            cachedHighlightResult = BuildHighlightedRichText(source);
            highlightContent.text = cachedHighlightResult;
        }

        private static string BuildHighlightedRichText(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(source.Length * 2);
            int length = source.Length;
            int index = 0;
            while (index < length)
            {
                char current = source[index];

                if (current == '/' && index + 1 < length)
                {
                    if (source[index + 1] == '/')
                    {
                        int start = index;
                        index += 2;
                        while (index < length && source[index] != '\n' && source[index] != '\r')
                        {
                            index++;
                        }

                        AppendColored(builder, source.Substring(start, index - start), CommentColorHex);
                        continue;
                    }

                    if (source[index + 1] == '*')
                    {
                        int start = index;
                        index += 2;
                        while (index + 1 < length && !(source[index] == '*' && source[index + 1] == '/'))
                        {
                            index++;
                        }

                        if (index + 1 < length)
                        {
                            index += 2;
                        }
                        else
                        {
                            index = length;
                        }

                        AppendColored(builder, source.Substring(start, index - start), CommentColorHex);
                        continue;
                    }
                }

                if (current == '#' && (index == 0 || source[index - 1] == '\n' || source[index - 1] == '\r'))
                {
                    int start = index;
                    index++;
                    while (index < length && source[index] != '\n' && source[index] != '\r')
                    {
                        index++;
                    }

                    AppendColored(builder, source.Substring(start, index - start), PreprocessorColorHex);
                    continue;
                }

                if (current == '@' && index + 1 < length && source[index + 1] == '"')
                {
                    int start = index;
                    index += 2;
                    while (index < length)
                    {
                        if (source[index] == '"')
                        {
                            index++;
                            if (index < length && source[index] == '"')
                            {
                                index++;
                                continue;
                            }

                            break;
                        }

                        index++;
                    }

                    AppendColored(builder, source.Substring(start, index - start), StringColorHex);
                    continue;
                }

                if (current == '"')
                {
                    int start = index;
                    index++;
                    bool closed = false;
                    while (index < length)
                    {
                        char ch = source[index];
                        if (ch == '\\')
                        {
                            index += 2;
                            continue;
                        }

                        if (ch == '"')
                        {
                            index++;
                            closed = true;
                            break;
                        }

                        index++;
                    }

                    if (!closed)
                    {
                        index = length;
                    }

                    AppendColored(builder, source.Substring(start, index - start), StringColorHex);
                    continue;
                }

                if (current == '\'')
                {
                    int start = index;
                    index++;
                    if (index < length && source[index] == '\\')
                    {
                        index += 2;
                    }
                    else
                    {
                        index++;
                    }

                    AppendColored(builder, source.Substring(start, Mathf.Min(index, length) - start), StringColorHex);
                    continue;
                }

                if (char.IsDigit(current))
                {
                    int start = index;
                    bool hasDecimalPoint = false;
                    bool hasExponent = false;
                    bool isHex = false;
                    if (current == '0' && index + 1 < length && (source[index + 1] == 'x' || source[index + 1] == 'X'))
                    {
                        isHex = true;
                        index += 2;
                        while (index < length && IsHexDigit(source[index]))
                        {
                            index++;
                        }
                    }
                    else
                    {
                        index++;
                        while (index < length)
                        {
                            char ch = source[index];
                            if (char.IsDigit(ch))
                            {
                                index++;
                                continue;
                            }

                            if (!isHex && ch == '.' && !hasDecimalPoint)
                            {
                                hasDecimalPoint = true;
                                index++;
                                continue;
                            }

                            if (!isHex && (ch == 'e' || ch == 'E') && !hasExponent)
                            {
                                hasExponent = true;
                                index++;
                                if (index < length && (source[index] == '+' || source[index] == '-'))
                                {
                                    index++;
                                }

                                continue;
                            }

                            break;
                        }
                    }

                    while (index < length && IsNumericSuffix(source[index]))
                    {
                        index++;
                    }

                    AppendColored(builder, source.Substring(start, index - start), NumberColorHex);
                    continue;
                }

                if (IsIdentifierStart(current))
                {
                    int start = index;
                    bool escaped = current == '@';
                    if (escaped)
                    {
                        index++;
                    }

                    while (index < length && IsIdentifierPart(source[index]))
                    {
                        index++;
                    }

                    string token = source.Substring(start + (escaped ? 1 : 0), index - start - (escaped ? 1 : 0));
                    string fullToken = source.Substring(start, index - start);
                    if (KeywordSet.Contains(token))
                    {
                        AppendColored(builder, fullToken, KeywordColorHex);
                        continue;
                    }

                    if (TypeKeywordSet.Contains(token))
                    {
                        AppendColored(builder, fullToken, TypeColorHex);
                        continue;
                    }

                    if (FuncKeywordSet.Contains(token))
                    {
                        AppendColored(builder, fullToken, FuncColorHex);
                        continue;
                    }

                    AppendEscaped(builder, fullToken);
                    continue;
                }

                AppendEscaped(builder, current);
                index++;
            }

            return builder.ToString();
        }

        private static void AppendColored(StringBuilder builder, string content, string colorHex)
        {
            builder.Append("<color=").Append(colorHex).Append('>');
            AppendEscaped(builder, content);
            builder.Append("</color>");
        }

        private static void AppendEscaped(StringBuilder builder, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            for (int i = 0; i < content.Length; i++)
            {
                AppendEscaped(builder, content[i]);
            }
        }

        private static void AppendEscaped(StringBuilder builder, char character)
        {
            switch (character)
            {
                case '<':
                    builder.Append('<');
                    builder.Append(ZeroWidthSpace);
                    break;
                case '>':
                    builder.Append(ZeroWidthSpace);
                    builder.Append('>');
                    break;
                case '&':
                    builder.Append("&amp;");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        private static bool IsIdentifierStart(char character)
        {
            return character == '@' || character == '_' || char.IsLetter(character);
        }

        private static bool IsIdentifierPart(char character)
        {
            return character == '_' || char.IsLetterOrDigit(character);
        }

        private static bool IsHexDigit(char character)
        {
            return (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') ||
                   (character >= 'A' && character <= 'F');
        }

        private static bool IsNumericSuffix(char character)
        {
            switch (character)
            {
                case 'f':
                case 'F':
                case 'd':
                case 'D':
                case 'm':
                case 'M':
                case 'u':
                case 'U':
                case 'l':
                case 'L':
                    return true;
                default:
                    return false;
            }
        }

        private static void ApplySelectedButtonStyle(GUIStyle style, Texture2D background)
        {
            style.normal.textColor = Color.white;
            style.active.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.onNormal.textColor = Color.white;
            style.onHover.textColor = Color.white;
            style.onActive.textColor = Color.white;
            style.onFocused.textColor = Color.white;

            style.normal.background = background;
            style.active.background = background;
            style.hover.background = background;
            style.focused.background = background;
            style.onNormal.background = background;
            style.onHover.background = background;
            style.onActive.background = background;
            style.onFocused.background = background;
        }

        private static void ApplyBackgroundTexture(GUIStyle style, Texture2D background)
        {
            style.normal.background = background;
            style.hover.background = background;
            style.active.background = background;
            style.focused.background = background;
            style.onNormal.background = background;
            style.onHover.background = background;
            style.onActive.background = background;
            style.onFocused.background = background;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
#endif
