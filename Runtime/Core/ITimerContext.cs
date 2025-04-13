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
    public delegate ITimerContext TimerSequenceCreate(ITimerScheduler scheduler);

    public interface ITimerScheduler
    {
        ITimerSequence NewTimerSequence();
        T NewTimerContext<T>() where T : TimerContext, new();
        ITimerContext RunTimerContext(TimerContext context);
    }
    public interface ITimerSequence : ITimerContext
    {
        ITimerSequence NewContext(TimerSequenceCreate func);
        ITimerSequence Run();
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
        bool valid { get; }
        bool isDone { get; }
        bool canceled { get; }
        void SetTimeScale(float timeScale);
        void Pause();
        void UnPause();
        void Complete();
        void Cancel();
        void OnComplete(Action<ITimerContext> action);
        void OnCancel(Action<ITimerContext> action);
        void OnTick(TimerAction action);
    }


    class Timer : IPoolObject
    {
        public bool isDone { get; private set; }
        public bool canceled { get; private set; }
        public bool valid { get; private set; }
        private float time;
        private float timeScale;
        public TimerContext context { get; private set; }
        private bool pause;

        void IPoolObject.OnGet()
        {
            valid = true;
            Reset();
        }

        void IPoolObject.OnSet()
        {
            valid = false;
            Reset();
        }

        private void Reset()
        {
            this.timeScale = 1;
            this.time = 0;
            this.context = null;
            this.pause = false;
        }
        public void Config(TimerContext context)
        {
            Pause();
            this.context = context;
            context.SetTimer(this);
            isDone = false;
            canceled = false;
        }
        public void Pause() => pause = true;
        public void UnPause() => pause = false;
        public void Update(float deltaTime)
        {
            if (canceled || pause || isDone) return;
            deltaTime *= timeScale;
            time += deltaTime;
            context.Update(time, deltaTime);
        }
        public void InvokeComplete()
        {
            if (isDone) return;
            isDone = true;

        }

        public void Cancel()
        {
            canceled = true;
            Pause();
        }
        public void SetTimeScale(float timeScale) => this.timeScale = timeScale;

    }
    public abstract class TimerContext : ITimerContext, IPoolObject
    {
        private Timer timer;
        private Action<ITimerContext> onComplete;
        private Action<ITimerContext> onCancel;
        private TimerAction onTick;
        public bool valid => timer == null ? false : timer.valid;

        public bool isDone => timer.isDone;
        public bool canceled => timer.canceled;

        internal void Update(float time, float delta)
        {
            onTick?.Invoke(time, delta);
            OnUpdate(time, delta);
        }
        internal void SetTimer(Timer timer) => this.timer = timer;




        protected abstract void OnUpdate(float time, float delta);
        public void Pause() => timer.Pause();
        public void UnPause() => timer.UnPause();
        protected virtual void Reset()
        {
            this.timer = null;
            onComplete = null;
            onCancel = null;
            onTick = null;
        }
        public void Complete()
        {
            this.timer.InvokeComplete();
            onComplete?.Invoke(this);
        }

        public void Cancel()
        {
            this.timer.Cancel();
            onCancel?.Invoke(this);
        }
        public void OnComplete(Action<ITimerContext> action) => onComplete += action;
        public void OnCancel(Action<ITimerContext> action) => onCancel += action;
        public void OnTick(TimerAction action) => onTick += action;

        void IPoolObject.OnGet() => Reset();

        void IPoolObject.OnSet() => Reset();

        public void SetTimeScale(float timeScale) => timer.SetTimeScale(timeScale);
    }
    class TimerScheduler : UpdateModule, ITimerScheduler
    {
        private SimpleObjectPool<TimerSequence> seuqencesPool;

        private SimpleObjectPool<Timer> pool;
        private List<Timer> timers;

        private Dictionary<Type, ISimpleObjectPool> contextPools;


        public ITimerSequence NewTimerSequence()
        {
            var seq = seuqencesPool.Get();
            seq.scheduler = this;
            return seq;
        }
        public void Cycle(TimerSequence seq) => seuqencesPool.Set(seq);

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
            return cls;
        }
        private void Cycle(Timer timer)
        {
            var type = timer.context.GetType();
            ISimpleObjectPool _pool = null;
            if (contextPools.TryGetValue(type, out _pool))
                _pool.SetObject(timer.context);
            pool.Set(timer);
        }


        private Timer Allocate(TimerContext context)
        {
            var cls = pool.Get();
            cls.Config(context);
            timers.Add(cls);
            return cls;
        }
        public ITimerContext RunTimerContext(TimerContext context)
        {
            var timer = Allocate(context);
            timer.UnPause();
            return context;
        }

        protected override void Awake()
        {
            seuqencesPool = new SimpleObjectPool<TimerSequence>();
            contextPools = new Dictionary<Type, ISimpleObjectPool>();
            pool = new SimpleObjectPool<Timer>();
            timers = new List<Timer>();
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

    class TimerSequence : ITimerSequence, IPoolObject
    {
        private Queue<TimerSequenceCreate> queue = new Queue<TimerSequenceCreate>();
        internal TimerScheduler scheduler;
        public ITimerSequence NewContext(TimerSequenceCreate func)
        {
            if (func == null) return this;
            queue.Enqueue(func);
            return this;
        }

        public bool isDone { get; private set; }
        public bool canceled { get; private set; }
        public bool valid { get; private set; }
        private Action<ITimerContext> onComplete;
        private Action<ITimerContext> onCancel;
        private TimerAction onTick;
        private float timeScele;
        public void OnComplete(Action<ITimerContext> action) => onComplete += action;
        public void OnCancel(Action<ITimerContext> action) => onCancel += action;
        public void OnTick(TimerAction action) => onTick += action;

        public void Cancel()
        {
            if (canceled) return;
            canceled = true;

            inner?.Cancel();

            onCancel?.Invoke(this);
            scheduler.Cycle(this);

        }

        private void Reset()
        {
            onCancel = null;
            isDone = false;
            canceled = false;

            onComplete = null;
            onTick = null;
            timeScele = 1;
            inner = null;
            queue.Clear();
        }



        public void Complete()
        {
            if (isDone) return;
            isDone = true;
            onComplete?.Invoke(this);

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
                    inner.OnTick(onTick);
                    inner.OnCancel(RunNext);
                    inner.OnComplete(RunNext);
                    inner.SetTimeScale(timeScele);
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
            canceled = false;
            isDone = false;
            RunNext(null);
            return this;
        }

        public void SetTimeScale(float timeScale)
        {
            if (!valid) return;
            this.timeScele = timeScale;
            inner?.SetTimeScale(timeScale);
        }

        public void Pause()
        {
            if (!valid) return;
            inner?.Pause();
        }

        public void UnPause()
        {
            if (!valid) return;
            inner?.UnPause();
        }






        void IPoolObject.OnGet()
        {
            valid = true;
            Reset();
        }

        void IPoolObject.OnSet()
        {
            valid = false;
            Reset();
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
        public static T OnCompleteEx<T>(this T context, Action<ITimerContext> action) where T : ITimerContext
        {
            context.OnComplete(action);
            return context;
        }
        public static T OnCancelEx<T>(this T context, Action<ITimerContext> action) where T : ITimerContext
        {
            context.OnCancel(action);
            return context;
        }
        public static T OnTickEx<T>(this T context, TimerAction action) where T : ITimerContext
        {
            context.OnTick(action);
            return context;
        }







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
