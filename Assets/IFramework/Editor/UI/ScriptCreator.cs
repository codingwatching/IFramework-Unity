/*********************************************************************************
 *Author:         OnClick
 *Version:        0.0.1
 *UnityVersion:   2018.3.1f1
 *Date:           2019-03-18
 *Description:    IFramework
 *History:        2018.11--
*********************************************************************************/
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using System;
using static IFramework.UI.ScriptCreatorContext;

namespace IFramework.UI
{
    [System.Serializable]
    public class ScriptCreator
    {
        public void SaveContext()
        {
            var gameObject = this.gameObject;
            if (!gameObject) return;

            //EditorUtility.SetDirty(context);

            EditorUtility.SetDirty(gameObject);
            //PrefabUtility.RevertPrefabInstance(gameObject, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        public string rootPath => gameObject.transform.GetPath();
        public GameObject gameObject { get; private set; }
        public ScriptCreatorContext context { get; private set; }
  

        public string ToValidFiledName(string src) => ScriptCreatorContext.ToValidFiledName(src);



        public bool IsPrefabInstance(GameObject obj)
        {
            if (obj == null) return true;
            return UnityEditor.PrefabUtility.IsPartOfPrefabInstance(obj);
        }

        public void RemoveMarks(List<GameObject> marks)
        {
            for (int i = 0; i < marks.Count; i++)
            {
                var s = marks[i];
                if (!CouldMark(s)) continue;
                context.RemoveMark(s, !IsPrefabInstance(s));
            }
            SaveContext();
        }
        public ScriptCreatorContext.MarkContext AddMark(GameObject go, Type type) => AddMark(go, type.FullName);
        public ScriptCreatorContext.MarkContext AddMark(GameObject go, string type)
        {
            if (!CouldMark(go)) return null;
            var sm = context.AddMark(go, type, !IsPrefabInstance(go));
            SaveContext();
            return sm;
        }

        public MarkContext GetMark(GameObject go)
        {
            return context.GetMark(go);
        }
        public MarkContext GetPrefabMark(GameObject go)
        {
            var all = context.GetAllMarks();
            return all.Find(m => m.gameObject == go);
        }


        public void RemoveUselessMarkFlag()
        {
            List<GameObject> result = new List<GameObject>();
            context.CollectFlagGameObjects(gameObject.transform, result);
            result.RemoveAll(x => IsPrefabInstance(x));

            result.RemoveAll(x => context.marks.Find(y => y.gameObject == x) != null);
            if (result.Count == 0) return;
            for (int i = 0; i < result.Count; i++)
                context.RemoveMark(result[i], true);
            SaveContext();

        }


        public void RemoveEmptyMarks()
        {
            context.RemoveEmpty();
            SaveContext();
        }


        public void SetGameObject(GameObject gameObject)
        {
            if (gameObject != this.gameObject)
            {
                this.gameObject = gameObject;
                this.context = null;
                if (gameObject != null)
                {
                    var context = this.gameObject.GetComponent<ScriptCreatorContext>();
                    if (context == null)
                    {
                        context = this.gameObject.AddComponent<ScriptCreatorContext>();
                    }
                    this.context = context;
                    context.RemoveEmpty();

                    SaveContext();
                }

            }
        }

        public bool HandleSameFieldName(out string same)
        {
            bool bo = context.HandleSameFieldName(out same, IsPrefabInstance);
            SaveContext();
            return bo;
        }
        public List<ScriptCreatorContext.MarkContext> GetMarks()
        {
            return this.context.marks;
        }
        //public List<ScriptCreatorContext.MarkContext> GetAllMarks()
        //{
        //    if (!gameObject) return null;
        //    return this.context.GetAllMarks();
        //}

        public void DestroyMarks()
        {
            context.DestroyMarks();
            SaveContext();
        }

        public bool CouldMark(GameObject go)
        {
            if (go == null) return false;
            if (IsPrefabInstance(go))
            {
                var tmp = go.transform;
                while (true)
                {
                    var child = tmp.GetComponent<ScriptCreatorContext>();
                    if (child != null) return false;
                    tmp = tmp.parent;
                    if (!IsPrefabInstance(tmp.gameObject))
                        return true;
                }
            }
            return true;
        }



    }
}
