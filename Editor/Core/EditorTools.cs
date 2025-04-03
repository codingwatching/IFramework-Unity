/*********************************************************************************
 *Author:         OnClick
 *Version:        0.0.1
 *UnityVersion:   2018.3.1f1
 *Date:           2019-03-18
 *Description:    IFramework
 *History:        2018.11--
*********************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace IFramework
{

    [InitializeOnLoad]

    public partial class EditorTools
    {
        static Dictionary<Type, List<Delegate>> on_addComp = new Dictionary<Type, List<Delegate>>();
        public static void CallAddComponent(Component obj)
        {
            Type type = obj.GetType();
            List<Delegate> list;
            if (!on_addComp.TryGetValue(type, out list)) return;

            foreach (var del in list)
            {
                del.DynamicInvoke(obj);
            }
        }

        static EditorTools()
        {
            ObjectFactory.componentWasAdded += CallAddComponent;

            var result = GetTypes()
                    .SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                   .Where(x => x.IsDefined(typeof(OnAddComponentAttribute)))
                   .Where(x =>
                   {
                       var param = x.GetParameters();
                       return param.Length == 1 && param[0].ParameterType.IsSubclassOf(typeof(Component));
                   })
                   .Select(x =>
                   {
                       var attr = x.GetCustomAttribute<OnAddComponentAttribute>();
                       return new { method = x, type = attr.type, oder = attr.oder };
                   });
            var types = result.Select(x => x.type).Distinct().ToList();
            foreach (var type in types)
            {
                var list = result.Where(x => x.type == type).ToList();
                list.Sort((x, y) => x.oder - y.oder);
                on_addComp[type] = new List<Delegate>(list.Select(x => x.method.ToDelegate(null)));
            }

            var directorys = new List<string>()
            {
                "Assets/Editor",
                EditorTools.projectMemoryPath,
            };
            CreateDirectories(directorys);

            AssetDatabase.Refresh();




            Log.logger = new UnityLogger();
            SetLogStatus();
        }



        public static void SetLogStatus()
        {
            Log.enable_F = ProjectConfig.enable_F;

            Log.enable_L = ProjectConfig.enable_L;
            Log.enable_W = ProjectConfig.enable_W;
            Log.enable_E = ProjectConfig.enable_E;

            Log.enable = ProjectConfig.enable;
        }

        public const string projectMemoryPath = "Assets/Editor/IFramework";

        private static string GetFilePath() => AssetDatabase.GetAllAssetPaths().FirstOrDefault(x => x.Contains(nameof(IFramework))
                                                        && x.EndsWith($"{nameof(EditorTools)}.cs"));
        public static string pkgPath
        {
            get
            {
                string packagePath = Path.GetFullPath("Packages/com.woo.iframework");
                if (Directory.Exists(packagePath))
                {
                    return packagePath;
                }

                string path = GetFilePath();
                var index = path.LastIndexOf("IFramework");
                path = path.Substring(0, index + "IFramework".Length);
                return path;
            }
        }


        public static void SaveToPrefs<T>(T value, string key, bool unique = true) => Prefs.SetObject(value.GetType(), key, value, unique);
        public static T GetFromPrefs<T>(string key, bool unique = true) => Prefs.GetObject<T>(key, unique);
        public static object GetFromPrefs(Type type, string key, bool unique = true) => Prefs.GetObject(type, key, unique);



        public static void OpenFolder(string folder) => EditorUtility.OpenWithDefaultApp(folder);

        public static string ToAbsPath(this string self)
        {
            string assetRootPath = Path.GetFullPath(Application.dataPath);
            assetRootPath = assetRootPath.Substring(0, assetRootPath.Length - 6) + self;
            return assetRootPath.ToRegularPath();
        }
        public static string ToAssetsPath(this string self) => "Assets" + Path.GetFullPath(self).Substring(Path.GetFullPath(Application.dataPath).Length).Replace("\\", "/");
        public static string GetPath(this Transform transform)
        {
            var sb = new System.Text.StringBuilder();
            var t = transform;
            while (true)
            {
                sb.Insert(0, t.name);
                t = t.parent;
                if (t)
                {
                    sb.Insert(0, "/");
                }
                else
                {
                    return sb.ToString();
                }
            }
        }
        public static string ToUnixLineEndings(this string self) => self.Replace("\r\n", "\n").Replace("\r", "\n");

        public static Delegate ToDelegate(this MethodInfo method, object target)
        {
            var _params = method.GetParameters();
            Type delegateType = default;
            var void_func = method.ReturnType == typeof(void);

            Type base_func_type = void_func ? typeof(Action) : typeof(Func<>);
            if (void_func)
            {
                if (_params == null || _params.Length == 0)
                    delegateType = typeof(Action);
                else
                {
                    if (_params.Length == 1) base_func_type = typeof(Action<>);
                    else if (_params.Length == 2) base_func_type = typeof(Action<,>);
                    else if (_params.Length == 3) base_func_type = typeof(Action<,,>);
                    else if (_params.Length == 4) base_func_type = typeof(Action<,,,>);
                    else if (_params.Length == 5) base_func_type = typeof(Action<,,,,>);
                    else if (_params.Length == 6) base_func_type = typeof(Action<,,,,,>);
                    else if (_params.Length == 7) base_func_type = typeof(Action<,,,,,,>);
                    else if (_params.Length == 8) base_func_type = typeof(Action<,,,,,,,>);
                    else if (_params.Length == 9) base_func_type = typeof(Action<,,,,,,,,>);
                    else if (_params.Length == 10) base_func_type = typeof(Action<,,,,,,,,,>);
                    else if (_params.Length == 11) base_func_type = typeof(Action<,,,,,,,,,,>);
                    else if (_params.Length == 12) base_func_type = typeof(Action<,,,,,,,,,,,>);
                    else if (_params.Length == 13) base_func_type = typeof(Action<,,,,,,,,,,,,>);
                    else if (_params.Length == 14) base_func_type = typeof(Action<,,,,,,,,,,,,,>);
                    else if (_params.Length == 15) base_func_type = typeof(Action<,,,,,,,,,,,,,,>);
                    else if (_params.Length == 16) base_func_type = typeof(Action<,,,,,,,,,,,,,,,>);
                    delegateType = base_func_type
                                    .MakeGenericType(_params
                                            .Select(x => x.ParameterType)
                                            .ToArray());

                }
            }
            else
            {

                if (_params == null || _params.Length == 0)
                {
                    delegateType = base_func_type.MakeGenericType(new Type[] { method.ReturnType });
                }
                else
                {
                    if (_params.Length == 1) base_func_type = typeof(Func<,>);
                    else if (_params.Length == 2) base_func_type = typeof(Func<,,>);
                    else if (_params.Length == 3) base_func_type = typeof(Func<,,,>);
                    else if (_params.Length == 4) base_func_type = typeof(Func<,,,,>);
                    else if (_params.Length == 5) base_func_type = typeof(Func<,,,,,>);
                    else if (_params.Length == 6) base_func_type = typeof(Func<,,,,,,>);
                    else if (_params.Length == 7) base_func_type = typeof(Func<,,,,,,,>);
                    else if (_params.Length == 8) base_func_type = typeof(Func<,,,,,,,,>);
                    else if (_params.Length == 9) base_func_type = typeof(Func<,,,,,,,,,>);
                    else if (_params.Length == 10) base_func_type = typeof(Func<,,,,,,,,,,>);
                    else if (_params.Length == 11) base_func_type = typeof(Func<,,,,,,,,,,,>);
                    else if (_params.Length == 12) base_func_type = typeof(Func<,,,,,,,,,,,,>);
                    else if (_params.Length == 13) base_func_type = typeof(Func<,,,,,,,,,,,,,>);
                    else if (_params.Length == 14) base_func_type = typeof(Func<,,,,,,,,,,,,,,>);
                    else if (_params.Length == 15) base_func_type = typeof(Func<,,,,,,,,,,,,,,,>);
                    else if (_params.Length == 16) base_func_type = typeof(Func<,,,,,,,,,,,,,,,,>);
                    delegateType = base_func_type
                                    .MakeGenericType(_params
                                            .Select(x => x.ParameterType)
                                            .Concat(new Type[] { method.ReturnType })
                                            .ToArray());

                }

            }
            return method.CreateDelegate(delegateType, target);
        }

        public static IEnumerable<Type> GetTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                                 .SelectMany(item => item.GetTypes());
        }
        public static IEnumerable<Type> GetSubTypesInAssemblies(this Type self)
        {
            if (self.IsInterface)
                return GetTypes().Where(item => item.GetInterfaces().Contains(self));
            return GetTypes().Where(item => item.IsSubclassOf(self));
        }

        public static string ToRegularPath(this string path) => path.Replace('\\', '/');

        public static string CombinePath(this string path, string toCombinePath) => Path.Combine(path, toCombinePath).ToRegularPath();
        public static void CreateDirectories(List<string> directories)
        {
            foreach (var path in directories)
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
        }
    }
}
