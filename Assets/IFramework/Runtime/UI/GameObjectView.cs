/*********************************************************************************
 *Author:         OnClick
 *Version:        1.0
 *UnityVersion:   2020.3.3f1c1
 *Date:           2022-08-03
 *Description:    Description
 *History:        2022-08-03--
*********************************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using static IFramework.Events;
using static IFramework.UI.UnityEventHelper;

namespace IFramework.UI
{
    public abstract class GameObjectView
    {
        public GameObject gameObject { get; private set; }
        public Transform transform { get; private set; }
        private ScriptCreatorContext context;


        Dictionary<string, GameObject> _prefabsName;
        public GameObject FindPrefab(string name)
        {
            if (_prefabsName == null) _prefabsName = new Dictionary<string, GameObject>();
            GameObject prefab = null;
            if (_prefabsName.TryGetValue(name, out prefab))
            {
                return prefab;
            }
            prefab = context.FindPrefab(name);
            if (prefab != null)
            {
                _prefabsName[name] = prefab;
            }
            else
            {
                Log.FE($"Not Find Prefab with Name {name} in {context.name}");
            }

            return prefab;
        }
        public virtual void SetActive(bool active) => gameObject.SetActive(active);
        public void SetGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                Log.FE($"{GetType()}-->  Can not SetGameObject With Null GameObject");
                return;
            }
            if (this.gameObject != gameObject)
            {
                ClearFields();
                this.gameObject = gameObject;
                transform = gameObject.transform;
                context = GetComponent<ScriptCreatorContext>(string.Empty);
                InitComponents();
            }
        }

        public Transform GetTransform(string path)
        {
            var tans = string.IsNullOrEmpty(path) ? transform : transform.Find(path);
            if (tans == null)
                Log.FE($"{GetType()}-->  GetTransform result is Null! path-->{path}");
            return tans;
        }

        public GameObject GetGameObject(string path) => GetTransform(path)?.gameObject;
        public T GetComponent<T>(string path)
        {
            var trans = GetTransform(path);
            if (trans != null)
                return trans.GetComponent<T>();
            return default;
        }
        protected abstract void InitComponents();




        public GameObjectView parent { get; private set; }
        public GameObjectView root
        {
            get
            {
                var tmp = this;
                while (tmp.parent != null)
                    tmp = tmp.parent;
                return tmp;
            }
        }
        internal void SetParent(GameObjectView parent)
        {
            var parent_last = this.parent;
            if (parent_last == parent) return;
            if (parent_last != null)
                parent_last.children.Remove(this);
            if (parent != null)
            {
                parent.children.Add(this);
                this.parent = parent;
            }
            else
            {
                Log.FE("SetParent   Parent Can Not Be Null");
            }
        }

        public void SetAsChild(GameObjectView view) => view.SetParent(this);






        private EventBox _eventBox;
        private UIEventBox __eventBox_ui;
        protected EventEntity SubscribeEvent(string msg, Action<IEventArgs> action)
        {
            if (_eventBox == null)
                _eventBox = new EventBox();
            return _eventBox.Subscribe(msg, action);
        }
        protected void UnSubscribeEvent(string msg, Action<IEventArgs> action)
        {
            if (_eventBox == null) return;
            _eventBox.UnSubscribe(msg, action);
        }
        protected void UnSubscribeEvent(EventEntity entity)
        {
            if (_eventBox == null) return;
            _eventBox.UnSubscribe(entity);
        }
        internal void AddUIEvent(UIEventEntity uiEvent)
        {
            if (__eventBox_ui == null)
                __eventBox_ui = new UIEventBox();
            uiEvent.AddTo(__eventBox_ui);
        }
        protected void DisposeUIEvent(UIEventEntity uiEvent)
        {
            if (__eventBox_ui == null) return;
            __eventBox_ui.Dispose(uiEvent);
        }



        public void DisposeEvents()
        {
            if (_eventBox != null)
            {
                _eventBox.Dispose();
                _eventBox = null;
            }
        }
        public void DisposeUIEvents()
        {
            if (__eventBox_ui != null)
            {
                __eventBox_ui.Dispose();
                __eventBox_ui = null;
            }
        }
        public void ClearWidgetPools()
        {
            if (widgetPools != null)
            {
                foreach (var pool in widgetPools.Values)
                {
                    pool.Clear();
                }
                widgetPools.Clear();
            }
            if (_prefabsName != null)
            {
                _prefabsName.Clear();
            }
        }
        protected virtual void OnClearFields() { }
        public void ClearFields()
        {
            DisposeChildren();
            OnClearFields();
            DisposeEvents();
            DisposeUIEvents();
            ClearWidgetPools();
        }
        private void DisposeChildren()
        {
            if (children.Count > 0)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    child.ClearFields();
                }
                children.Clear();
            }
        }







        private List<GameObjectView> children = new List<GameObjectView>();


        public T CreateWidget<T>(GameObject gameObject) where T : GameObjectView, new()
        {
            T t = new T();
            InitWidget(t, gameObject);
            return t;
        }
        public T InitWidget<T>(T view, GameObject gameObject) where T : GameObjectView
        {
            view.SetGameObject(gameObject);
            view.SetParent(this);
            return view;
        }


        private Dictionary<GameObject, IWidgetPool> widgetPools;
        public WidgetPool<T> CreateWidgetPool<T>(GameObject prefab, Transform parent, Func<T> createClass) where T : GameObjectView
        {
            if (widgetPools == null) widgetPools = new Dictionary<GameObject, IWidgetPool>();
            if (widgetPools.TryGetValue(prefab, out IWidgetPool pool))
            {
                return pool as WidgetPool<T>;
            }
            else
            {
                var _pool = new WidgetPool<T>(this, prefab, parent, createClass, prefab.transform.IsChildOf(transform));
                widgetPools[prefab] = _pool;
                return _pool;
            }
        }

        public WidgetPool<T> FindWidgetPool<T>(GameObject prefab) where T : GameObjectView
        {
            if (widgetPools == null) return null;
            if (widgetPools.TryGetValue(prefab, out IWidgetPool pool))
                return pool as WidgetPool<T>;
            return null;
        }

        public T GetWidgetFromPool<T>(GameObject prefab, Transform parent = null) where T : GameObjectView
        {
            var pool = FindWidgetPool<T>(prefab);
            return pool.Get(parent);
        }
        public void SetWidgetToPool<T>(GameObject prefab, T ins) where T : GameObjectView
        {
            var pool = FindWidgetPool<T>(prefab);
            pool.Set(ins);
        }

    }
}
