/*********************************************************************************
 *Author:         OnClick
 *Version:        0.0.1
 *UnityVersion:   2018.3.11f1
 *Date:           2020-01-13
 *Description:    IFramework
 *History:        2018.11--
*********************************************************************************/
using UnityEditor;
using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;

using System.IO;

namespace IFramework.UI
{
    public partial class UIModuleWindow
    {
        public class UICollectData
        {

            [System.Serializable]
            public class Plan
            {
                public string ConfigGenPath;
                public string PanelCollectPath;
                public string ScriptGenPath;
                public string ScriptName;
                public string name;
                public string collectionJsonPath => ConfigGenPath.CombinePath($"{ConfigName}.json");


                private static string[] _typeNames, _shortTypes;
                public static string[] typeNames
                {
                    get
                    {
                        if (_typeNames == null)
                            Enable();
                        return _typeNames;
                    }
                }
                public static string[] shortTypes
                {
                    get
                    {
                        if (_shortTypes == null)
                            Enable();
                        return _shortTypes;
                    }
                }
                public static Type[] __types;
                public static Type[] types
                {
                    get
                    {
                        if (__types == null)
                        {
                            Enable();
                        }
                        return __types;
                    }
                }
                public int typeIndex;
                public static Type baseType = typeof(UIGenCode);
                public string ConfigName;

                private static void Enable()
                {
                    var list = EditorTools.GetSubTypesInAssemblies(baseType)
                   .Where(type => !type.IsAbstract);
                    __types = list.ToArray();
                    _typeNames = list.Select(type => type.FullName).ToArray();
                    _shortTypes = list.Select(type => type.Name).ToArray();
                }
                public Type GetSelectType()
                {
                    typeIndex = Mathf.Clamp(typeIndex, 0, typeNames.Length);
                    var type_str = typeNames[typeIndex];
                    Type type = types.FirstOrDefault(x => x.FullName == type_str);

                    return type;
                }


            }


            [System.Serializable]
            private class Plans
            {
                public int index = 0;

                public List<Plan> plans = new List<Plan>();
            }
            const string res = "Resources";

            public static List<Plan> plans { get => context.plans; }

            private static Plans _context;
            private static void SavePlansData() => EditorTools.SaveToPrefs(_context, nameof(Plans), false);
            private static Plans context
            {
                get
                {

                    if (_context == null)
                    {
                        _context = EditorTools.GetFromPrefs<Plans>(nameof(Plans), false);
                        if (_context == null)
                            _context = new Plans();
                        if (_context.plans.Count == 0)
                            NewPlan();
                        SavePlansData();
                    }
                    return _context;
                }
            }


            public static Plan plan => plans[planIndex];
            public static int planIndex
            {
                get => context.index;
                set
                {
                    if (context.index != value)
                    {
                        context.index = value;
                        SavePlansData();
                    }
                }
            }


            internal static void SavePlan(string name, string GenPath, string CollectPath, string ScriptGenPath,
                string scriptName, string configName, int typeIndex)
            {
                var _plan = UICollectData.plan;
                if (_plan.name != name || _plan.ConfigGenPath != GenPath || _plan.PanelCollectPath != CollectPath
                    || _plan.ConfigName != configName
                    || _plan.ScriptGenPath != ScriptGenPath || _plan.ScriptName != scriptName || _plan.typeIndex != typeIndex)
                {
                    _plan.name = name;
                    _plan.ConfigGenPath = GenPath;
                    _plan.PanelCollectPath = CollectPath;
                    _plan.ScriptGenPath = ScriptGenPath;
                    _plan.ScriptName = scriptName;
                    _plan.typeIndex = typeIndex;
                    _plan.ConfigName = configName;
                    SavePlansData();
                }
            }

            internal static void DeletePlan()
            {
                if (plans.Count == 1)
                {
                    window.ShowNotification(new GUIContent("Must Exist One Plan"));
                    return;
                }
                plans.RemoveAt(planIndex);
                planIndex = 0;
            }
            internal static void NewPlan()
            {
                plans.Add(new Plan()
                {
                    name = DateTime.Now.ToString("yy_MM_dd_hh_mm_ss"),
                    PanelCollectPath = "Assets",
                    ConfigGenPath = "Assets",
                    ScriptGenPath = "Assets",
                    ConfigName = "UICollect",
                    ScriptName = "PanelNames",
                });
                planIndex = plans.Count - 1;
            }
            public static void GenPlans()
            {
                for (int i = 0; i < plans.Count; i++)
                    GenPlan(plans[i], Collect(plan));
            }

            public static void GenPlan(Plan plan, PanelCollection collect)
            {
                var selectType = plan.GetSelectType();
                foreach (var item in window._tabs.Values)
                {
                    if (item.GetType() == selectType)
                    {
                        (item as UIGenCode).GenPanelNames(collect, plan.ScriptGenPath, plan.ScriptName);
                        break;
                    }
                }
                GenCollectionJson(plan, collect);
            }
            private static void GenCollectionJson(Plan path, PanelCollection collect)
            {
                File.WriteAllText(path.collectionJsonPath, JsonUtility.ToJson(collect, true));
                AssetDatabase.Refresh();
            }




            [System.Serializable]
            public class ScriptPathCollection
            {
                [System.Serializable]
                public class Seg
                {
                    public string prefab;
                    public string ScriptPath;
                    public List<string> Paths;
                }

                public List<Seg> segs = new List<Seg>();
                private static ScriptPathCollection __context;
                private Seg Get(string prefab)
                {
                    var find = segs.Find(x => x.prefab == prefab);
                    if (find == null)
                    {
                        find = new Seg() { prefab = prefab };
                        segs.Add(find);
                    }
                    return find;
                }
                private static ScriptPathCollection context_scripts
                {
                    get
                    {
                        if (__context == null)
                        {

                            __context = EditorTools.GetFromPrefs<ScriptPathCollection>(nameof(ScriptPathCollection), false);
                            if (__context == null)
                                __context = new ScriptPathCollection();
                        }
                        return __context;
                    }
                }
                public static void SaveScriptsData()
                {
                    EditorTools.SaveToPrefs(context_scripts, nameof(ScriptPathCollection), false);
                }

                private static Seg GetSeg(string prefab) => context_scripts.Get(prefab);
                public static Seg GetSeg(PanelCollection.Data data) => GetSeg(data.path);
            }


            private static bool CollectScripPaths(PanelCollection collect, Plan plan)
            {
                var tab = window.GetTab(plan.GetSelectType());
                var paths = AssetDatabase.FindAssets((tab as UIGenCode).GetScriptFitter())
                    .Select(x => AssetDatabase.GUIDToAssetPath(x))
                    .ToList();
                bool change = false;



                for (int i = 0; i < collect.datas.Count; i++)
                {
                    var data = collect.datas[i];
                    var s_name = (tab as UIGenCode).GetPanelScriptName(data.name);
                    var find = paths.FindAll(x => x.EndsWith("/" + s_name)) ?? new List<string>();

                    var seg = ScriptPathCollection.GetSeg(data);
                    seg.Paths = find;

                SetEmpty:
                    if (string.IsNullOrEmpty(seg.ScriptPath))
                    {
                        if (find.Count > 0)
                        {
                            change = true;
                            seg.ScriptPath = find[0];
                        }
                    }
                    else
                    {
                        if (!find.Contains(seg.ScriptPath))
                        {
                            seg.ScriptPath = string.Empty;
                            change = true;
                            goto SetEmpty;
                        }
                    }
                }
                ScriptPathCollection.SaveScriptsData();
                return change;
            }
            public static bool ValidOrders(PanelCollection collection)
            {
                bool change = false;
                var layers = collection.datas.Select(x => x.layer).Distinct().ToList();
                foreach (var layer in layers)
                {
                    var list = collection.datas.Where(x => x.layer == layer).ToList();
                    list.Sort((x, y) => { return x.order >= y.order ? 1 : -1; });
                    for (var i = 0; i < list.Count; i++)
                    {
                        var data = list[i];
                        if (data.order != i)
                        {
                            data.order = i;
                            change = true;
                        }
                    }
                }
                return change;
            }
            public static PanelCollection Collect(Plan plan)
            {
                string path = plan.collectionJsonPath;
                PanelCollection collect = null;
                if (!File.Exists(path))
                    collect = new PanelCollection();
                else
                    collect = JsonUtility.FromJson<PanelCollection>(File.ReadAllText(path));






                var paths = AssetDatabase.FindAssets("t:prefab", new string[] { plan.PanelCollectPath })
                    .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                    .Where(path => AssetDatabase.LoadAssetAtPath<UIPanel>(path) != null).ToList();





                int remove = collect.datas.RemoveAll(x => !paths.Contains(x.path));
                var _new = paths.FindAll(path => !collect.datas.Any(data => data.path == path));




                _new.ForEach(path =>
                  {
                      var isResourcePath = path.Contains(res);
                      if (isResourcePath)
                      {
                          var index = path.IndexOf(res);
                          path = path.Substring(index + res.Length + 1)
                                  .Replace(".prefab", "");
                      }
                      collect.datas.Add(new PanelCollection.Data()
                      {
                          isResourcePath = isResourcePath,
                          path = path,
                          layer = 0,
                          fullScreen = false,
                          order = int.MaxValue
                      });
                  });
                var change = CollectScripPaths(collect, plan);

                change |= ValidOrders(collect);
                if (remove != 0 || _new.Count > 0 || change)
                    GenCollectionJson(plan, collect);
                return collect;
            }





        }

    }
}
