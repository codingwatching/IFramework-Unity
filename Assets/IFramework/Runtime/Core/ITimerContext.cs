/*********************************************************************************
 *Author:         OnClick
 *Version:        0.0.2.116
 *UnityVersion:   2018.4.24f1
 *Date:           2020-11-29
 *Description:    IFramework
 *History:        2018.11--
*********************************************************************************/
using System.Collections.Generic;
using System;
using UnityEngine;

namespace IFramework
{
    public delegate bool TimerFunc(float time, float delta);
    public delegate void TimerAction(float time, float delta);
    public delegate ITimerContext TimerContextCreate(ITimerScheduler scheduler);

    public interface ITimerScheduler
    {
        ITimerSequence NewTimerSequence();
        ITimerParallel NewTimerParallel();
        T NewTimerContext<T>() where T : TimerContext, new();
        ITimerContext RunTimerContext(TimerContext context);
    }
    public interface ITimerSequence : ITimerContext
    {
        ITimerSequence NewContext(TimerContextCreate func);
        ITimerSequence Run();
    }
    public interface ITimerParallel : ITimerContext
    {
        ITimerParallel NewContext(TimerContextCreate func);
        ITimerParallel Run();
    }


    public interface ITimerContextBox
    {
        void AddTimer(ITimerContext context);
        void CompleteTimer(ITimerContext context);
        void CompleteTimers();
        void CancelTimers();
        void CancelTimer(ITimerContext context);
    }
    public interface ITimerContext
    {
        //string guid { get; }
        bool isDone { get; }
        bool canceled { get; }

    }


    public abstract class TimerContextBase : ITimerContext, IPoolObject
    {
        //public string guid { get; private set; } = Guid.NewGuid().ToString();

        public bool isDone { get; private set; }
        public bool canceled { get; private set; }
        public bool valid { get; set; }
        internal TimerScheduler scheduler;

        //public bool valid { get; internal set; }
        private Action<ITimerContext> onComplete;
        private Action<ITimerContext> onCancel;
        private TimerAction onTick;
        protected float timeScale { get; private set; }
        public void OnComplete(Action<ITimerContext> action) => onComplete += action;
        public void OnCancel(Action<ITimerContext> action) => onCancel += action;
        public void OnTick(TimerAction action) => onTick += action;

        protected void InvokeTick(float time, float delta)
        {
            onTick?.Invoke(time, delta);

        }
        protected void InvokeCancel()
        {
            if (canceled) return;

            canceled = true;
            onCancel?.Invoke(this);
        }
        protected void InvokeComplete()
        {
            if (isDone) return;
            isDone = true;
            onComplete?.Invoke(this);
        }

        protected virtual void Reset()
        {
            onCancel = null;
            isDone = false;
            canceled = false;

            onComplete = null;
            onTick = null;
            timeScale = 1;
        }



        public abstract void Cancel();
        public abstract void Complete();


        public virtual void SetTimeScale(float timeScale)
        {
            if (!valid) return;
            this.timeScale = timeScale;
        }

        public abstract void Pause();

        public abstract void UnPause();






        void IPoolObject.OnGet() => Reset();

        void IPoolObject.OnSet() => Reset();
    }
    public abstract class TimerContext : TimerContextBase, ITimerContext
    {

        private float time;
        private bool pause;
        public void Update(float delta)
        {
            if (canceled || pause || isDone) return;
            delta *= timeScale;
            time += delta;

            InvokeTick(time, delta);
            OnUpdate(time, delta);
        }




        protected abstract void OnUpdate(float time, float delta);
        public override void Pause() => pause = true;
        public override void UnPause() => pause = false;
        protected override void Reset()
        {
            base.Reset();
            this.time = 0;

        }
        public override void Complete()
        {
            if (isDone) return;
            InvokeComplete();
        }

        public override void Cancel()
        {
            if (canceled) return;
            InvokeCancel();
        }


    }
    class TimerScheduler : UpdateModule, ITimerScheduler
    {
        private SimpleObjectPool<TimerSequence> seuqencesPool;
        private SimpleObjectPool<TimerParallel> parallelPool;

        //private SimpleObjectPool<Timer> pool;
        private List<TimerContext> timers;

        private Dictionary<Type, ISimpleObjectPool> contextPools;


        public ITimerSequence NewTimerSequence()
        {
            var seq = seuqencesPool.Get();
            seq.scheduler = this;
            return seq;
        }
        public ITimerParallel NewTimerParallel()
        {
            var seq = parallelPool.Get();
            seq.scheduler = this;
            return seq;
        }
        public void Cycle(TimerSequence seq) => seuqencesPool.Set(seq);
        public void Cycle(TimerParallel parallel) => parallelPool.Set(parallel);
        public T NewTimerContext<T>() where T : TimerContext, new()
        {
            Type type = typeof(T);
            ISimpleObjectPool pool = null;
            if (!contextPools.TryGetValue(type, out pool))
            {
                pool = new SimpleObjectPool<T>();
                contextPools.Add(type, pool);
            }
            var simple = pool as SimpleObjectPool<T>;
            var cls = simple.Get();
            cls.scheduler = this;
            return cls;
        }
        private void Cycle(TimerContext context)
        {
            var type = context.GetType();
            ISimpleObjectPool _pool = null;
            if (contextPools.TryGetValue(type, out _pool))
                _pool.SetObject(context);
            //pool.Set(timer);
        }

        public ITimerContext RunTimerContext(TimerContext context)
        {
            context.UnPause();
            timers.Add(context);
            return context;
        }

        protected override void Awake()
        {
            parallelPool = new SimpleObjectPool<TimerParallel>();
            seuqencesPool = new SimpleObjectPool<TimerSequence>();
            contextPools = new Dictionary<Type, ISimpleObjectPool>();
            //pool = new SimpleObjectPool<Timer>();
            timers = new List<TimerContext>();
        }

        protected override void OnDispose()
        {

            timers.Clear();
        }


        static DateTime last;
        private static float GetRealDeltaTime()
        {
            var now = DateTime.Now;
            var result = (now - last).TotalSeconds;
            last = now;
            return Mathf.Min((float)result, 0.02f);
        }


        protected override void OnUpdate()
        {
            float deltaTime = Time.deltaTime;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                deltaTime = GetRealDeltaTime();

#endif
            for (int i = timers.Count - 1; i >= 0; i--)
            {


                var timer = timers[i];
                if (timer.canceled)
                {
                    timers.RemoveAt(i);
                    Cycle(timer);
                    continue;
                }

                timer.Update(deltaTime);
                if (timer.isDone)
                {
                    timers.RemoveAt(i);
                    Cycle(timer);
                }
            }


        }


    }
    public class TimerContextBox : ITimerContextBox
    {
        private List<ITimerContext> contexts = new List<ITimerContext>();
        public void AddTimer(ITimerContext context)
        {
            contexts.Add(context);
            context.OnComplete(RemoveContext);
            context.OnCancel(RemoveContext);

        }

        private void RemoveContext(ITimerContext context)
        {
            if (contexts.Contains(context))
            {
                contexts.Remove(context);
            }
        }

        public void CancelTimer(ITimerContext context)
        {
            if (!contexts.Contains(context)) return;
            context.Cancel();
            RemoveContext(context);
        }

        public void CancelTimers()
        {
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.Cancel();
            }
            contexts.Clear();
        }

        public void CompleteTimer(ITimerContext context)
        {
            if (!contexts.Contains(context)) return;
            context.Complete();
            RemoveContext(context);
        }
        public void CompleteTimers()
        {
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.Complete();
            }
            contexts.Clear();
        }
    }

    class TimerSequence : TimerContextBase, ITimerSequence
    {
        private Queue<TimerContextCreate> queue = new Queue<TimerContextCreate>();
        public ITimerSequence NewContext(TimerContextCreate func)
        {
            if (func == null) return this;
            queue.Enqueue(func);
            return this;
        }

        public override void Cancel()
        {
            if (canceled) return;
            inner?.Cancel();
            InvokeCancel();
            scheduler.Cycle(this);

        }

        protected override void Reset()
        {
            base.Reset();
            inner = null;
            queue.Clear();
        }



        public override void Complete()
        {
            if (isDone) return;
            InvokeComplete();

            scheduler.Cycle(this);

        }

        private ITimerContext inner;

        private void RunNext(ITimerContext context)
        {
            if (canceled || isDone) return;
            if (queue.Count > 0)
            {
                inner = queue.Dequeue().Invoke(this.scheduler);
                if (inner != null)
                {
                    inner.OnTick(InvokeTick);
                    inner.OnCancel(RunNext);
                    inner.OnComplete(RunNext);
                    inner.SetTimeScale(timeScale);
                }
                else
                {
                    RunNext(context);
                }
            }
            else
            {
                Complete();
            }
        }

        public ITimerSequence Run()
        {
            RunNext(null);
            return this;
        }

        public override void SetTimeScale(float timeScale)
        {
            if (!valid) return;
            base.SetTimeScale(timeScale);
            inner?.SetTimeScale(timeScale);
        }

        public override void Pause()
        {
            if (!valid) return;
            inner?.Pause();
        }

        public override void UnPause()
        {
            if (!valid) return;
            inner?.UnPause();
        }







    }
    class TimerParallel : TimerContextBase, ITimerParallel
    {

        private Queue<TimerContextCreate> queue = new Queue<TimerContextCreate>();
        private List<ITimerContext> contexts = new List<ITimerContext>();

        public ITimerParallel NewContext(TimerContextCreate func)
        {
            if (func == null) return this;
            queue.Enqueue(func);
            return this;
        }


        public override void Cancel()
        {
            if (canceled) return;
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.Cancel();
            }
            InvokeCancel();
            scheduler.Cycle(this);
        }

        protected override void Reset()
        {
            base.Reset();
            this._delta = this._time = -1;
            queue.Clear();
            contexts.Clear();
        }



        public override void Complete()
        {
            if (isDone) return;
            InvokeComplete();

            scheduler.Cycle(this);

        }



        private void OnContextEnd(ITimerContext context)
        {
            if (canceled || isDone) return;
            if (contexts.Count > 0)
                contexts.Remove(context);
            if (contexts.Count == 0)
                Complete();
        }

        public ITimerParallel Run()
        {


            if (queue.Count > 0)
                while (queue.Count > 0)
                {
                    var context = queue.Dequeue().Invoke(this.scheduler);
                    context.OnCancel(OnContextEnd);
                    context.OnComplete(OnContextEnd);
                    context.OnTick(_OnTick);
                    context.SetTimeScale(timeScale);

                    contexts.Add(context);
                }
            else
                Complete();
            return this;
        }

        private float _time, _delta;
        private void _OnTick(float time, float delta)
        {
            if (_time != time || _delta != delta)
            {
                this._time = time;
                this._delta = delta;
                InvokeTick(this._time, this._delta);

            }
        }

        public override void SetTimeScale(float timeScale)
        {
            if (!valid) return;
            base.SetTimeScale(timeScale);
            for (var i = 0; i < contexts.Count; i++)
                contexts[i].SetTimeScale(timeScale);
        }

        public override void Pause()
        {
            if (!valid) return;
            for (var i = 0; i < contexts.Count; i++)
                contexts[i].Pause();
        }

        public override void UnPause()
        {
            if (!valid) return;
            for (var i = 0; i < contexts.Count; i++)
                contexts[i].UnPause();
        }
    }


    public static class TimeEx
    {
        const float min2Delta = 0.00001f;
        public static IAwaiter<T> GetAwaiter<T>(this T context) where T : ITimerContext => new ITimerContextAwaitor<T>(context);
        public static T AddTo<T>(this T context, ITimerContextBox box) where T : ITimerContext
        {
            box.AddTimer(context);
            return context;
        }
        public static T OnComplete<T>(this T context, Action<ITimerContext> action) where T : ITimerContext
        {
            context.AsContextBase().OnComplete(action);
            return context;
        }
        public static T OnCancel<T>(this T context, Action<ITimerContext> action) where T : ITimerContext
        {
            context.AsContextBase().OnCancel(action);
            return context;
        }
        public static T OnTick<T>(this T context, TimerAction action) where T : ITimerContext
        {
            context.AsContextBase().OnTick(action);
            return context;
        }

        public static T SetTimeScale<T>(this T t, float timeScale) where T : ITimerContext
        {
            t.AsContextBase().SetTimeScale(timeScale);
            return t;
        }
        public static T Pause<T>(this T t) where T : ITimerContext
        {
            t.AsContextBase().Pause();

            return t;
        }
        public static T UnPause<T>(this T t) where T : ITimerContext
        {
            t.AsContextBase().UnPause();
            return t;
        }
        public static T Complete<T>(this T t) where T : ITimerContext
        {
            t.AsContextBase().Complete();
            return t;
        }
        public static T Cancel<T>(this T t) where T : ITimerContext
        {
            t.AsContextBase().Cancel();
            return t;
        }
        private static TimerContextBase AsContextBase(this ITimerContext t) => t as TimerContextBase;




        public static ITimerContext Until(this ITimerScheduler scheduler, TimerFunc condition, float interval = min2Delta)
        {
            var cls = scheduler.NewTimerContext<ConditionTimerContext>();
            bool succ = cls.Condition(condition, interval, true, false);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext While(this ITimerScheduler scheduler, TimerFunc condition, float interval = min2Delta)
        {
            var cls = scheduler.NewTimerContext<ConditionTimerContext>();
            bool succ = cls.Condition(condition, interval, false, false);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext DoWhile(this ITimerScheduler scheduler, TimerFunc condition, float interval = min2Delta)
        {
            var cls = scheduler.NewTimerContext<ConditionTimerContext>();

            bool succ = cls.Condition(condition, interval, false, true);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext DoUntil(this ITimerScheduler scheduler, TimerFunc condition, float interval = min2Delta)
        {
            var cls = scheduler.NewTimerContext<ConditionTimerContext>();
            bool succ = cls.Condition(condition, interval, true, true);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext Delay(this ITimerScheduler scheduler, float delay, TimerAction action = null)
        {
            var cls = scheduler.NewTimerContext<TickTimerContext>();
            bool succ = cls.Delay(delay, action);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext Frame(this ITimerScheduler scheduler) => scheduler.Delay(min2Delta);
        public static ITimerContext Tick(this ITimerScheduler scheduler, float interval, int times, TimerAction action)
        {
            var cls = scheduler.NewTimerContext<TickTimerContext>();
            bool succ = cls.Tick(interval, times, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }
        public static ITimerContext DelayAndTick(this ITimerScheduler scheduler, float delay, TimerAction delayCall, float interval, int times, TimerAction action)
        {
            var cls = scheduler.NewTimerContext<TickTimerContext>();
            bool succ = cls.DelayAndTick(delay, delayCall, interval, times, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }



    }




    class ConditionTimerContext : TimerContext
    {
        private void CalcNextTime(float time) => nextIntervalTime = interval + time;
        private float interval;
        private TimerFunc interverl_call;
        private bool callFirst;
        private bool resultFlag;

        public bool Condition(TimerFunc condition, float interval, bool resultFlag, bool callFirst)
        {
            if (interval <= 0)
            {
                Log.FE($"Err Param {nameof(interval)}:{interval}");
                Complete();
                return false;
            }
            this.callFirst = callFirst;
            this.resultFlag = resultFlag;
            this.interverl_call = condition;
            this.interval = interval;
            return true;
        }

        private void OnceCall(float time, float delta)
        {
            bool end = interverl_call.Invoke(time, delta);
            if (end != this.resultFlag)
                CalcNextTime(time);
            else
                Complete();
        }
        protected override void OnUpdate(float time, float delta)
        {
            if (!trick_mode_first)
            {
                if (callFirst)
                    OnceCall(time, delta);
                trick_mode_first = true;
                if (isDone) return;
                CalcNextTime(time);
            }
            if (time >= nextIntervalTime)
            {
                OnceCall(time, delta);
            }
        }
        private bool trick_mode_first;
        private float nextIntervalTime;
        protected override void Reset()
        {
            base.Reset();
            trick_mode_first = false;
            nextIntervalTime = 0;

        }
    }
    class TickTimerContext : TimerContext
    {
        private enum Mode
        {
            Delay,
            Tick,
            DelayAndTick
        }

        private Mode mode;
        private float delay;
        private TimerAction _delay_call_delegate;


        private int _tick_times;
        private float interval;
        private TimerAction _tick_call_delegate;

        public bool Delay(float delay, TimerAction action)
        {
            if (!CheckDelay(delay)) return false;
            mode = Mode.Delay;
            this.delay = delay;
            this._delay_call_delegate = action;
            return true;
        }

        private bool CheckDelay(float delay)
        {
            if (delay <= 0)
            {
                Log.FE($"Err Param {nameof(delay)}:{delay}");
                Complete();
                return false;
            }
            return true;
        }
        private bool CheckTick(float interval, int times)
        {
            if (times == 0 || interval <= 0)
            {
                Log.FE($"Err Param {nameof(interval)}:{interval}  {nameof(times)}:{times}");
                Complete();
                return false;
            }
            return true;
        }
        public bool Tick(float interval, int times, TimerAction action)
        {
            if (!CheckTick(interval, times)) return false;
            mode = Mode.Tick;
            this.interval = interval;
            this._tick_call_delegate = action;
            this._tick_times = times;
            return true;
        }
        public bool DelayAndTick(float delay, TimerAction delayCall, float interval, int times, TimerAction action)
        {
            bool succeed = true;
            succeed &= Delay(delay, delayCall);
            succeed &= Tick(interval, times, action);
            mode = Mode.DelayAndTick;
            return succeed;
        }



        private void CallDelay(float time, float delta)
        {

            _delay_call_delegate?.Invoke(time, delta);
            delay_called = true;
        }

        private void CallTick(float time, float delta) => _tick_call_delegate?.Invoke(time, delta);



        private bool tick_mode_first;
        private bool delay_called;
        private float nextIntervalTime;
        private int _times;
        private void CalcNextTime(float time)
        {
            if (_tick_times != -1 && _times >= _tick_times)
            {
                Complete();
                return;
            }
            _times++;
            nextIntervalTime = interval + time;
        }
        protected override void OnUpdate(float time, float delta)
        {
            if (mode == Mode.Tick && !tick_mode_first)
            {
                tick_mode_first = true;
                CalcNextTime(time);
            }
            if (mode == Mode.DelayAndTick || mode == Mode.Delay)
            {
                if (!delay_called && time >= delay)
                {
                    CallDelay(time, delta);
                    if (mode == Mode.Delay)
                        Complete();
                    else
                        CalcNextTime(time);
                }
            }

            if (mode == Mode.Tick || (mode == Mode.DelayAndTick && delay_called))
            {
                if (time >= nextIntervalTime)
                {
                    CallTick(time, delta);
                    CalcNextTime(time);
                }
            }

        }

        protected override void Reset()
        {
            base.Reset();
            tick_mode_first = false;
            nextIntervalTime = 0;
            _times = 0;
            delay_called = false;
        }
    }
    struct ITimerContextAwaitor<T> : IAwaiter<T> where T : ITimerContext
    {
        private T op;
        private Queue<Action> actions;
        public ITimerContextAwaitor(T op)
        {
            this.op = op;
            actions = new Queue<Action>();
            op.OnComplete(OnCompleted);
        }

        private void OnCompleted(ITimerContext context)
        {
            while (actions.Count > 0)
            {
                actions.Dequeue()?.Invoke();
            }
        }

        public bool IsCompleted => op.isDone;

        public T GetResult() => op;
        public void OnCompleted(Action continuation)
        {
            actions?.Enqueue(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            OnCompleted(continuation);
        }
    }

}
