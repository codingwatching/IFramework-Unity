/*********************************************************************************
 *Author:         OnClick
 *Version:        0.0.1
 *UnityVersion:   2018.3.1f1
 *Date:           2019-03-18
 *Description:    IFramework
 *History:        2018.11--
*********************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static IFramework.TweenComponentActor;
namespace IFramework
{
    public partial class EditorTools
    {
        [CustomEditor(typeof(TweenComponent))]
        class TweenComponentEditor : Editor
        {
            TweenComponent comp;
            private void OnEnable()
            {
                comp = target as TweenComponent;

            }

            private void Tools()
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(nameof(TweenComponent.Play)))
                {
                    comp.Play();
                }
                using (new EditorGUI.DisabledGroupScope(!comp.hasValue))
                {
                    GUILayout.Space(10);
                    if (comp.paused)
                    {
                        if (GUILayout.Button(nameof(TweenComponent.UnPause)))
                            comp.UnPause();
                    }
                    else
                    {
                        if (GUILayout.Button(nameof(TweenComponent.Pause)))
                            comp.Pause();
                    }
                    GUILayout.Space(10);

                    if (GUILayout.Button(nameof(TweenComponent.Stop)))
                        comp.Stop();

                    //if (GUILayout.Button(nameof(TweenComponent.ReStart)))
                    //    comp.ReStart();

                }

                GUILayout.EndHorizontal();
            }
            public override void OnInspectorGUI()
            {
                GUI.enabled = !EditorApplication.isPlaying;
                base.OnInspectorGUI();

                if (EditorGUILayout.DropdownButton(new GUIContent("Actors", EditorGUIUtility.TrIconContent("d_Toolbar Plus").image),
                    FocusType.Passive, new GUIStyle(EditorStyles.miniPullDown)
                    {
                        fixedHeight = 30
                    }))
                {
                    var types = typeof(TweenComponentActor).GetSubTypesInAssemblies()
                        .Where(x => !x.IsAbstract).ToList();
                    GenericMenu menu = new GenericMenu();
                    foreach (var type in types)
                    {
                        var _baseType = type;
                        bool find = false;
                        while (true)
                        {
                            if (_baseType.IsGenericType && _baseType.GetGenericTypeDefinition() == typeof(TweenComponentActor<,>))
                            {
                                find = true;
                                break;
                            }
                            else if (_baseType == typeof(object))
                            {
                                break;
                            }
                            _baseType = _baseType.BaseType;
                        }
                        if (find)
                        {
                            var args = _baseType.GetGenericArguments();
                            menu.AddItem(new GUIContent($"{args[0].Name}/{type.Name}"), false, () =>
                            {
                                comp.actors.Add(Activator.CreateInstance(type) as TweenComponentActor);
                            });

                        }

                    }

                    menu.ShowAsContext();
                }


                EditorGUI.BeginChangeCheck();
                for (int i = 0; i < comp.actors.Count; i++)
                {
                    var actor = comp.actors[i];
                    var mode = DrawActor(actor, i);
                    switch (mode)
                    {
                        case Mode.Remove:
                            comp.actors.RemoveAt(i);
                            break;
                        case Mode.MoveDown:
                            {
                                if (i != comp.actors.Count - 1)
                                {
                                    comp.actors[i] = comp.actors[i + 1];
                                    comp.actors[i + 1] = actor;
                                }
                            }
                            break;
                        case Mode.MoveUp:
                            {
                                if (i != 0)
                                {
                                    comp.actors[i] = comp.actors[i - 1];
                                    comp.actors[i - 1] = actor;
                                }
                            }
                            break;

                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(comp);
                    //if (comp.hasValue && !comp.paused)
                }
                GUILayout.Space(10);
                Repaint();
                Tools();
            }

            private enum Mode
            {
                Remove, MoveDown, MoveUp, None
            }
            private Mode DrawActor(TweenComponentActor actor, int index)
            {
                Mode mode = Mode.None;
                EditorGUILayout.LabelField("", GUI.skin.textField, GUILayout.Height(25));
                var rect = EditorTools.RectEx.Zoom(GUILayoutUtility.GetLastRect(),
                    TextAnchor.MiddleRight, new Vector2(-20, 0));
                var rs = EditorTools.RectEx.VerticalSplit(rect, rect.width - 80, 4);
                EditorGUI.ProgressBar(rs[0], actor.percent, "");
                var fold = EditorGUI.Foldout(rs[0], GetFoldout(actor), $"{actor.GetType().Name}", true);
                SetFoldout(actor, fold);


                var rss = RectEx.VerticalSplit(rs[1], rect.height, 0);
                if (GUI.Button(rss[0], EditorGUIUtility.TrIconContent("d_Toolbar Minus")))
                    mode = Mode.Remove;
                rss = RectEx.VerticalSplit(rss[1], rect.height, 0);
                GUI.enabled = index != 0;
                if (GUI.Button(rss[0], EditorGUIUtility.TrIconContent("d_scrollup")))
                    mode = Mode.MoveUp;
                rss = RectEx.VerticalSplit(rss[1], rect.height, 0);
                GUI.enabled = index != comp.actors.Count - 1;
                if (GUI.Button(rss[0], EditorGUIUtility.TrIconContent("d_scrolldown")))
                    mode = Mode.MoveDown;




                GUI.enabled = true;

                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.BeginVertical();
                if (mode == Mode.None && fold)
                {

                    GUILayout.BeginVertical(EditorStyles.helpBox);
                    FieldDefaultInspector(actor.GetType().GetField("target"), actor);

                    actor.snap = EditorGUILayout.Toggle("Snap", actor.snap);
                    actor.duration = EditorGUILayout.FloatField("Duration", actor.duration);
                    actor.delay = EditorGUILayout.FloatField("Delay", actor.delay);
                    GUILayout.Space(10);

                    actor.loopType = (LoopType)EditorGUILayout.EnumPopup(nameof(LoopType), actor.loopType);
                    actor.loops = EditorGUILayout.IntField("Loops", actor.loops);


                    GUILayout.Space(10);

                    actor.curveType = (CurveType)EditorGUILayout.EnumPopup(nameof(CurveType), actor.curveType);
                    if (actor.curveType == CurveType.Ease)
                    {
                        actor.ease = (Ease)EditorGUILayout.EnumPopup(nameof(Ease), actor.ease);
                    }
                    else
                    {
                        AnimationCurve curve = actor.curve;
                        if (curve == null)
                        {
                            curve = new AnimationCurve();
                        }
                        actor.curve = EditorGUILayout.CurveField(nameof(AnimationCurve), curve);
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(5);

                    List<Type> types = new List<Type>();


                    var _baseType = actor.GetType();
                    while (true)
                    {
                        if (_baseType.IsGenericType && _baseType.GetGenericTypeDefinition() == typeof(TweenComponentActor<,>))
                        {
                            break;
                        }
                        types.Insert(0, _baseType);
                        _baseType = _baseType.BaseType;
                    }


                    GUILayout.BeginVertical(EditorStyles.helpBox);



                    actor.startType = (StartValueType)EditorGUILayout.EnumPopup(nameof(StartValueType), actor.startType);

                    foreach (var type in types)
                    {
                        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                        for (int i = 0; i < fields.Length; i++)
                        {
                            FieldDefaultInspector(fields[i], actor);
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                return mode;
            }


            public object DrawDefaultInspector(object obj)
            {
                GUILayout.BeginVertical();
                var type = obj.GetType();
                //得到字段的值,只能得到public类型的字典的值
                FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                //排序一下，子类的字段在后，父类的在前
                Array.Sort(fieldInfos, FieldsSprtBy);

                //判断需要过滤不显示的字段
                List<FieldInfo> needShowField = new List<FieldInfo>();
                foreach (var field in fieldInfos)
                {
                    var need = true;
                    var attributes = field.GetCustomAttributes();
                    foreach (var attribute in attributes)
                    {
                        if (attribute is HideInInspector hide)
                        {
                            need = false;
                            break;
                        }


                    }

                    if (need)
                    {
                        needShowField.Add(field);
                    }
                }
                foreach (var field in needShowField)
                {
                    FieldDefaultInspector(field, obj);
                }
                GUILayout.EndVertical();
                return obj;
            }

            static List<Type> _base = new List<Type>()
        {
            typeof(int),typeof(float),typeof(double),typeof(bool),typeof(long),typeof(string),
            typeof(Color),typeof(Vector2),typeof(Vector3),typeof(Vector4),typeof(Vector2Int),typeof(Vector3Int),
            typeof(Rect),typeof(RectInt),typeof(Bounds),typeof(UnityEngine.Object),typeof(AnimationCurve),
        };
            private static bool IsBaseType(Type type)
            {
                if (type.IsEnum || _base.Contains(type)) return true;
                return false;
            }
            private object DrawBase(object value, string name, Type fieldType)
            {

                if (fieldType == typeof(int)) return EditorGUILayout.IntField(name, (int)value);
                else if (fieldType == typeof(float)) return EditorGUILayout.FloatField(name, (float)value);
                else if (fieldType == typeof(bool)) return EditorGUILayout.Toggle(name, (bool)value);
                else if (fieldType == typeof(string)) return EditorGUILayout.TextField(name, (string)value);
                else if (fieldType == typeof(long)) return EditorGUILayout.LongField(name, (long)value);
                else if (fieldType == typeof(double)) return EditorGUILayout.DoubleField(name, (double)value);
                else if (fieldType.IsEnum) return EditorGUILayout.EnumPopup(name, (Enum)value);
                else if (fieldType == typeof(Color)) return EditorGUILayout.ColorField(name, (Color)value);
                else if (fieldType == typeof(Vector2)) return EditorGUILayout.Vector2Field(name, (Vector2)value);
                else if (fieldType == typeof(Vector3)) return EditorGUILayout.Vector3Field(name, (Vector3)value);
                else if (fieldType == typeof(Vector4)) return EditorGUILayout.Vector4Field(name, (Vector4)value);
                else if (fieldType == typeof(Vector2Int)) return EditorGUILayout.Vector2IntField(name, (Vector2Int)value);
                else if (fieldType == typeof(Vector3Int)) return EditorGUILayout.Vector3IntField(name, (Vector3Int)value);
                else if (fieldType == typeof(Rect)) return EditorGUILayout.RectField(name, (Rect)value);
                else if (fieldType == typeof(RectInt)) return EditorGUILayout.RectIntField(name, (RectInt)value);
                else if (fieldType == typeof(Bounds)) return EditorGUILayout.BoundsField(name, (Bounds)value);
                else if (fieldType.IsSubclassOf(typeof(UnityEngine.Object))) return EditorGUILayout.ObjectField(name, (UnityEngine.Object)value, fieldType, true);
                else if (fieldType == typeof(AnimationCurve))
                {
                    AnimationCurve curve = value as AnimationCurve;
                    if (curve == null)
                    {
                        curve = new AnimationCurve();
                    }

                    return EditorGUILayout.CurveField(name, curve);
                }
                return value;
            }
            private float DrawRange(string name, float value, float min, float max)
            {
                return EditorGUILayout.Slider(name, (float)value, min, max);
            }

            private string DrawMutiLine(string name, string value, int lines)
            {
                GUILayout.Label(name);
                return EditorGUILayout.TextArea(value, GUILayout.MinHeight(lines * 18));
            }

            static MethodInfo method;
            private static Dictionary<int, bool> _unfoldDictionary = new Dictionary<int, bool>();

            private IList DrawArr(ref bool fold, string name, IList arr, Type ele)
            {
                IList array = Activator.CreateInstance(typeof(List<>).MakeGenericType(ele)) as IList;

                for (int i = 0; i < arr.Count; i++)
                    array.Add(arr[i]);
                var cout = array.Count;
                GUILayout.BeginHorizontal();
                fold = EditorGUILayout.Foldout(fold, $"{name}({ele.Name})");
                if (GUILayout.Button("+", GUILayout.Width(30)))
                {
                    Array newArray = Array.CreateInstance(ele, array != null ? array.Count + 1 : 1);
                    if (array != null)
                    {
                        array.CopyTo(newArray, 0);
                    }

                    newArray.SetValue(Activator.CreateInstance(ele), newArray.Length - 1);
                    array = newArray;
                    SetFoldout(newArray, true);
                }
                GUILayout.EndHorizontal();
                if (fold)
                {
                    GUILayout.Space(6);
                    GUILayout.BeginVertical(GUI.skin.box);
                    for (int i = 0; i < array.Count; i++)
                    {
                        object listItem = array[i];
                        EditorGUILayout.BeginHorizontal();
                        {
                            if (IsBaseType(ele))
                                array[i] = DrawBase(listItem, $"Element {i}", ele);
                            else
                                array[i] = DrawDefaultInspector(listItem);
                            if (GUILayout.Button("x", GUILayout.Width(20)))
                            {
                                array.Remove(listItem);
                                break;
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                        DrawDivider();
                    }
                    GUILayout.EndVertical();
                }
                return array;
            }
            private void DrawDivider()
            {
                GUILayout.Space(2);
                //Color color = Color.black.WithAlpha(0.1f);
                Rect rect = EditorGUILayout.GetControlRect(false, 2);
                GUI.Label(rect, "", (GUIStyle)"WindowBottomResize");
                GUILayout.Space(2);
            }


            protected void FieldDefaultInspector(FieldInfo field, object obj)
            {
                var fieldType = field.FieldType;
                var showType = field.FieldType;
                var value = field.GetValue(obj);
                var newValue = value;
                var name = field.Name;
                var attributes = field.GetCustomAttributes();
                SpaceAttribute space = attributes.FirstOrDefault(x => x is SpaceAttribute) as SpaceAttribute;
                if (space != null)
                {
                    GUILayout.Space(space.height);
                }
                HeaderAttribute header = attributes.FirstOrDefault(x => x is HeaderAttribute) as HeaderAttribute;
                if (header != null)
                    GUILayout.Label(header.header, EditorStyles.boldLabel);
                RangeAttribute range = attributes.FirstOrDefault(x => x is RangeAttribute) as RangeAttribute;
                MultilineAttribute mutiline = attributes.FirstOrDefault(x => x is MultilineAttribute) as MultilineAttribute;

                if (range != null && fieldType == typeof(float))
                    newValue = DrawRange(name, (float)value, range.min, range.max);
                else if (mutiline != null && fieldType == typeof(string))
                    newValue = DrawMutiLine(name, (string)value, mutiline.lines);
                else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    Type elementType = fieldType.GetGenericArguments()[0];
                    IList array = (IList)value;
                    if (array == null)
                        array = Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType)) as IList;
                    var fold = GetFoldout(value);
                    var result = DrawArr(ref fold, name, array, elementType);
                    array.Clear();
                    SetFoldout(array, fold);
                    for (int i = 0; i < result.Count; i++)
                        array.Add(result[i]);
                    newValue = array;
                }
                // 处理数组类型
                else if (fieldType.IsArray)
                {

                    Type elementType = fieldType.GetElementType();
                    Array array = (Array)value;

                    if (array == null)
                        array = Array.CreateInstance(elementType, 0);
                    var fold = GetFoldout(value);
                    var result = DrawArr(ref fold, name, array, elementType);
                    Array.Clear(array, 0, array.Length);
                    if (array.Length != result.Count)
                        array = Array.CreateInstance(elementType, result.Count);
                    SetFoldout(array, fold);

                    for (int i = 0; i < result.Count; i++)
                        array.SetValue(result[i], i);
                    newValue = array;
                }

                else
                    newValue = DrawBase(value, name, fieldType);
                if (value != newValue)
                    field.SetValue(obj, newValue);
            }

            private int FieldsSprtBy(FieldInfo f1, FieldInfo f2)
            {
                if (f1 == null || f2 == null) return 0;
                var e1 = f1.DeclaringType == f1.ReflectedType;
                var e2 = f2.DeclaringType == f2.ReflectedType;
                if (e1 != e2)
                {
                    if (e1)
                    {
                        return 1;
                    }

                    return -1;
                }

                return 0;
            }


            private bool GetFoldout(object obj)
            {
                if (obj == null) return false;
                if (!_unfoldDictionary.TryGetValue(obj.GetHashCode(), out var value))
                {
                    _unfoldDictionary[obj.GetHashCode()] = false;
                }

                return value;
            }

            private void SetFoldout(object obj, bool unfold)
            {
                if (obj == null) return;
                _unfoldDictionary[obj.GetHashCode()] = unfold;
            }





        }
    }
}
