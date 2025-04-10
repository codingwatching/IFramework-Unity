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
    public interface ITimerAction
    {
        void Act();
    }
    public interface ITimerDelayAction : ITimerAction { }
    public interface ITimerTickAction : ITimerAction { }

    class Timer
    {
        public bool isDone { get; private set; }
        public bool canceled { get; private set; }
        private float time;
        public TimerContext context { get; private set; }
        private bool pause;
        public void Config(TimerContext context)
        {
            Pause();
            this.time = 0;
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
    public abstract class TimerContext : ITimerContext
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
            Reset();
        }
        protected abstract void Reset();
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
            pool.Set(timer);
            var type = timer.context.GetType();
            ISimpleObjectPool _pool = null;
            if (!contextPools.TryGetValue(type, out _pool)) return;
            _pool.SetObject(timer.context);

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

        protected override void OnUpdate()
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {


                var timer = timers[i];
                if (timer.canceled)
                {
                    timers.RemoveAt(i);
                    Cycle(timer);
                    continue;
                }

                timer.Update(Time.deltaTime);
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

        public static IAwaiter<ITimerContext> GetAwaiter(this ITimerContext context)
        {
            return new ITimerContextAwaitor(context);
        }
        public static ITimerContext AddTo(this ITimerContext context, ITimerContextBox box)
        {
            box.AddTimer(context);
            return context;
        }
        public static ITimerContext Until(this ITimerScheduler scheduler, Func<bool> condition, float interval = 0.01f)
        {
            var cls = scheduler.AllocateTimerContext<ConditionTimerContext>();
            bool succ = cls.Condition(condition, interval, true);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }
        public static ITimerContext While(this ITimerScheduler scheduler, Func<bool> condition, float interval = 0.01f)
        {
            var cls = scheduler.AllocateTimerContext<ConditionTimerContext>();
            bool succ = cls.Condition(condition, interval, false);
            return succ ? scheduler.RunTimerContext(cls) : null;


        }

        public static ITimerContext Delay(this ITimerScheduler scheduler, float delay, Action action = null)
        {
            var cls = scheduler.AllocateTimerContext<TickTimerContext>();
            bool succ = cls.Delay(delay, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }

        public static ITimerContext Tick(this ITimerScheduler scheduler, float interval, int times, Action action)
        {
            var cls = scheduler.AllocateTimerContext<TickTimerContext>();
            bool succ = cls.Tick(interval, times, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }
        public static ITimerContext DelayAndTick(this ITimerScheduler scheduler, float delay, Action delayCall, float interval, int times, Action action)
        {
            var cls = scheduler.AllocateTimerContext<TickTimerContext>();
            bool succ = cls.DelayAndTick(delay, delayCall, interval, times, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }

        public static ITimerContext Delay(this ITimerScheduler scheduler, float delay, ITimerDelayAction action)
        {
            var cls = scheduler.AllocateTimerContext<TickTimerContext>();
            bool succ = cls.Delay(delay, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }

        public static ITimerContext Tick(this ITimerScheduler scheduler, float interval, int times, ITimerTickAction action)
        {
            var cls = scheduler.AllocateTimerContext<TickTimerContext>();
            bool succ = cls.Tick(interval, times, action);
            return succ ? scheduler.RunTimerContext(cls) : null;

        }

        public static ITimerContext DelayAndTick(this ITimerScheduler scheduler, float delay, ITimerDelayAction delayCall, float interval, int times, ITimerTickAction action)
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
        private Func<bool> interverl_call;
        private bool succeed;

        public bool Condition(Func<bool> condition, float interval, bool succeed)
        {
            if (interval <= 0)
            {
                Log.FE($"Err Param {nameof(interval)}:{interval}");
                Complete();
                return false;
            }
            this.succeed = succeed;
            this.interverl_call = condition;
            this.interval = interval;
            return true;
        }
        protected override void OnUpdate(float time)
        {
            if (!trick_mode_first)
            {
                trick_mode_first = true;
                CalcNextTime(time);
            }
            if (time >= nextIntervalTime)
            {
                bool end = interverl_call.Invoke();
                if (end != this.succeed)
                    CalcNextTime(time);
                else
                    Complete();
            }
        }
        private bool trick_mode_first;
        private float nextIntervalTime;
        protected override void Reset()
        {
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

        private bool isdelegate;
        private Mode mode;
        private float delay;
        private Action _delay_call_delegate;
        private ITimerDelayAction _delay_call_interface;


        private int _tick_times;
        private float interval;
        private Action _tick_call_delegate;
        private ITimerTickAction _tick_call_interface;

        public bool Delay(float delay, Action action)
        {
            if (!CheckDelay(delay)) return false;
            isdelegate = false;
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
        public bool Tick(float interval, int times, Action action)
        {
            if (!CheckTick(interval, times)) return false;
            isdelegate = false;
            mode = Mode.Tick;
            this.interval = interval;
            this._tick_call_delegate = action;
            this._tick_times = times;
            return true;
        }
        public bool DelayAndTick(float delay, Action delayCall, float interval, int times, Action action)
        {
            bool succeed = true;
            succeed &= Delay(delay, delayCall);
            succeed &= Tick(interval, times, action);
            mode = Mode.DelayAndTick;
            return succeed;
        }

        public bool Delay(float delay, ITimerDelayAction action)
        {
            if (!CheckDelay(delay)) return false;
            isdelegate = true;
            mode = Mode.Delay;
            this.delay = delay;
            this._delay_call_interface = action;
            return true;
        }

        public bool Tick(float interval, int times, ITimerTickAction action)
        {
            if (!CheckTick(interval, times)) return false;
            isdelegate = true;
            mode = Mode.Tick;
            this.interval = interval;
            this._tick_call_interface = action;
            this._tick_times = times;
            return true;
        }
        public bool DelayAndTick(float delay, ITimerDelayAction delayCall, float interval, int times, ITimerTickAction action)
        {
            bool succeed = true;
            succeed &= Delay(delay, delayCall);
            succeed &= Tick(interval, times, action);
            mode = Mode.DelayAndTick;
            return succeed;
        }

        private void CallDelay()
        {
            if (isdelegate)
                _delay_call_delegate?.Invoke();
            else
                _delay_call_interface?.Act();
            delay_called = true;
        }

        private void CallTick()
        {
            if (isdelegate)
                _tick_call_delegate?.Invoke();
            else
                _tick_call_interface?.Act();
        }



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
                    CallDelay();
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
                    CallTick();
                    CalcNextTime(time);
                }
            }

        }

        protected override void Reset()
        {
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
