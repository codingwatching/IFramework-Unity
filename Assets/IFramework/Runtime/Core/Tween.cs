/*********************************************************************************
 *Author:         OnClick
 *Version:        0.0.2.116
 *UnityVersion:   2018.4.24f1
 *Date:           2020-11-29
 *Description:    IFramework
 *History:        2018.11--
*********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IFramework
{
    public enum TweenType
    {
        Normal, Shake, Punch, Jump, Bezier, Array
    }
    public enum LoopType
    {
        Restart,
        PingPong,
        Add
    }
    public enum Ease
    {
        Linear,
        InSine,
        OutSine,
        InOutSine,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InQuart,
        OutQuart,
        InOutQuart,
        InQuint,
        OutQuint,
        InOutQuint,
        InExpo,
        OutExpo,
        InOutExpo,
        InCirc,
        OutCirc,
        InOutCirc
    }
    public interface IValueEvaluator
    {
        float Evaluate(float percent, float time, float duration);
    }

    public interface ITweenContext
    {
        bool isDone { get; }
        bool autoCycle { get; }
        bool paused { get; }
    }
    public interface ITweenGroup : ITweenContext
    {
        ITweenGroup NewContext(Func<ITweenContext> func);
    }

    public interface ITweenContext<T, Target> : ITweenContext { }

    public interface ITweenContextBox
    {
        void AddTween(ITweenContext context);
        void CancelTween(ITweenContext context);
        void CancelTweenContexts();
        void CompleteTween(ITweenContext context);
        void CompleteTweenContexts();
    }
    class TweenContextBox : ITweenContextBox
    {
        private List<ITweenContext> contexts = new List<ITweenContext>();
        public void AddTween(ITweenContext context)
        {
            if (contexts.Contains(context)) return;
            contexts.Add(context);
            context.OnComplete(RemoveTween);
            context.OnCancel(RemoveTween);

        }

        private void RemoveTween(ITweenContext context)
        {
            if (contexts.Contains(context))
            {
                contexts.Remove(context);
            }
        }

        public void CancelTween(ITweenContext context)
        {
            if (!contexts.Contains(context)) return;
            context.Cancel();
            RemoveTween(context);
        }

        public void CancelTweenContexts()
        {
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.Cancel();
            }
            contexts.Clear();
        }

        public void CompleteTween(ITweenContext context)
        {
            if (!contexts.Contains(context)) return;
            context.Complete(true);
            RemoveTween(context);
        }
        public void CompleteTweenContexts()
        {
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.Complete(true);
            }
            contexts.Clear();
        }
    }


    abstract class TweenContextBase : ITweenContext, IPoolObject
    {

        public void Recycle()
        {
            if (!valid) return;
            Tween.GetScheduler().CycleContext(this);
        }
        protected void TryRecycle()
        {
            if (!autoCycle) return;
            Recycle();
        }
        public bool autoCycle { get; private set; }
        public bool isDone { get; private set; }
        public bool canceled { get; private set; }
        public bool valid { get; set; }
        public bool paused { get; private set; }
        protected float timeScale { get; private set; }



        private Action<ITweenContext> onBegin;

        private Action<ITweenContext> onComplete;
        private Action<ITweenContext> onCancel;
        private Action<ITweenContext, float, float> onTick;



        public void OnBegin(Action<ITweenContext> action) => onBegin += action;
        public void OnComplete(Action<ITweenContext> action) => onComplete += action;
        public void OnCancel(Action<ITweenContext> action) => onCancel += action;
        public void OnTick(Action<ITweenContext, float, float> action) => onTick += action;
        protected virtual void Reset()
        {
            paused = false;
            isDone = false;
            canceled = false;
            onCancel = null;
            onBegin = null;
            onComplete = null;
            onTick = null;
            autoCycle = true;
            timeScale = 1f;
        }
        protected void InvokeCancel()
        {
            if (canceled) return;
            canceled = true;
            onCancel?.Invoke(this);
        }
        protected void SetCancel()
        {
            if (canceled) return;
            canceled = true;
        }
        protected void InvokeComplete()
        {
            if (isDone) return;
            isDone = true;
            onComplete?.Invoke(this);
        }
        protected void InvokeTick(float time, float tick)
        {
            onTick?.Invoke(this, time, tick);
        }


        void IPoolObject.OnGet() => Reset();

        void IPoolObject.OnSet() => Reset();

        public virtual ITweenContext SetTimeScale(float timeScale)
        {
            if (!valid) return this;
            this.timeScale = timeScale;
            return this;
        }

        public abstract void Complete(bool callComplete);

        public virtual void Pause()
        {
            if (!valid || paused) return;
            paused = true;
        }
        public virtual void UnPause()
        {
            if (!valid || !paused) return;
            paused = false;
        }



        public virtual void Run()
        {
            paused = false;
            canceled = false;
            isDone = false;
            onBegin?.Invoke(this);
        }

        public ITweenContext SetAutoCycle(bool cycle)
        {
            this.autoCycle = cycle;
            return this;
        }



        public abstract void Stop();

    }


    class TweenSequence : TweenContextBase, ITweenGroup
    {
        private List<Func<ITweenContext>> list = new List<Func<ITweenContext>>();
        private Queue<Func<ITweenContext>> _queue = new Queue<Func<ITweenContext>>();
        public ITweenGroup NewContext(Func<ITweenContext> func)
        {
            if (func == null) return this;
            list.Add(func);
            return this;
        }
        private ITweenContext inner;
        public override void Stop()
        {
            inner?.Stop();
            inner = null;
            TryRecycle();
        }
        private void Cancel()
        {
            if (canceled) return;
            InvokeCancel();
            inner?.Cancel();
            TryRecycle();

        }
        private void Complete()
        {
            if (isDone) return;
            InvokeComplete();
            TryRecycle();
        }
        protected override void Reset()
        {
            base.Reset();
            inner = null;
            list.Clear();
            _queue.Clear();
        }

        public override void Complete(bool callComplete)
        {
            if (callComplete)
                Complete();
            else
                Cancel();
        }





        private void RunNext(ITweenContext context)
        {
            if (canceled || isDone) return;
            if (_queue.Count > 0)
            {
                inner = _queue.Dequeue().Invoke();
                if (inner != null)
                {
                    inner.OnTick(_OnTick);
                    inner.OnCancel(RunNext);
                    inner.OnComplete(RunNext);
                    inner?.SetTimeScale(this.timeScale);
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

        private void _OnTick(ITweenContext context, float time, float delta)
        {
            InvokeTick(time, delta);
        }

        public override void Run()
        {
            base.Run();
            _queue.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var func = list[i];
                _queue.Enqueue(func);
            }
            RunNext(null);
        }

        public override ITweenContext SetTimeScale(float timeScale)
        {
            if (!valid) return this;
            base.SetTimeScale(timeScale);
            inner?.SetTimeScale(timeScale);
            return this;
        }

        public override void Pause()
        {
            if (!valid || paused) return;
            base.Pause();
            inner?.Pause();
        }

        public override void UnPause()
        {
            if (!valid || !paused) return;
            base.UnPause();
            inner?.UnPause();
        }



    }

    class TweenParallel : TweenContextBase, ITweenGroup
    {
        private List<Func<ITweenContext>> list = new List<Func<ITweenContext>>();
        private List<ITweenContext> contexts = new List<ITweenContext>();

        public ITweenGroup NewContext(Func<ITweenContext> func)
        {
            if (func == null) return this;
            list.Add(func);
            return this;
        }

        public override void Stop()
        {
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.Stop();
            }
            TryRecycle();
        }


        private void Cancel()
        {
            if (canceled) return;
            InvokeCancel();
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.Cancel();
            }
            TryRecycle();

        }
        private void Complete()
        {
            if (isDone) return;
            InvokeComplete();
            TryRecycle();
        }
        protected override void Reset()
        {
            base.Reset();
            this._time = this._delta = -1;
            list.Clear();
            contexts.Clear();
        }

        public override void Complete(bool callComplete)
        {
            if (callComplete)
                Complete();
            else
                Cancel();
        }
        private void OnContextEnd(ITweenContext context)
        {

            if (canceled || isDone) return;
            if (contexts.Count > 0)
                contexts.Remove(context);
            if (contexts.Count == 0)
                Complete();
        }
        private float _time, _delta;
        private void _OnTick(ITweenContext context, float time, float delta)
        {
            if (_time != time || _delta != delta)
            {
                this._time = time;
                this._delta = delta;
                InvokeTick(time, delta);
            }
        }

        public override void Run()
        {
            base.Run();
            contexts.Clear();
            if (list.Count > 0)
                for (int i = 0; i < list.Count; i++)
                {
                    var func = list[i];
                    var context = func.Invoke();
                    context.OnCancel(OnContextEnd);
                    context.OnComplete(OnContextEnd);
                    context.OnTick(_OnTick);
                    context.SetTimeScale(timeScale);
                    contexts.Add(context);
                }
            else
                Complete();
        }

        public override ITweenContext SetTimeScale(float timeScale)
        {
            if (!valid) return this;
            base.SetTimeScale(timeScale);
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.SetTimeScale(timeScale);
            }
            return this;
        }

        public override void Pause()
        {
            if (!valid || paused) return;
            base.Pause();
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.Pause();
            }
        }

        public override void UnPause()
        {
            if (!valid || !paused) return;
            base.UnPause();
            for (int i = 0; i < contexts.Count; i++)
            {
                var context = contexts[i];
                context.UnPause();
            }
        }
    }



















    abstract class TweenContext : TweenContextBase, ITweenContext
    {
        protected float time { get; private set; }
        protected void SetTime(float value) => time = value;
        internal void Update(float deltaTime)
        {
            if (paused || isDone) return;
            if (!MoveNext(deltaTime))
            {
                InvokeComplete();
                TryRecycle();
            }
            else
            {
                InvokeTick(this.time, deltaTime);
            }
        }
        protected abstract bool MoveNext(float delta);
        public override void Run()
        {
            this.SetTime(0);
            base.Run();
            OnRun();
        }
        protected abstract void OnRun();
        public override void Complete(bool callComplete)
        {
            if (isDone) return;
            if (callComplete)
                InvokeComplete();
            else
                InvokeCancel();
            TryRecycle();
        }

        public override void Stop()
        {
            SetCancel();
            TryRecycle();
        }

    }

    class TweenContext<T, Target> : TweenContext, ITweenContext<T, Target>
    {


        private static Dictionary<int, T[]> arrays_Bezier = new Dictionary<int, T[]>();
        public static T[] AllocateArray(int length)
        {
            var min = 16;
            while (min < length)
                min *= 2;
            T[] result = null;
            if (arrays_Bezier.TryGetValue(min, out result))
            {
                return result;
            }
            return new T[min];
        }
        public static void CycleArray(T[] arr)
        {
            arrays_Bezier[arr.Length] = arr;
        }


        private Target target;
        private static ValueCalculator<T> _calc;

        private T start;
        private T end;
        private bool snap;
        private float delay;
        private float duration;
        private float sourceDelta;
        private IValueEvaluator evaluator;
        private int loops;
        private LoopType loopType;
        private Func<Target, T> getter;
        private Action<Target, T> setter;
        private T _start;
        private T _end;

        private bool _wait_delay_flag;
        private int _loop;


        private TweenType _mode;
        private T[] points;
        private T[] _points;
        private int _points_length;

        private int jumpCount;
        private float jumpDamping;

        private static ValueCalculator<T> calc
        {
            get
            {
                if (_calc == null)
                    _calc = Tween.GetValueCalculator<T>();
                return _calc;
            }
        }
        protected override void Reset()
        {
            base.Reset();
            OnRun();
            this.SetEvaluator(EaseEvaluator.Default);
            this.SetSourceDelta(0);
            this.SetDelay(0);
            this.SetLoop(LoopType.Restart, 1);
            _set2Start_called = false;
        }
        protected override void OnRun()
        {
            _wait_delay_flag = true;
            _loop = 0;
            _start = start;
            _end = end;
            _set2Start_called = false;

            if (this._points != null)
            {
                CycleArray(this._points);
                this._points = null;
            }
            if (_mode == TweenType.Bezier || _mode == TweenType.Array)
            {
                _points_length = this.points.Length;
                this._points = AllocateArray(_points_length);
                for (int i = 0; i < _points_length; i++)
                {
                    this._points[i] = this.points[i];
                }
            }

        }

        private bool _set2Start_called = false;
        private int frequency;
        private float dampingRatio;
        private T strength;


        protected override bool MoveNext(float delta)
        {
            var targetTime = this.time + delta * timeScale;
            if (!_set2Start_called && getter != null)
            {

                CalculateView(0);
                _set2Start_called = true;
            }
            if (_wait_delay_flag)
            {
                SetTime(targetTime);
                if (this.time >= delay)
                {
                    targetTime = 0;
                    SetTime(targetTime);
                    _wait_delay_flag = false;
                }
            }
            if (!_wait_delay_flag)
                if (targetTime >= duration)
                {
                    SetTime(duration);
                    CalculateView(duration);
                    OnLoopEnd();
                    _loop++;
                    SetTime(0);
                }
                else
                {
                    SetTime(targetTime);
                    CalculateView(targetTime);
                }
            bool result = loops == -1 || _loop < loops;
            return result;
        }
        private void CalculateView(float _time)
        {
            //var _time = this.time;
            var _dur = this.duration;
            var _percent = Mathf.Clamp01(_time / _dur);
            var _convertPercent = evaluator.Evaluate(_percent, _time, _dur);
            var _deltaPercent = (1 - sourceDelta) + sourceDelta * _percent;

            var src = getter.Invoke(target);
            T _cur = calc.Calculate(_mode, _start, _end, _convertPercent, src, _deltaPercent, snap, strength,
                frequency, dampingRatio, jumpCount, jumpDamping, _points, _points_length);
            if (!src.Equals(_cur))
                setter?.Invoke(target, _cur);
        }


        private void OnLoopEnd()
        {
            if (_mode == TweenType.Bezier || _mode == TweenType.Array)
            {
                if (loopType == LoopType.PingPong)
                {
                    for (int i = 0; i < _points_length; i++)
                    {
                        this._points[i] = this.points[_points_length - 1 - i];

                    }
                }
                else if (loopType == LoopType.Add)
                {
                    var gap = calc.Minus(this._points[_points_length - 1], this._points[0]);

                    for (int i = 0; i < _points_length; i++)
                    {
                        this._points[i] = calc.Add(this._points[i], gap);
                    }
                }
            }
            else
            {
                if (loopType == LoopType.PingPong)
                {
                    var temp = _start;
                    _start = _end;
                    _end = temp;
                }
                else if (loopType == LoopType.Add)
                {
                    var gap = calc.Minus(_end, _start);
                    var tmp = calc.Add(_end, gap);
                    _start = _end;
                    _end = tmp;
                }
            }
        }


        public TweenContext<T, Target> Config(Target target, T start, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, bool snap)
        {
            this.target = target;
            _mode = TweenType.Normal;
            this._start = this.start = start;
            this._end = this.end = end;
            this.duration = duration;
            this.getter = getter;
            this.setter = setter;
            this.snap = snap;
            return this;
        }
        public TweenContext<T, Target> ShakeConfig(Target target, T start, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, bool snap, T strength,
            int frequency, float dampingRatio)
        {
            this.Config(target, start, end, duration, getter, setter, snap);
            _mode = TweenType.Shake;
            this.frequency = frequency;
            this.dampingRatio = dampingRatio;
            this.strength = strength;

            return this;
        }
        public TweenContext<T, Target> PunchConfig(Target target, T start, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, bool snap, T strength,
       int frequency, float dampingRatio)
        {
            this.Config(target, start, end, duration, getter, setter, snap);
            _mode = TweenType.Punch;
            this.frequency = frequency;
            this.dampingRatio = dampingRatio;
            this.strength = strength;

            return this;
        }
        public TweenContext<T, Target> JumpConfig(Target target, T start, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, bool snap, T strength,
  int jumpCount, float jumpDamping)
        {
            this.Config(target, start, end, duration, getter, setter, snap);
            _mode = TweenType.Jump;
            this.jumpCount = jumpCount;
            this.jumpDamping = jumpDamping;
            this.strength = strength;

            return this;
        }


        public TweenContext<T, Target> BezierConfig(Target target, float duration, Func<Target, T> getter, Action<Target, T> setter, bool snap, T[] points)
        {
            if (points == null || points.Length <= 2)
            {
                Log.FE("At Least 3 point");
            }
            this.Config(target, default, default, duration, getter, setter, snap);
            _mode = TweenType.Bezier;
            this.points = points;
            return this;
        }

        public TweenContext<T, Target> ArrayConfig(Target target, float duration, Func<Target, T> getter, Action<Target, T> setter, bool snap, T[] points)
        {
            if (points == null || points.Length <= 2)
            {
                Log.FE("At Least 3 point");
            }
            this.Config(target, default, default, duration, getter, setter, snap);
            _mode = TweenType.Array;
            this.points = points;
            return this;
        }

        public void SetSourceDelta(float delta) => sourceDelta = delta;
        public void SetEvaluator(IValueEvaluator evaluator) => this.evaluator = evaluator;
        public void SetSnap(bool value) => snap = value;



        public void SetDelay(float value)
        {
            if (value <= 0.00002f)
                value = 0.0002f;
            delay = value;
        }
        public void SetLoop(LoopType type, int loops)
        {
            this.loops = loops;
            loopType = type;
        }

        public void SetDuration(float duration) => this.duration = duration;
        public void SetFrequency(int duration) => this.frequency = duration;
        public void SetStrength(T duration) => this.strength = duration;
        public void SetDampingRatio(float duration) => this.dampingRatio = duration;
        public void SetJumpDamping(float jumpDamping) => this.jumpDamping = jumpDamping;
        public void SetJumpCount(int jumpCount) => this.jumpCount = jumpCount;

    }



    class TweenScheduler
    {
        private Dictionary<Type, ISimpleObjectPool> contextPools;

        public TweenScheduler()
        {
            contextPools = new Dictionary<Type, ISimpleObjectPool>();
        }

        public void Update()
        {
            float deltaTime = Launcher.GetDeltaTime();
            for (int i = 0; i < contexts_run.Count; i++)
            {
                var context = contexts_run[i];
                (context as TweenContext).Update(deltaTime);
            }
            for (int i = 0; i < contexts_wait_to_run.Count; i++)
            {
                var context = contexts_wait_to_run[i];
                context.Run();
            }
            contexts_wait_to_run.Clear();
        }

        private List<ITweenContext> contexts_run = new List<ITweenContext>();
        private List<ITweenContext> contexts_wait_to_run = new List<ITweenContext>();


        public ITweenContext<T, Target> AllocateContext<T, Target>(bool auto_run)
        {
            Type type = typeof(TweenContext<T, Target>);
            ISimpleObjectPool pool = null;
            if (!contextPools.TryGetValue(type, out pool))
            {
                pool = new SimpleObjectPool<TweenContext<T, Target>>();
                contextPools.Add(type, pool);
            }
            var simple = pool as SimpleObjectPool<TweenContext<T, Target>>;
            var context = simple.Get();
            if (auto_run)
                contexts_wait_to_run.Add(context);
            return context;
        }
        public ITweenGroup AllocateSequence()
        {
            Type type = typeof(TweenSequence);
            ISimpleObjectPool pool = null;
            if (!contextPools.TryGetValue(type, out pool))
            {
                pool = new SimpleObjectPool<TweenSequence>();
                contextPools.Add(type, pool);
            }
            var simple = pool as SimpleObjectPool<TweenSequence>;
            var context = simple.Get();
            //contexts_run.Add(context);
            return context;
        }
        public ITweenGroup AllocateParallel()
        {
            Type type = typeof(TweenParallel);
            ISimpleObjectPool pool = null;
            if (!contextPools.TryGetValue(type, out pool))
            {
                pool = new SimpleObjectPool<TweenParallel>();
                contextPools.Add(type, pool);
            }
            var simple = pool as SimpleObjectPool<TweenParallel>;
            var context = simple.Get();
            //contexts_run.Add(context);
            return context;
        }

        public void CycleContext(ITweenContext context)
        {
            var type = context.GetType();


            ISimpleObjectPool pool = null;
            if (!contextPools.TryGetValue(type, out pool)) return;
            contexts_run.Remove(context);
            contexts_wait_to_run.Remove(context);

            pool.SetObject(context);
        }

        public void CancelAllTween()
        {
            for (int i = contexts_run.Count - 1; i >= 0; i--)
            {
                var context = contexts_run[i];
                context.Cancel();
            }
        }

        internal void AddToRun(ITweenContext context)
        {
            if (context == null || contexts_run.Contains(context)) return;
            contexts_run.Add(context);
        }
    }
    [MonoSingletonPath(nameof(IFramework.Tween))]
    class TweenScheduler_Runtime : MonoSingleton<TweenScheduler_Runtime>
    {
        public TweenScheduler scheduler;

        protected override void OnSingletonInit()
        {
            base.OnSingletonInit();
            scheduler = new TweenScheduler();
        }
        private void Update()
        {
            scheduler.Update();
        }
        protected override void OnDestroy()
        {
            scheduler.CancelAllTween();
            base.OnDestroy();
        }


    }




    public static class Tween
    {
#if UNITY_EDITOR
        private static TweenScheduler editorScheduler;
        private static void OnModeChange(UnityEditor.PlayModeStateChange mode)
        {
            if (mode == UnityEditor.PlayModeStateChange.EnteredPlayMode)
                editorScheduler?.CancelAllTween();

        }

        [UnityEditor.InitializeOnLoadMethod]
        static void CreateEditorScheduler()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnModeChange;
            UnityEditor.EditorApplication.playModeStateChanged += OnModeChange;
            editorScheduler = new TweenScheduler();
            UnityEditor.EditorApplication.update += editorScheduler.Update;
        }


#endif
        internal static TweenScheduler GetScheduler()
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
                return editorScheduler;
#endif
            return TweenScheduler_Runtime.Instance.scheduler;
        }
        public static void CancelAllTween() => GetScheduler().CancelAllTween();

        static Dictionary<Type, object> value_calcs = new Dictionary<Type, object>()
        {
            { typeof(float), new ValueCalculator_Float() },
            { typeof(Vector2), new ValueCalculator_Vector2() },
            { typeof(Vector3), new ValueCalculator_Vector3() },
            { typeof(Vector4), new ValueCalculator_Vector4() },
            { typeof(Color), new ValueCalculator_Color() },
            { typeof(Rect), new ValueCalculator_Rect() },
        };
        public static List<Type> GetSupportTypes() => value_calcs.Keys.ToList();
        internal static ValueCalculator<T> GetValueCalculator<T>()
        {
            var type = typeof(T);
            object obj = null;
            if (!value_calcs.TryGetValue(type, out obj))
            {
                Log.FE($"Tween Not support Type: {type}");
            }
            return obj as ValueCalculator<T>;
        }



        //public static ITweenContext<T> As<T>(this ITweenContext t) => t as ITweenContext<T>;
        private static TweenContext<T, Target> AsInstance<T, Target>(this ITweenContext<T, Target> context) => context as TweenContext<T, Target>;
        public static IAwaiter<T> GetAwaiter<T>(this T context) where T : ITweenContext => new ITweenContextAwaitor<T>(context);
        public static ITweenContext<T, Target> DoGoto<T, Target>(Target target, T start, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, bool snap, bool autoRun = true)
        {
            var context = GetScheduler().AllocateContext<T, Target>(autoRun);
            context.AsInstance().Config(target, start, end, duration, getter, setter, snap);
            return context;
        }
        public static ITweenContext<T, Target> DoGoto<T, Target>(Target target, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, bool snap, bool autoRun = true)
            => DoGoto(target, getter.Invoke(target), end, duration, getter, setter, snap, autoRun);

        public static ITweenContext<T, Target> DoShake<T, Target>(Target target, T start, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, T strength,
            int frequency = 10, float dampingRatio = 1, bool snap = false, bool autoRun = true)
        {
            var context = GetScheduler().AllocateContext<T, Target>(autoRun);
            context.AsInstance().ShakeConfig(target, start, end, duration, getter, setter, snap, strength, frequency, dampingRatio);
            return context;
        }

        public static ITweenContext<T, Target> DoShake<T, Target>(Target target, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, T strength,
          int frequency = 10, float dampingRatio = 1, bool snap = false, bool autoRun = true)
        {
            return DoShake<T, Target>(target, getter.Invoke(target), end, duration, getter, setter, strength, frequency, dampingRatio, snap, autoRun);
        }
        public static ITweenContext<T, Target> DoPunch<T, Target>(Target target, T start, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, T strength,
      int frequency = 10, float dampingRatio = 1, bool snap = false, bool autoRun = true)
        {
            var context = GetScheduler().AllocateContext<T, Target>(autoRun);
            context.AsInstance().PunchConfig(target, start, end, duration, getter, setter, snap, strength, frequency, dampingRatio);
            return context;
        }
        public static ITweenContext<T, Target> DoPunch<T, Target>(Target target, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, T strength,
       int frequency = 10, float dampingRatio = 1, bool snap = false, bool autoRun = true)
        {
            return DoPunch<T, Target>(target, getter.Invoke(target), end, duration, getter, setter, strength, frequency, dampingRatio, snap, autoRun);
        }
        public static ITweenContext<T, Target> DoJump<T, Target>(Target target, T start, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, T strength,
   int jumpCount = 5, float jumpDamping = 2f, bool snap = false, bool autoRun = true)
        {
            var context = GetScheduler().AllocateContext<T, Target>(autoRun);
            context.AsInstance().JumpConfig(target, start, end, duration, getter, setter, snap, strength, jumpCount, jumpDamping);
            return context;
        }
        public static ITweenContext<T, Target> DoJump<T, Target>(Target target, T end, float duration, Func<Target, T> getter, Action<Target, T> setter, T strength,
int jumpCount = 5, float jumpDamping = 2f, bool snap = false, bool autoRun = true)
        {
            return DoJump<T, Target>(target, getter.Invoke(target), end, duration, getter, setter, strength, jumpCount, jumpDamping, snap, autoRun);

        }


        public static ITweenContext<T, Target> DoArray<T, Target>(Target target, float duration, Func<Target, T> getter, Action<Target, T> setter, T[] points, bool snap = false, bool autoRun = true)
        {
            var context = GetScheduler().AllocateContext<T, Target>(autoRun);
            context.AsInstance().ArrayConfig(target, duration, getter, setter, snap, points);
            return context;
        }
        public static ITweenContext<T, Target> DoBezier<T, Target>(Target target, float duration, Func<Target, T> getter, Action<Target, T> setter, T[] points, bool snap = false, bool autoRun = true)
        {
            var context = GetScheduler().AllocateContext<T, Target>(autoRun);
            context.AsInstance().BezierConfig(target, duration, getter, setter, snap, points);
            return context;
        }

        public static ITweenGroup Sequence() => GetScheduler().AllocateSequence();
        public static ITweenGroup Parallel() => GetScheduler().AllocateParallel();



        public static T AddTo<T>(this T context, ITweenContextBox box) where T : ITweenContext
        {
            box.AddTween(context);
            return context;
        }




        public static T ReStart<T>(this T context) where T : ITweenContext
        {
            if (!(context as IPoolObject).valid)
            {
                Log.FE($"The {context} have be in pool,{nameof(context.autoCycle)}:{context.autoCycle}");
            }
            else
            {

                context.Stop();
                context.Run();
            }
            return context;
        }


        private static TweenContextBase AsContextBase(this ITweenContext t) => t as TweenContextBase;
        public static void Cancel<T>(this T context) where T : ITweenContext => context.Complete(false);
        public static T Pause<T>(this T t) where T : ITweenContext
        {
            t.AsContextBase().Pause();
            return t;
        }
        public static T UnPause<T>(this T t) where T : ITweenContext
        {
            t.AsContextBase().UnPause();
            return t;
        }
        public static T Complete<T>(this T t, bool callComplete) where T : ITweenContext
        {
            t.AsContextBase().Complete(callComplete);
            return t;
        }
        public static T Recycle<T>(this T t) where T : ITweenContext
        {
            t.AsContextBase().Recycle();
            return t;
        }
        public static T Stop<T>(this T t) where T : ITweenContext
        {
            t.AsContextBase().Stop();
            return t;
        }
        public static T SetTimeScale<T>(this T t, float value) where T : ITweenContext
        {
            t.AsContextBase().SetTimeScale(value);
            return t;
        }
        public static T SetAutoCycle<T>(this T t, bool value) where T : ITweenContext
        {
            t.AsContextBase().SetAutoCycle(value);
            return t;
        }
        public static T Run<T>(this T t) where T : ITweenContext
        {
            t.AsContextBase().Run();
            if (!(t is ITweenGroup))
                Tween.GetScheduler().AddToRun(t);
            return t;
        }
        public static T OnComplete<T>(this T t, Action<ITweenContext> action) where T : ITweenContext
        {
            t.AsContextBase().OnComplete(action);
            return t;
        }
        public static T OnBegin<T>(this T t, Action<ITweenContext> action) where T : ITweenContext
        {
            t.AsContextBase().OnBegin(action);
            return t;
        }
        public static T OnCancel<T>(this T t, Action<ITweenContext> action) where T : ITweenContext
        {
            t.AsContextBase().OnCancel(action);
            return t;
        }
        public static T OnTick<T>(this T t, Action<ITweenContext, float, float> action) where T : ITweenContext
        {
            t.AsContextBase().OnTick(action);
            return t;
        }





        public static ITweenContext<T, Target> SetLoop<T, Target>(this ITweenContext<T, Target> context, LoopType type, int loops)
        {
            context.AsInstance().SetLoop(type, loops);
            return context;
        }

        public static ITweenContext<T, Target> SetDelay<T, Target>(this ITweenContext<T, Target> t, float value)
        {
            t.AsInstance().SetDelay(value);
            return t;
        }
        public static ITweenContext<T, Target> SetSourceDelta<T, Target>(this ITweenContext<T, Target> t, float delta)
        {
            t.AsInstance().SetSourceDelta(delta);
            return t;
        }
        public static ITweenContext<T, Target> SetEvaluator<T, Target>(this ITweenContext<T, Target> t, IValueEvaluator evaluator)
        {
            t.AsInstance().SetEvaluator(evaluator);
            return t;
        }
        public static ITweenContext<T, Target> SetEase<T, Target>(this ITweenContext<T, Target> t, Ease ease) => t.SetEvaluator(new EaseEvaluator(ease));
        public static ITweenContext<T, Target> SetAnimationCurve<T, Target>(this ITweenContext<T, Target> t, AnimationCurve curve) => t.SetEvaluator(new AnimationCurveEvaluator(curve));
        public static ITweenContext<T, Target> SetSnap<T, Target>(this ITweenContext<T, Target> t, bool value)
        {
            t.AsInstance().SetSnap(value);
            return t;
        }
        public static ITweenContext<T, Target> SetDuration<T, Target>(this ITweenContext<T, Target> t, float value)
        {
            t.AsInstance().SetDuration(value);
            return t;
        }
        public static ITweenContext<T, Target> SetFrequency<T, Target>(this ITweenContext<T, Target> t, int value)
        {
            t.AsInstance().SetFrequency(value);
            return t;
        }
        public static ITweenContext<T, Target> SetDampingRatio<T, Target>(this ITweenContext<T, Target> t, float value)
        {
            t.AsInstance().SetDampingRatio(value);
            return t;
        }
        public static ITweenContext<T, Target> SetJumpCount<T, Target>(this ITweenContext<T, Target> t, int value)
        {
            t.AsInstance().SetJumpCount(value);
            return t;
        }
        public static ITweenContext<T, Target> SetJumpDamping<T, Target>(this ITweenContext<T, Target> t, float value)
        {
            t.AsInstance().SetJumpDamping(value);
            return t;
        }
        public static ITweenContext<T, Target> SetStrength<T, Target>(this ITweenContext<T, Target> t, T value)
        {
            t.AsInstance().SetStrength(value);
            return t;
        }
        //public static ITweenContext<T> SetEnd<T>(this ITweenContext<T> t, T value)
        //{
        //    t.AsInstance().SetEnd(value);
        //    return t;
        //}
        //public static ITweenContext<T> SetStart<T>(this ITweenContext<T> t, T value)
        //{
        //    t.AsInstance().SetStart(value);
        //    return t;
        //}


    }

    struct AnimationCurveEvaluator : IValueEvaluator
    {
        private AnimationCurve _curve;

        public AnimationCurveEvaluator(AnimationCurve curve) => _curve = curve;
        float IValueEvaluator.Evaluate(float percent, float time, float duration) => _curve.Evaluate(percent);
    }
    struct EaseEvaluator : IValueEvaluator
    {
        public static EaseEvaluator Default = new EaseEvaluator(Ease.Linear);
        private Ease _ease;
        public EaseEvaluator(Ease ease) => _ease = ease;
        private static float Evaluate(Ease easeType, float time, float duration)
        {
            float percent = Mathf.Clamp01((time / duration));
            switch (easeType)
            {
                case Ease.Linear:
                    return percent;
                case Ease.InSine:
                    return -(float)Math.Cos((double)(percent * 1.5707964f)) + 1f;
                case Ease.OutSine:
                    return (float)Math.Sin((double)(percent * 1.5707964f));
                case Ease.InOutSine:
                    return -0.5f * ((float)Math.Cos((double)(3.1415927f * percent)) - 1f);
                case Ease.InQuad:
                    return percent * percent;
                case Ease.OutQuad:
                    return -(time /= duration) * (time - 2f);
                case Ease.InOutQuad:
                    if ((time /= duration * 0.5f) < 1f)
                    {
                        return 0.5f * time * time;
                    }
                    return -0.5f * ((time -= 1f) * (time - 2f) - 1f);
                case Ease.InCubic:
                    return (time /= duration) * time * time;
                case Ease.OutCubic:
                    return (time = percent - 1f) * time * time + 1f;
                case Ease.InOutCubic:
                    if ((time /= duration * 0.5f) < 1f)
                    {
                        return 0.5f * time * time * time;
                    }
                    return 0.5f * ((time -= 2f) * time * time + 2f);
                case Ease.InQuart:
                    return (time /= duration) * time * time * time;
                case Ease.OutQuart:
                    return -((time = percent - 1f) * time * time * time - 1f);
                case Ease.InOutQuart:
                    if ((time /= duration * 0.5f) < 1f)
                    {
                        return 0.5f * time * time * time * time;
                    }
                    return -0.5f * ((time -= 2f) * time * time * time - 2f);
                case Ease.InQuint:
                    return (time /= duration) * time * time * time * time;
                case Ease.OutQuint:
                    return (time = percent - 1f) * time * time * time * time + 1f;
                case Ease.InOutQuint:
                    if ((time /= duration * 0.5f) < 1f)
                    {
                        return 0.5f * time * time * time * time * time;
                    }
                    return 0.5f * ((time -= 2f) * time * time * time * time + 2f);
                case Ease.InExpo:
                    if (time != 0f)
                    {
                        return (float)Math.Pow(2.0, (double)(10f * (percent - 1f)));
                    }
                    return 0f;
                case Ease.OutExpo:
                    if (time == duration)
                    {
                        return 1f;
                    }
                    return -(float)Math.Pow(2.0, (double)(-10f * percent)) + 1f;
                case Ease.InOutExpo:
                    if (time == 0f)
                    {
                        return 0f;
                    }
                    if (time == duration)
                    {
                        return 1f;
                    }
                    if ((time /= duration * 0.5f) < 1f)
                    {
                        return 0.5f * (float)Math.Pow(2.0, (double)(10f * (time - 1f)));
                    }
                    return 0.5f * (-(float)Math.Pow(2.0, (double)(-10f * (time -= 1f))) + 2f);
                case Ease.InCirc:
                    return -((float)Math.Sqrt((double)(1f - (time /= duration) * time)) - 1f);
                case Ease.OutCirc:
                    return (float)Math.Sqrt((double)(1f - (time = percent - 1f) * time));
                case Ease.InOutCirc:
                    if ((time /= duration * 0.5f) < 1f)
                    {
                        return -0.5f * ((float)Math.Sqrt((double)(1f - time * time)) - 1f);
                    }
                    return 0.5f * ((float)Math.Sqrt((double)(1f - (time -= 2f) * time)) + 1f);

                default:
                    return -(time /= duration) * (time - 2f);
            }
        }
        float IValueEvaluator.Evaluate(float percent, float time, float duration) => Evaluate(_ease, time, duration);
    }
    struct ITweenContextAwaitor<T> : IAwaiter<T> where T : ITweenContext
    {
        private T op;
        private Queue<Action> actions;

        public T GetResult() => op;
        public ITweenContextAwaitor(T op)
        {
            this.op = op;
            actions = new Queue<Action>();
            op.OnComplete(OnCompleted);
        }

        private void OnCompleted(ITweenContext context)
        {
            while (actions.Count > 0)
            {
                actions.Dequeue()?.Invoke();
            }
        }

        public bool IsCompleted => op.isDone;

        public void OnCompleted(Action continuation)
        {
            actions?.Enqueue(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            OnCompleted(continuation);
        }


    }


    abstract class ValueCalculator<T>
    {

        public T Calculate(TweenType mode, T start, T end, float percent, T srcValue,
            float srcPercent, bool snap, T strength, int frequency, float dampingRatio, int jumpCount, float jumpDamping,
            T[] points, int pointsLength)
        {
            T dest = default(T);
            if (mode == TweenType.Bezier || mode == TweenType.Array)
            {
                if (percent == 1)
                    dest = points[pointsLength - 1];
                else
                {
                    if (mode == TweenType.Bezier)
                    {
                        float[] tempCoefficients = EvaluateBezier(percent, pointsLength);
                        for (int i = 0; i < pointsLength; i++)
                            dest = Add(dest, Multi(points[i], tempCoefficients[i]));
                        CycleArray(tempCoefficients);
                    }
                    else
                    {
                        T _start, _end; float _percent;
                        EvaluateArray(percent, points, pointsLength, out _start, out _end, out _percent);
                        dest = Lerp(_start, _end, _percent);
                    }
                }
            }
            else
                dest = Lerp(start, end, percent);


            dest = Lerp(srcValue, dest, srcPercent);


            if (mode == TweenType.Shake || mode == TweenType.Punch)
            {
                strength = MultiStrength(strength, end);
                var s = Multi(strength, EvaluateStrength(frequency, dampingRatio, percent));
                if (mode == TweenType.Punch)
                    dest = Add(dest, s);
                else
                {
                    s = Multi(s, percent);
                    dest = Add(dest, RangeValue(s));
                }
            }
            else if (mode == TweenType.Jump)
            {
                int devCount;
                float jumpAdd;
                EvaluateJump(jumpCount, percent, out devCount, out jumpAdd);
                for (int i = 0; i < devCount; i++)
                    strength = Dev(strength, jumpDamping);
                dest = Add(dest, Multi(strength, jumpAdd));
            }
            if (snap)
                dest = Snap(dest);
            return dest;


        }

        public abstract T Snap(T value);
        public abstract T Lerp(T start, T end, float percent);
        public abstract T Minus(T value, T value2);
        public abstract T Add(T value, T value2);
        public abstract T Dev(T value, float dev);
        public abstract T Multi(T value, float dev);
        public abstract T MultiStrength(T strength, T value);
        public abstract T RangeValue(T value);









        protected static float Range() => UnityEngine.Random.Range(-1, 1);

        const float E = 2.71828175F;
        const float PI = 3.14159274F;

        private static Dictionary<int, float[]> arrays = new Dictionary<int, float[]>();
        private static float[] AllocateArray(int length)
        {
            var min = 16;
            while (min < length)
                min *= 2;
            float[] result = null;
            if (arrays.TryGetValue(min, out result))
            {
                return result;
            }
            return new float[min];
        }
        private static void CycleArray(float[] arr)
        {
            arrays[arr.Length] = arr;
        }
        private static float[] EvaluateBezier(float percent, int length)
        {
            float u = 1f - percent;

            // 使用Bernstein多项式递推关系优化计算
            float[] tempCoefficients = AllocateArray(length);
            tempCoefficients[0] = Mathf.Pow(u, length - 1);

            for (int i = 1; i < length; i++)
                tempCoefficients[i] = tempCoefficients[i - 1] * (percent / u) * (length - i) / i;

            return tempCoefficients;
        }
        private static void EvaluateArray(float percent, T[] array, int length, out T start, out T end, out float _percent)
        {
            var temp = percent * (length - 1);
            var floor = Mathf.FloorToInt(temp);
            _percent = temp - floor;
            start = array[floor];

            end = array[floor + 1];
        }
        private static void EvaluateJump(int jumpCount, float percent, out int devCount, out float jumpAdd)
        {
            if (percent == 0 || percent == 1)
            {
                jumpAdd = 0;
                devCount = 0;
                return;
            }

            var gap = 1f / jumpCount;
            var _percent = (percent % gap) * jumpCount;
            devCount = Mathf.FloorToInt(percent / gap);
            jumpAdd = (1 - (4 * Mathf.Pow(_percent - 0.5f, 2)));
        }
        private static float EvaluateStrength(int frequency, float dampingRatio, float t)
        {
            if (t == 1f || t == 0f)
            {
                return 0;
            }
            float angularFrequency = (frequency - 0.5f) * PI;
            float dampingFactor = dampingRatio * frequency / (2f * PI);
            return Mathf.Cos(angularFrequency * t) * Mathf.Pow(E, -dampingFactor * t);
        }
    }
    class ValueCalculator_Float : ValueCalculator<float>
    {

        public override float Minus(float value, float value2) => value - value2;

        public override float Add(float value, float value2) => value + value2;

        public override float Dev(float value, float dev) => value / dev;

        public override float Multi(float value, float dev) => value * dev;

        public override float Snap(float value) => Mathf.RoundToInt(value);

        public override float Lerp(float start, float end, float percent) => Mathf.Lerp(start, end, percent);

        public override float MultiStrength(float strength, float value) => strength * value;

        public override float RangeValue(float value) => value * Range();
    }

    class ValueCalculator_Color : ValueCalculator<Color>
    {

        public override Color Minus(Color value, Color value2) => value - value2;

        public override Color Add(Color value, Color value2) => value + value2;

        public override Color Dev(Color value, float dev) => value / dev;

        public override Color Multi(Color value, float dev) => value * dev;

        public override Color Snap(Color value)
        {
            value.a = Mathf.RoundToInt(value.a);
            value.r = Mathf.RoundToInt(value.r);
            value.g = Mathf.RoundToInt(value.g);
            value.b = Mathf.RoundToInt(value.b);
            return value;
        }

        public override Color Lerp(Color start, Color end, float percent) => Color.Lerp(start, end, percent);

        public override Color MultiStrength(Color strength, Color value) => new Color(strength.r * value.r, strength.g * value.g, strength.b * value.b, strength.a * value.a);

        public override Color RangeValue(Color value)
        {
            value.r *= Range();
            value.g *= Range();
            value.b *= Range();
            value.a *= Range();
            return value;
        }
    }

    class ValueCalculator_Rect : ValueCalculator<Rect>
    {
        public override Rect Minus(Rect value, Rect value2)
        {
            return new Rect(
       value.x - value2.x,
       value.y - value2.y,
       value.width - value2.width,
       value.height - value2.height);
        }

        public override Rect Add(Rect value, Rect value2)
        {
            return new Rect(
                value.x + value2.x,
                value.y + value2.y,
                value.width + value2.width,
                value.height + value2.height);
        }


        public override Rect Dev(Rect value, float dev) => new Rect(value.x / dev, value.y / dev, value.width / dev, value.height / dev);

        public override Rect Multi(Rect value, float dev) => new Rect(value.x * dev, value.y * dev, value.width * dev, value.height * dev);

        public override Rect Snap(Rect value)
        {
            value.x = Mathf.RoundToInt(value.x);
            value.y = Mathf.RoundToInt(value.y);
            value.width = Mathf.RoundToInt(value.width);
            value.height = Mathf.RoundToInt(value.height);
            return value;
        }

        public override Rect Lerp(Rect a, Rect b, float t)
        {
            Rect r = Rect.zero;
            r.x = Mathf.Lerp(a.x, b.x, t);
            r.y = Mathf.Lerp(a.y, b.y, t);
            r.width = Mathf.Lerp(a.width, b.width, t);
            r.height = Mathf.Lerp(a.height, b.height, t);
            return r;
        }

        public override Rect MultiStrength(Rect strength, Rect end)
        {
            return new Rect(strength.x * end.x, strength.y * end.y, strength.width * end.width, strength.height * end.height);

        }

        public override Rect RangeValue(Rect value)
        {
            value.x *= Range();
            value.y *= Range();
            value.width *= Range();
            value.height *= Range();
            return value;
        }
    }
    class ValueCalculator_Vector2 : ValueCalculator<Vector2>
    {
        public override Vector2 Minus(Vector2 value, Vector2 value2) => value - value2;

        public override Vector2 Add(Vector2 value, Vector2 value2) => value + value2;
        public override Vector2 Dev(Vector2 value, float dev) => value / dev;

        public override Vector2 Multi(Vector2 value, float dev) => value * dev;

        public override Vector2 Snap(Vector2 value)
        {
            value.x = Mathf.RoundToInt(value.x);
            value.y = Mathf.RoundToInt(value.y);
            return value;
        }

        public override Vector2 Lerp(Vector2 start, Vector2 end, float percent) => Vector2.Lerp(start, end, percent);

        public override Vector2 MultiStrength(Vector2 strength, Vector2 value) => new Vector2(strength.x * value.x, strength.y * value.y);

        public override Vector2 RangeValue(Vector2 value)
        {
            value.x *= Range();
            value.y *= Range();
            return value;
        }
    }
    class ValueCalculator_Vector3 : ValueCalculator<Vector3>
    {




        public override Vector3 Minus(Vector3 value, Vector3 value2) => value - value2;

        public override Vector3 Add(Vector3 value, Vector3 value2) => value + value2;
        public override Vector3 Dev(Vector3 value, float dev) => value / dev;

        public override Vector3 Multi(Vector3 value, float dev) => value * dev;



        public override Vector3 Snap(Vector3 value)
        {
            value.x = Mathf.RoundToInt(value.x);
            value.y = Mathf.RoundToInt(value.y);
            value.z = Mathf.RoundToInt(value.z);
            return value;
        }

        public override Vector3 Lerp(Vector3 start, Vector3 end, float percent) => Vector3.Lerp(start, end, percent);

        public override Vector3 MultiStrength(Vector3 strength, Vector3 value) => new Vector3(strength.x * value.x, strength.y * value.y, strength.z * value.z);

        public override Vector3 RangeValue(Vector3 value)
        {
            value.x *= Range();
            value.y *= Range();
            value.z *= Range();
            return value;
        }
    }
    class ValueCalculator_Vector4 : ValueCalculator<Vector4>
    {
        public override Vector4 Dev(Vector4 value, float dev) => value / dev;

        public override Vector4 Multi(Vector4 value, float dev) => value * dev;

        public override Vector4 Minus(Vector4 value, Vector4 value2) => value - value2;

        public override Vector4 Add(Vector4 value, Vector4 value2) => value + value2;

        public override Vector4 Snap(Vector4 value)
        {
            value.x = Mathf.RoundToInt(value.x);
            value.y = Mathf.RoundToInt(value.y);
            value.z = Mathf.RoundToInt(value.z);
            value.w = Mathf.RoundToInt(value.w);
            return value;
        }

        public override Vector4 Lerp(Vector4 start, Vector4 end, float percent) => Vector4.Lerp(start, end, percent);

        public override Vector4 MultiStrength(Vector4 strength, Vector4 value) => new Vector4(strength.x * value.x, strength.y * value.y, strength.z * value.z, strength.w * value.w);

        public override Vector4 RangeValue(Vector4 value)
        {
            value.x *= Range();
            value.y *= Range();
            value.z *= Range();
            value.w *= Range();
            return value;
        }
    }




}