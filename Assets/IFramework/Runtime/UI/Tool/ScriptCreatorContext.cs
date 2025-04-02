/*********************************************************************************
 *Author:         OnClick
 *Version:        0.0.1
 *UnityVersion:   2017.2.3p3
 *Date:           2019-07-30
 *Description:    IFramework
 *History:        2018.11--
*********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
namespace IFramework.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class ScriptCreatorContext : MonoBehaviour
    {
        [HideInInspector][SerializeField] internal List<MarkContext> marks = new List<MarkContext>();
        [System.Serializable]
        public class MarkContext
        {
            public GameObject gameObject;
            public string fieldName;
            public string fieldType;
        }
        public static bool IsLegalFieldName(string src)
        {
            if (string.IsNullOrEmpty(src)) return false;
            return Regex.IsMatch(src, @"^[_a-zA-Z][_a-zA-Z0-9]*$");
        }
        public static string ToValidFiledName(string src)
        {
            var m = Regex.Matches(src.Replace(" ", "_"), "[_a-zA-Z0-9]");
            var list = m.Where(x => x.Success).Select(x => x.Value).ToList();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i == 0)
                {
                    int _val;
                    if (int.TryParse(list[i], out _val))
                    {
                        sb.Append("_");
                    }
                }
                sb.Append(list[i]);
            }
            return sb.ToString();

        }


        public GameObject FindPrefab(string name)
        {
            for (int i = 0; i < Prefabs.Count; i++)
            {
                if (Prefabs[i].name == name) return Prefabs[i];
            }
            return null;
        }

        [SerializeField] internal List<GameObject> Prefabs = new List<GameObject>();
    }
}
