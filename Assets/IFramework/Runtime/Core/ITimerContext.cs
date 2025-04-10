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
    public interface ITimerScheduler
    {
        T AllocateTimerContext<T>() where T : TimerContext, new();
        ITimerContext RunTimerContext(TimerContext context);
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
        bool isDone { get; }
        bool canceled { get; }
        void Pause();
        void UnPause();
        void Complete();
        void Cancel();
    }

    class Timer : IPoolObject
    {
        public bool isDone { get; private set; }
        public bool canceled { get; private set; }
        private float time;
        public TimerContext context { get; private set; }
        private bool pause;

        void IPoolObject.OnGet() => Reset();
        void IPoolObject.OnSet() => Reset();
        internal void Reset()
        {
            this.time = 0;
            this.context = null;
            pause = false;
        }
        public void Config(TimerContext context)
        {
            Pause();
            this.context = context;
            context.Config(this);
            isDone = false;
            canceled = false;
        }
        public void Pause()
        {
            pause = true;
        }
        public void UnPause()
        {
            pause = false;
        }
        public void Update(float deltaTime)
        {
            if (canceled || pause || isDone) return;
            time += deltaTime;
            context.Update(time);
        }
        public void InvokeComplete()
        {
            if (isDone) return;
            isDone = true;

        }

        internal void Cancel()
        {
            canceled = true;
            Pause();
        }


    }
    public abstract class TimerContext : ITimerContext, IPoolObject
    {
        private Timer timer;
        internal Action completed;

        public bool isDone => timer.isDone;
        public bool canceled => timer.canceled;

        internal void Update(float time) => OnUpdate(time);
        protected abstract void OnUpdate(float time);
        public void Pause() => timer.Pause();
        public void UnPause() => timer.UnPause();
        internal void Config(Timer timer)
        {
            this.timer = timer;
        }
        protected virtual void Reset() => this.timer = null;
        public void Complete()
        {
            this.timer.InvokeComplete();
            completed?.Invoke();
            completed = null;
        }

        public void Cancel()
        {
            this.timer.Cancel();
            completed = null;

        }

        void IPoolObject.OnGet() => Reset();

        void IPoolObject.OnSet() => Reset();
    }


    class TimerScheduler : UpdateModule, ITimerScheduler
    {
        private SimpleObjectPool<Timer> pool;
        private List<Timer> timers;
        private Dictionary<Type, ISimpleObjectPool> contextPools;
        public T AllocateTimerContext<T>() where T : TimerContext, new()
        {
            Type type = typeof(T);
            ISimpleObjectPool pool = null;
            if (!contextPools.TryGetValue(type, out pool))
            {
                pool = new SimpleObjectPool<T>();
                contextPools.Add(type, pool);
            }
            var simple = pool as SimpleObjectPool<T>;
            return simple.Get();
        }
        private void Cycle(Timer timer)
        {

            var type = timer.context.GetType();
            ISimpleObjectPool _pool = null;
            if (!contextPools.TryGetValue(type, out _pool))
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
        }

        public void CancelTimer(ITimerContext context)
        {
            if (!contexts.Contains(context)) return;
            context.Cancel();
            contexts.Remove(context);
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
            contexts.Remove(context);
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
    public static class TimeEx
    {
        const float min2Delta = 0.00001f;
        public static IAwaiter<ITimerContext> GetAwaiter(this ITimerContext context) => new ITimerContextAwaitor(context);
        public static ITimerContext AddTo(this ITimerContext context, ITimerContextBox box)
        {
            box.AddTimer(context);
            return context;
        }
        public static ITimerContext Until(this ITimerScheduler scheduler, Func<float, bool> condition, float interval = min2Delta)
        {
            var cls = scheduler.AllocateTimerContext<ConditionTimerContext>();
            bool succ = cls.Condition(condition, interval, true, false);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext While(this ITimerScheduler scheduler, Func<float, bool> condition, float interval = min2Delta)
        {
            var cls = scheduler.AllocateTimerContext<ConditionTimerContext>();
            bool succ = cls.Condition(condition, interval, false, false);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext DoWhile(this ITimerScheduler scheduler, Func<float, bool> condition, float interval = min2Delta)
        {
            var cls = scheduler.AllocateTimerContext<ConditionTimerContext>();

            bool succ = cls.Condition(condition, interval, false, true);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext DoUntil(this ITimerScheduler scheduler, Func<float, bool> condition, float interval = min2Delta)
        {
            var cls = scheduler.AllocateTimerContext<ConditionTimerContext>();
            bool succ = cls.Condition(condition, interval, true, true);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext Delay(this ITimerScheduler scheduler, float delay, Action<float> action = null)
        {
            var cls = scheduler.AllocateTimerContext<TickTimerContext>();
            bool succ = cls.Delay(delay, action);
            return succ ? scheduler.RunTimerContext(cls) : null;
        }
        public static ITimerContext Frame(this ITimerScheduler scheduler) => scheduler.Delay(min2Delta);
        public static ITimerContext Tick(this ITimerScheduler scheduler, float interval, int times, Action<float> action)
        {
            var cls = scheduler.AllocateTimerContext<TickTimerContext>();
            bool succ = cls.Tick(interval, times, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }
        public static ITimerContext DelayAndTick(this ITimerScheduler scheduler, float delay, Action<float> delayCall, float interval, int times, Action<float> action)
        {
            var cls = scheduler.AllocateTimerContext<TickTimerContext>();
            bool succ = cls.DelayAndTick(delay, delayCall, interval, times, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }



    }
    class ConditionTimerContext : TimerContext
    {
        private void CalcNextTime(float time) => nextIntervalTime = interval + time;
        private float interval;
        private Func<float, bool> interverl_call;
        private bool callFirst;
        private bool resultFlag;

        public bool Condition(Func<float, bool> condition, float interval, bool resultFlag, bool callFirst)
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

        private void OnceCall(float time)
        {
            bool end = interverl_call.Invoke(time);
            if (end != this.resultFlag)
                CalcNextTime(time);
            else
                Complete();
        }
        protected override void OnUpdate(float time)
        {
            if (!trick_mode_first)
            {
                if (callFirst)
                    OnceCall(time);
                trick_mode_first = true;
                if (isDone) return;
                CalcNextTime(time);
            }
            if (time >= nextIntervalTime)
            {
                OnceCall(time);
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
        private Action<float> _delay_call_delegate;


        private int _tick_times;
        private float interval;
        private Action<float> _tick_call_delegate;

        public bool Delay(float delay, Action<float> action)
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
        public bool Tick(float interval, int times, Action<float> action)
        {
            if (!CheckTick(interval, times)) return false;
            mode = Mode.Tick;
            this.interval = interval;
            this._tick_call_delegate = action;
            this._tick_times = times;
            return true;
        }
        public bool DelayAndTick(float delay, Action<float> delayCall, float interval, int times, Action<float> action)
        {
            bool succeed = true;
            succeed &= Delay(delay, delayCall);
            succeed &= Tick(interval, times, action);
            mode = Mode.DelayAndTick;
            return succeed;
        }



        private void CallDelay(float time)
        {

            _delay_call_delegate?.Invoke(time);
            delay_called = true;
        }

        private void CallTick(float time) => _tick_call_delegate?.Invoke(time);



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
        protected override void OnUpdate(float time)
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
                    CallDelay(time);
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
                    CallTick(time);
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
    struct ITimerContextAwaitor : IAwaiter<ITimerContext>
    {
        private ITimerContext op;
        private Queue<Action> actions;
        public ITimerContextAwaitor(ITimerContext op)
        {
            this.op = op;
            actions = new Queue<Action>();
            (op as TimerContext).completed += OnCompleted;
        }

        private void OnCompleted()
        {
            while (actions.Count > 0)
            {
                actions.Dequeue()?.Invoke();
            }
        }

        public bool IsCompleted => op.isDone;

        public ITimerContext GetResult() => op;
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
