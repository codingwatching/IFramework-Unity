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

    public interface ITimerContext
    {
        bool isDone { get; }
        bool canceled { get; }
        void Pause();
        void UnPause();
        void Complete();
        void Cancel();
    }
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

        public void Cancel() {
            this.timer.Cancel();
            completed = null;

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
    class NormalTimerContext : TimerContext
    {
        private enum Mode
        {
            Delay,
            Trick,
            DelayAndTrick
        }
        private Mode mode;
        private float delay;
        private Action delay_call;
        private int times;
        private float interval;
        private Action interverl_call;

        public bool Delay(float delay, Action action)
        {
            mode = Mode.Delay;
            if (delay <= 0)
            {
                Log.FE($"Err Param {nameof(delay)}:{delay}");
                Complete();
                return false;
            }
            this.delay = delay;
            this.delay_call = action;
            return true;
        }

        private bool Check(float interval, int times)
        {
            if (times == 0 || interval <= 0)
            {
                Log.FE($"Err Param {nameof(interval)}:{interval}  {nameof(times)}:{times}");
                Complete();
                return false;
            }
            return true;
        }

        public bool Trick(float interval, int times, Action action)
        {
            if (!Check(interval, times)) return false;
            mode = Mode.Trick;
            this.interval = interval;
            this.interverl_call = action;
            this.times = times;
            return true;
        }
        public bool DelayAndTrick(float delay, Action delayCall, float interval, int times, Action action)
        {
            bool succeed = true;
            succeed &= Delay(delay, delayCall);
            succeed &= Trick(interval, times, action);
            mode = Mode.DelayAndTrick;
            return succeed;
        }

        private bool trick_mode_first;
        private bool delay_called;
        private float nextIntervalTime;
        private int _times;
        private void CalcNextTime(float time)
        {
            if (times != -1 && _times >= times)
            {
                Complete();
                return;
            }
            _times++;
            nextIntervalTime = interval + time;
        }
        protected override void OnUpdate(float time)
        {
            if (mode == Mode.Trick && !trick_mode_first)
            {
                trick_mode_first = true;
                CalcNextTime(time);
            }
            if (mode == Mode.DelayAndTrick || mode == Mode.Delay)
            {
                if (!delay_called && time >= delay)
                {
                    delay_call?.Invoke();
                    delay_called = true;
                    if (mode == Mode.Delay)
                    {
                        Complete();
                    }
                    else
                    {
                        CalcNextTime(time);
                    }

                }
            }

            if (mode == Mode.Trick || (mode == Mode.DelayAndTrick && delay_called))
            {
                if (time >= nextIntervalTime)
                {
                    interverl_call?.Invoke();
                    CalcNextTime(time);
                }
            }

        }

        protected override void Reset()
        {
            trick_mode_first = false;
            nextIntervalTime = 0;
            _times = 0;
            delay_called = false;
        }
    }
    class TimerModule : UpdateModule
    {
        private class ConditionPool : ObjectPool<ConditionTimerContext>
        {
            protected override ConditionTimerContext CreateNew() => new ConditionTimerContext();
        }
        private class ContextPool : ObjectPool<NormalTimerContext>
        {
            protected override NormalTimerContext CreateNew() => new NormalTimerContext();
        }
        private class Pool : ObjectPool<Timer>
        {
            protected override Timer CreateNew() => new Timer();
        }
        private Pool pool;
        private ContextPool contextPool;
        private ConditionPool conditionPool;

        private List<Timer> timers;
        private Timer Allocate(TimerContext context)
        {
            var cls = pool.Get();
            cls.Config(context);
            timers.Add(cls);
            return cls;
        }
        public ITimerContext Custom(TimerContext context)
        {
            var timer = Allocate(context);
            timer.UnPause();
            return context;
        }


        public ITimerContext Until(Func<bool> condition, float interval)
        {
            var cls = conditionPool.Get();
            bool succ = cls.Condition(condition, interval, true);
            if (!succ) return null;
            var timer = Allocate(cls);
            timer.UnPause();
            return cls;
        }
        public ITimerContext While(Func<bool> condition, float interval)
        {
            var cls = conditionPool.Get();
            bool succ = cls.Condition(condition, interval, false);
            if (!succ) return null;
            var timer = Allocate(cls);
            timer.UnPause();
            return cls;
        }

        public ITimerContext Delay(float delay, Action action)
        {
            var cls = contextPool.Get();
            bool succ = cls.Delay(delay, action);
            if (!succ) return null;
            var timer = Allocate(cls);
            timer.UnPause();
            return cls;
        }

        public ITimerContext Trick(float interval, int times, Action action)
        {
            var cls = contextPool.Get();
            bool succ = cls.Trick(interval, times, action);
            if (!succ) return null;
            var timer = Allocate(cls);
            timer.UnPause();
            return cls;
        }
        public ITimerContext DelayAndTrick(float delay, Action delayCall, float interval, int times, Action action)
        {
            var cls = contextPool.Get();
            bool succ = cls.DelayAndTrick(delay, delayCall, interval, times, action);
            if (!succ) return null;
            var timer = Allocate(cls);
            timer.UnPause();
            return cls;
        }

        protected override void Awake()
        {
            contextPool = new ContextPool();
            pool = new Pool();
            conditionPool = new ConditionPool();
            timers = new List<Timer>();
        }

        protected override void OnDispose()
        {
            timers.Clear();
        }
        private void Cycle(Timer timer)
        {
            pool.Set(timer);
            if (timer.context is NormalTimerContext)
                contextPool.Set(timer.context as NormalTimerContext);
            if (timer.context is ConditionTimerContext)
                conditionPool.Set(timer.context as ConditionTimerContext);
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
    public interface ITimerContextBox
    {
        void AddTimer(ITimerContext context);
        void CompleteTimer(ITimerContext context);
        void CompleteTimers();
        void CancelTimers();
        void CancelTimer(ITimerContext context);
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

    }
}
