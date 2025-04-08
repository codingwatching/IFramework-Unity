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
        class EditorPanelCollection
        {
            const string res = "Resources";

            public static List<EditorPanelCollectionPlan> plans { get => context.plans; }

            private static EditorPanelCollectionPlans _context;
            private static void SavePlansData() => EditorTools.SaveToPrefs(_context, nameof(EditorPanelCollectionPlans), false);
            private static EditorPanelCollectionPlans context
            {
                get
                {

                    if (_context == null)
                    {
                        _context = EditorTools.GetFromPrefs<EditorPanelCollectionPlans>(nameof(EditorPanelCollectionPlans), false);
                        if (_context == null)
                            _context = new EditorPanelCollectionPlans();
                        if (_context.plans.Count == 0)
                            NewPlan();
                        SavePlansData();
                    }
                    return _context;
                }
            }


            public static EditorPanelCollectionPlan plan_current => plans[planIndex];
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
                var _plan = EditorPanelCollection.plan_current;
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
                plans.Add(new EditorPanelCollectionPlan()
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
                    GenPlan(plans[i], Collect(plan_current));
            }

            public static void GenPlan(EditorPanelCollectionPlan plan, PanelCollection collect)
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
            private static void GenCollectionJson(EditorPanelCollectionPlan path, PanelCollection collect)
            {
                File.WriteAllText(path.collectionJsonPath, JsonUtility.ToJson(collect, true));
                AssetDatabase.Refresh();
            }
            public static PanelCollection Collect(EditorPanelCollectionPlan plan)
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
                      });
                  });
                collect.datas.Sort((x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.name, y.name));


                var tab = window.GetTab(plan.GetSelectType());

                var change = ScriptPathCollection.CollectScripPaths(collect, tab);
                if (remove != 0 || _new.Count > 0 || change)
                    GenCollectionJson(plan, collect);
                return collect;
            }





        }

    }
}
