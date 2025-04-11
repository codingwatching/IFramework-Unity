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
using UnityEngine;


namespace IFramework
{
    public enum LoopType
    {
        ReStart,
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
        bool valid { get; }
        bool isDone { get; }
        void Pause();
        void UnPause();
        void Complete(bool callComplete);
    }
    public interface ITweenContext<T> : ITweenContext
    {
        T start { get; }
        T end { get; }
        bool snap { get; }
    }

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



    abstract class TweenContext : ITweenContext, IPoolObject
    {
        bool ITweenContext.isDone => _context == null ? false : _context.isDone || _context.canceled;

        protected float percent => Mathf.Clamp01(time / duration);
        protected float convertPercent => evaluator.Evaluate(percent, time, duration);
        protected float deltaPercent => (1 - sourceDelta) + sourceDelta * percent;

        public bool valid { get; private set; }



        private float time { get; set; }
        private IValueEvaluator evaluator { get; set; }
        private int loops { get; set; }
        private float timeScale { get; set; }
        protected LoopType loopType { get; private set; }
        protected float duration { get; private set; }
        private event Action<ITweenContext> onComplete, onCancel;
        private event Action<ITweenContext, float, float> onTick;

        private float delay { get; set; }
        private bool _wait_delay_flag;
        private float sourceDelta { get; set; }
        private int _loop;
        private ITimerContext _context;
        private void Reset()
        {
            _wait_delay_flag = true;
            _context = null;
            onComplete = null;
            onCancel = null;
            onTick = null;
            time = 0;
            _loop = 0;
            this.SetEvaluator(EaseEvaluator.Default);
            this.SetLoop(LoopType.ReStart, 1);
            this.SetSourceDelta(0);
            this.SetTimeScale(1);
            this.SetDelay(0);
        }
        void IPoolObject.OnGet()
        {
            Reset();
            valid = true;
        }

        void IPoolObject.OnSet()
        {
            valid = false;
            Reset();
        }

        protected void SetDuration(float duration) => this.duration = duration;

        public void SetLoop(LoopType type, int loops)
        {
            this.loops = loops;
            loopType = type;
        }
        public void SetSourceDelta(float delta) => sourceDelta = delta;
        public void SetEvaluator(IValueEvaluator evaluator) => this.evaluator = evaluator;
        public void OnComplete(Action<ITweenContext> action) => onComplete += action;
        public void OnCancel(Action<ITweenContext> action) => onCancel += action;
        public void OnTick(Action<ITweenContext, float, float> action) => onTick += action;

        public void SetTimeScale(float value) => timeScale = value;
        public void SetDelay(float value)
        {
            if (value <= 0.00002f)
                value = 0.0002f;
            delay = value;
        }

        void ITweenContext.Pause() => _context?.Pause();
        void ITweenContext.UnPause() => _context?.UnPause();

        void ITweenContext.Complete(bool callComplete)
        {
            if (((ITweenContext)this).isDone) return;
            if (callComplete)
                _context.Complete();
            else
            {
                _context.Cancel();
                onCancel?.Invoke(this);
            }
            CycleThis();
        }
        private void CycleThis() => Tween.GetScheduler().Cycle(this);


        protected abstract void OnLoopEnd();
        private bool WhileCheck(float time, float delta)
        {
            bool result = loops == -1 || _loop < loops;
            var targetTime = this.time + delta * timeScale;

            if (_wait_delay_flag)
            {
                this.time = targetTime;
                if (this.time >= delay)
                {
                    targetTime = this.time = 0;
                    _wait_delay_flag = false;
                }
            }
            if (!_wait_delay_flag)
                if (targetTime >= duration)
                {
                    this.time = duration;

                    CalculateView();
                    OnLoopEnd();
                    _loop++;
                    this.time = 0;
                }
                else
                {
                    this.time = targetTime;
                    CalculateView();
                }

            onTick?.Invoke(this, this.time, delta);
            return result;
        }

        protected abstract void CalculateView();

        public async void Run()
        {
            TweenScheduler scheduler = Tween.GetScheduler();
            //await scheduler.Frame();
            _context = scheduler.DoWhile(WhileCheck);
            await _context;
            onComplete?.Invoke(this);
            CycleThis();
        }







    }
    class TweenContext<T> : TweenContext, ITweenContext<T>
    {

        private static ValueCalculator<T> _calc;
        public T start { get; private set; }
        public T end { get; private set; }
        public bool snap { get; private set; }


        public void SetSnap(bool value) { snap = value; }
        public void SetStart(T value)
        {
            if (_start.Equals(start))
                _start = value;
            if (_end.Equals(start))
                _end = value;


            start = value;
        }
        public void SetEnd(T value)
        {
            if (_start.Equals(end))
                _start = value;
            if (_end.Equals(end))
                _end = value;


            end = value;
        }

        private T _start;
        private T _end;

        private Func<T> getter;
        private Action<T> setter;

        private static ValueCalculator<T> calc
        {
            get
            {
                if (_calc == null)
                    _calc = Tween.GetValueCalculator<T>();
                return _calc;
            }
        }
        protected sealed override void CalculateView()
        {

            var src = getter.Invoke();
            T _cur = calc.Calculate(_start, _end, convertPercent, src, deltaPercent, snap);
            if (!src.Equals(_cur))
                setter?.Invoke(_cur);
        }
        protected override void OnLoopEnd()
        {
            if (loopType == LoopType.PingPong)
            {
                var temp = _start;
                _start = _end;
                _end = temp;
            }
            else if (loopType == LoopType.Add)
            {
                var tmp = calc.CalculatorEnd(_start, _end);
                _start = _end;
                _end = tmp;
            }
        }
        internal void Config(T start, T end, float duration, Func<T> getter, Action<T> setter, bool snap)
        {
            this._start = this.start = start;
            this._end = this.end = end;
            SetDuration(duration);
            this.getter = getter;
            this.setter = setter;
            this.snap = snap;
        }
    }




    class TweenScheduler : ITimerScheduler
    {
        private TimerScheduler timer;
        private Dictionary<Type, ISimpleObjectPool> contextPools;

        public TweenScheduler()
        {
            timer = TimerScheduler.CreateInstance<TimerScheduler>();
            contextPools = new Dictionary<Type, ISimpleObjectPool>();
        }

        public void Update()
        {
            timer.Update();
        }

        T ITimerScheduler.AllocateTimerContext<T>() => timer.AllocateTimerContext<T>();
        ITimerContext ITimerScheduler.RunTimerContext(TimerContext context) => timer.RunTimerContext(context);


        private List<TweenContext> contexts_run = new List<TweenContext>();
        public TweenContext<T> AllocateContext<T>()
        {
            Type type = typeof(TweenContext<T>);
            ISimpleObjectPool pool = null;
            if (!contextPools.TryGetValue(type, out pool))
            {
                pool = new SimpleObjectPool<TweenContext<T>>();
                contextPools.Add(type, pool);
            }
            var simple = pool as SimpleObjectPool<TweenContext<T>>;
            var context = simple.Get();
            contexts_run.Add(context);
            return context;
        }

        internal void Cycle(TweenContext context)
        {
            Type type = context.GetType();
            ISimpleObjectPool pool = null;
            if (!contextPools.TryGetValue(type, out pool)) return;
            contexts_run.Remove(context);
            pool.SetObject(context);
        }

        internal void CancelAllTween()
        {
            for (int i = contexts_run.Count - 1; i >= 0; i--)
            {
                var context = contexts_run[i];
                context.Cancel();
            }
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


        public static IAwaiter<T> GetAwaiter<T>(this T context) where T : ITweenContext => new ITweenContextAwaitor<T>(context);
        public static ITweenContext<T> DoGoto<T>(T start, T end, float duration, Func<T> getter, Action<T> setter, bool snap)
        {
            var context = GetScheduler().AllocateContext<T>();
            context.Config(start, end, duration, getter, setter, snap);
            context.Run();
            return context;
        }

        public static T OnComplete<T>(this T t, Action<ITweenContext> action) where T : ITweenContext
        {
            (t as TweenContext).OnComplete(action);
            return t;
        }
        public static T OnCancel<T>(this T t, Action<ITweenContext> action) where T : ITweenContext
        {
            (t as TweenContext).OnCancel(action);
            return t;
        }
        public static T OnTick<T>(this T t, Action<ITweenContext, float, float> action) where T : ITweenContext
        {
            (t as TweenContext).OnTick(action);
            return t;
        }
        public static T SetEvaluator<T>(this T t, IValueEvaluator evaluator) where T : ITweenContext
        {
            (t as TweenContext).SetEvaluator(evaluator);
            return t;
        }
        public static T SetTimeScale<T>(this T t, float value) where T : ITweenContext
        {
            (t as TweenContext).SetTimeScale(value);
            return t;
        }
        public static T SetDelay<T>(this T t, float value) where T : ITweenContext
        {
            (t as TweenContext).SetDelay(value);
            return t;
        }
        public static T SetSourceDelta<T>(this T t, float delta) where T : ITweenContext
        {
            (t as TweenContext).SetSourceDelta(delta);
            return t;
        }
        public static T SetEase<T>(this T t, Ease ease) where T : ITweenContext => t.SetEvaluator(new EaseEvaluator(ease));

        public static T SetAnimationCurve<T>(this T t, AnimationCurve curve) where T : ITweenContext => t.SetEvaluator(new AnimationCurveEvaluator(curve));

        public static T SetLoop<T>(this T context, LoopType type, int loops) where T : ITweenContext
        {
            var _context = (context as TweenContext);
            _context.SetLoop(type, loops);
            return context;
        }

        public static T AddTo<T>(this T context, ITweenContextBox box) where T : ITweenContext
        {
            box.AddTween(context);
            return context;
        }

        public static void Cancel(this ITweenContext context) => context.Complete(false);

        public static ITweenContext<T> SetSnap<T>(this ITweenContext<T> t, bool value)
        {
            (t as TweenContext<T>).SetSnap(value);
            return t;
        }
        public static ITweenContext<T> SetEnd<T>(this ITweenContext<T> t, T value)
        {
            (t as TweenContext<T>).SetEnd(value);
            return t;
        }
        public static ITweenContext<T> SetStart<T>(this ITweenContext<T> t, T value)
        {
            (t as TweenContext<T>).SetStart(value);
            return t;
        }
        public static ITweenContext<T> As<T>(this ITweenContext t) => t as ITweenContext<T>;

        public static void CancelAllTween() => GetScheduler().CancelAllTween();

        static Dictionary<Type, object> value_calcs = new Dictionary<Type, object>()
        {
            { typeof(float), new ValueCalculator_Float() },
            { typeof(bool), new ValueCalculator_Bool() },
            { typeof(int), new ValueCalculator_Int() },
            { typeof(Vector2), new ValueCalculator_Vector2() },
            { typeof(Vector3), new ValueCalculator_Vector3() },
            { typeof(Vector4), new ValueCalculator_Vector4() },
            { typeof(Color), new ValueCalculator_Color() },
            { typeof(Quaternion), new ValueCalculator_Quaternion() },
            { typeof(Rect), new ValueCalculator_Rect() },
            { typeof(long), new ValueCalculator_Long() },
            { typeof(short), new ValueCalculator_Short() },
            { typeof(Vector3Int), new ValueCalculator_Vector3Int() },
            { typeof(Vector2Int), new ValueCalculator_Vector2Int() },
        };
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
        public abstract T Calculate(T start, T end, float percent, T srcValue, float srcPercent, bool snap);

        public abstract T CalculatorEnd(T start, T end);
    }
    class ValueCalculator_Float : ValueCalculator<float>
    {
        public override float Calculate(float start, float end, float percent, float srcValue, float srcPercent, bool snap)
        {
            float dest = Mathf.Lerp(start, end, percent);
            dest = Mathf.Lerp(srcValue, dest, srcPercent);
            if (snap)
                return Mathf.RoundToInt(dest);
            return dest;
        }

        public override float CalculatorEnd(float start, float end) => end + end - start;
    }
    class ValueCalculator_Int : ValueCalculator<int>
    {
        public override int Calculate(int start, int end, float percent, int srcValue, float srcPercent, bool snap)
        {
            float dest = Mathf.Lerp(start, end, percent);
            dest = Mathf.Lerp(srcValue, dest, srcPercent);
            return Mathf.RoundToInt(dest);
        }

        public override int CalculatorEnd(int start, int end) => end + end - start;
    }
    class ValueCalculator_Long : ValueCalculator<long>
    {
        public override long Calculate(long start, long end, float percent, long srcValue, float srcPercent, bool snap)
        {
            float dest = Mathf.Lerp(start, end, percent);
            dest = Mathf.Lerp(srcValue, dest, srcPercent);
            return Mathf.RoundToInt(dest);
        }

        public override long CalculatorEnd(long start, long end) => end + end - start;
    }
    class ValueCalculator_Short : ValueCalculator<short>
    {
        public override short Calculate(short start, short end, float percent, short srcValue, float srcPercent, bool snap)
        {
            float dest = Mathf.Lerp(start, end, percent);
            dest = Mathf.Lerp(srcValue, dest, srcPercent);
            return (short)Mathf.RoundToInt(dest);
        }

        public override short CalculatorEnd(short start, short end) => (short)(end + end - start);
    }



    class ValueCalculator_Bool : ValueCalculator<bool>
    {
        public override bool Calculate(bool start, bool end, float percent, bool srcValue, float srcPercent, bool snap)
        {
            return percent >= 1 ? end : start;
        }

        public override bool CalculatorEnd(bool start, bool end)
        {
            Log.FE($"type:{typeof(bool)} not support {nameof(LoopType)}.{nameof(LoopType.Add)}");
            return start || end;
        }
    }
    class ValueCalculator_Color : ValueCalculator<Color>
    {
        public override Color Calculate(Color start, Color end, float percent, Color srcValue, float srcPercent, bool snap)
        {
            Color dest = Color.Lerp(start, end, percent);
            dest = Color.Lerp(srcValue, dest, srcPercent);
            if (snap)
            {
                dest.a = Mathf.RoundToInt(dest.a);
                dest.r = Mathf.RoundToInt(dest.r);
                dest.g = Mathf.RoundToInt(dest.g);
                dest.b = Mathf.RoundToInt(dest.b);
            }

            return dest;
        }

        public override Color CalculatorEnd(Color start, Color end) => end + end - start;
    }
    class ValueCalculator_Quaternion : ValueCalculator<Quaternion>
    {
        public override Quaternion Calculate(Quaternion start, Quaternion end, float percent, Quaternion srcValue, float srcPercent, bool snap)
        {
            Quaternion dest = Quaternion.Lerp(start, end, percent);
            dest = Quaternion.Lerp(srcValue, dest, srcPercent);
            if (snap)
            {
                dest.x = Mathf.RoundToInt(dest.x);
                dest.y = Mathf.RoundToInt(dest.y);
                dest.z = Mathf.RoundToInt(dest.z);
                dest.z = Mathf.RoundToInt(dest.z);
            }

            return dest;
        }

        public override Quaternion CalculatorEnd(Quaternion start, Quaternion end)
        {
            var _start = start.eulerAngles;
            var _end = end.eulerAngles;
            return Quaternion.Euler(_end + _end - _start);
        }
    }
    class ValueCalculator_Rect : ValueCalculator<Rect>
    {
        private static Rect Lerp(Rect r, Rect a, Rect b, float t)
        {
            r.x = Mathf.Lerp(a.x, b.x, t);
            r.y = Mathf.Lerp(a.y, b.y, t);
            r.width = Mathf.Lerp(a.width, b.width, t);
            r.height = Mathf.Lerp(a.height, b.height, t);
            return r;
        }
        public override Rect Calculate(Rect start, Rect end, float percent, Rect srcValue, float srcPercent, bool snap)
        {
            Rect dest = Lerp(start, start, end, percent);
            dest = Lerp(start, srcValue, dest, srcPercent);
            if (snap)
            {
                dest.x = Mathf.RoundToInt(dest.x);
                dest.y = Mathf.RoundToInt(dest.y);
                dest.width = Mathf.RoundToInt(dest.width);
                dest.height = Mathf.RoundToInt(dest.height);
            }
            return dest;
        }

        public override Rect CalculatorEnd(Rect start, Rect end)
        {
            end.x *= 2;
            end.y *= 2;
            end.width *= 2;
            end.height *= 2;
            end.x -= start.x;
            end.y -= start.y;
            end.width -= start.width;
            end.height -= start.height;
            return end;
        }
    }
    class ValueCalculator_Vector2 : ValueCalculator<Vector2>
    {
        public override Vector2 Calculate(Vector2 start, Vector2 end, float percent, Vector2 srcValue, float srcPercent, bool snap)
        {
            Vector2 dest = Vector2.Lerp(start, end, percent);
            dest = Vector2.Lerp(srcValue, dest, srcPercent);
            if (snap)
            {
                dest.x = Mathf.RoundToInt(dest.x);
                dest.y = Mathf.RoundToInt(dest.y);
            }
            return dest;
        }

        public override Vector2 CalculatorEnd(Vector2 start, Vector2 end) => end + end - start;
    }
    class ValueCalculator_Vector3 : ValueCalculator<Vector3>
    {
        public override Vector3 Calculate(Vector3 start, Vector3 end, float percent, Vector3 srcValue, float srcPercent, bool snap)
        {
            Vector3 dest = Vector3.Lerp(start, end, percent);
            dest = Vector3.Lerp(srcValue, dest, srcPercent);
            if (snap)
            {
                dest.x = Mathf.RoundToInt(dest.x);
                dest.y = Mathf.RoundToInt(dest.y);
                dest.z = Mathf.RoundToInt(dest.z);
            }
            return dest;
        }

        public override Vector3 CalculatorEnd(Vector3 start, Vector3 end) => end + end - start;
    }
    class ValueCalculator_Vector4 : ValueCalculator<Vector4>
    {
        public override Vector4 Calculate(Vector4 start, Vector4 end, float percent, Vector4 srcValue, float srcPercent, bool snap)
        {
            Vector4 dest = Vector4.Lerp(start, end, percent);
            dest = Vector4.Lerp(srcValue, dest, srcPercent);
            if (snap)
            {
                dest.x = Mathf.RoundToInt(dest.x);
                dest.y = Mathf.RoundToInt(dest.y);
                dest.z = Mathf.RoundToInt(dest.z);
                dest.w = Mathf.RoundToInt(dest.w);

            }
            return dest;
        }

        public override Vector4 CalculatorEnd(Vector4 start, Vector4 end) => end + end - start;
    }


    class ValueCalculator_Vector2Int : ValueCalculator<Vector2Int>
    {
        public override Vector2Int Calculate(Vector2Int start, Vector2Int end, float percent, Vector2Int srcValue, float srcPercent, bool snap)
        {
            Vector2 dest = Vector2.Lerp(start, end, percent);
            dest = Vector2.Lerp(srcValue, dest, srcPercent);

            dest.x = Mathf.RoundToInt(dest.x);
            dest.y = Mathf.RoundToInt(dest.y);

            return new Vector2Int((int)dest.x, (int)dest.y);
        }

        public override Vector2Int CalculatorEnd(Vector2Int start, Vector2Int end) => end + end - start;
    }
    class ValueCalculator_Vector3Int : ValueCalculator<Vector3Int>
    {
        public override Vector3Int Calculate(Vector3Int start, Vector3Int end, float percent, Vector3Int srcValue, float srcPercent, bool snap)
        {
            Vector3 dest = Vector3.Lerp(start, end, percent);
            dest = Vector3.Lerp(srcValue, dest, srcPercent);
            dest.x = Mathf.RoundToInt(dest.x);
            dest.y = Mathf.RoundToInt(dest.y);
            dest.z = Mathf.RoundToInt(dest.z);
            return new Vector3Int((int)dest.x, (int)dest.y, (int)dest.z);
        }

        public override Vector3Int CalculatorEnd(Vector3Int start, Vector3Int end) => end + end - start;
    }

}