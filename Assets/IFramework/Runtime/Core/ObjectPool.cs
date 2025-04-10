﻿using System;
using System.Collections.Generic;

namespace IFramework
{

    public abstract class ObjectPool<T>
    {

        protected Queue<T> pool { get { return _lazy.Value; } }
        private Lazy<Queue<T>> _lazy = new Lazy<Queue<T>>(() => { return new Queue<T>(); }, true);

        public virtual Type type { get { return typeof(T); } }


        public int count { get { return pool.Count; } }




        public virtual T Get()
        {
            T t;
            if (pool.Count > 0)
            {
                t = pool.Dequeue();
            }
            else
            {
                t = CreateNew();
                OnCreate(t);
            }
            OnGet(t);
            return t;
        }

        public virtual bool Set(T t)
        {
            if (!pool.Contains(t))
            {
                if (OnSet(t))
                {
                    pool.Enqueue(t);
                }
                return true;
            }
            else
            {
                Log.FE("Set Err: Exist " + type);
                return false;
            }
        }


        public void Clear()
        {
            while (pool.Count > 0)
            {
                var t = pool.Dequeue();
                OnClear(t);
                IDisposable dispose = t as IDisposable;
                if (dispose != null)
                    dispose.Dispose();
            }
        }

        protected abstract T CreateNew();

        protected virtual void OnClear(T t) { }

        protected virtual bool OnSet(T t)
        {
            return true;
        }

        protected virtual void OnGet(T t) { }

        protected virtual void OnCreate(T t) { }
    }
    public interface ISimpleObjectPool
    {
        void SetObject(object context);
    }
    public sealed class SimpleObjectPool<T> : ObjectPool<T>, ISimpleObjectPool where T : class, new()
    {
        public void SetObject(object context)
        {
            if (!(context is T))
            {
                Log.FE($"{nameof(context)} is not {typeof(T)}");
                return;
            }
            base.Set(context as T);
        }

        protected override T CreateNew()
        {
            return new T();
        }
    }
}
