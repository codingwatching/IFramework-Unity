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
        bool isDone { get; }
        void Pause();
        void UnPause();
        void Complete(bool callComplete);
    }

    abstract class TweenContext : ITweenContext,IPoolObject
    {
        bool ITweenContext.isDone => _context == null ? false : _context.isDone || _context.canceled;

        private float _time { get; set; }
        protected float percent { get { return (Mathf.Clamp01((_time) / duration)); } }
        protected float convertPercent { get { return evaluator.Evaluate(percent, _time, duration); } }
        protected float deltaPercent { get { return (1 - sourceDelta) + sourceDelta * percent; } }
        private IValueEvaluator _evaluator = EaseEvaluator.Default;
        internal IValueEvaluator evaluator { get { return _evaluator; } set { _evaluator = value; } }
        internal LoopType _loopType = LoopType.ReStart;
        internal int _loop = 1;
        internal LoopType loopType { get { return _loopType; } set { _loopType = value; } }
        internal int loop { get { return _loop; } set { _loop = value; } }
        protected float duration { get; set; }

        internal event Action onComplete;


        public float sourceDelta { get; set; }

        private ITimerContext _context;


        void ITweenContext.Pause() => _context?.Pause();
        void ITweenContext.UnPause() => _context?.UnPause();

        void ITweenContext.Complete(bool callComplete)
        {
            if (((ITweenContext)this).isDone) return;
            if (callComplete)
                _context.Complete();
            else
                _context.Cancel();
            Tween.GetScheduler().Cycle(this);
        }

        private int _loop_count;
        protected abstract void OnLoopEnd();
        private bool WhileCheck(float time)
        {
            bool result = loop == -1 || _loop_count < loop;
            var resultTime = time % duration;
            if (duration - resultTime <= 0.01f)
            {
                SetTime(duration);
                OnLoopEnd();
                _loop_count++;
            }
            else
            {
                SetTime(resultTime);

            }


            return result;
        }

        internal async void Run()
        {
            TweenScheduler scheduler = Tween.GetScheduler();
            await scheduler.Frame();
            _context = scheduler.DoWhile(WhileCheck);
            await _context;
            onComplete?.Invoke();
            (this as ITweenContext).Complete(true);
        }



        private void SetTime(float time)
        {
            this._time = time;
            Update();
        }
        protected abstract void Update();
        private void Reset()
        {
            _context = null;
            onComplete = null;
            _time = 0;
            _evaluator = EaseEvaluator.Default;
            _loop_count = 0;
            loop = 1;
            loopType = LoopType.ReStart;
            sourceDelta = 0;
        }
        void IPoolObject.OnGet() => Reset();

        void IPoolObject.OnSet() => Reset();
    }
    class TweenContext<T> : TweenContext
    {

        private static ValueCalculator<T> _calc;
        private T start;
        private T end;

        private T _start;
        private T _end;

        private Func<T> getter;
        private Action<T> setter;
        private bool snap;

        private static ValueCalculator<T> calc
        {
            get
            {
                if (_calc == null)
                    _calc = Tween.GetValueCalculator<T>();
                return _calc;
            }
        }
        protected sealed override void Update()
        {

            var src = getter.Invoke();
            T _cur = calc.Calculator(_start, _end, convertPercent, src, deltaPercent, snap);
            if (!src.Equals(_cur))
                setter?.Invoke(_cur);
        }
        protected override void OnLoopEnd()
        {
            if (loopType == LoopType.PingPong)
            {
                _start = end;
                _end = start;
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
            this.duration = duration;
            this.getter = getter;
            this.setter = setter;
            this.snap = snap;
        }
    }




    class TweenScheduler : ITimerScheduler, ITimerContextBox, IDisposable
    {
        private TimerScheduler timer;
        private TimerContextBox box;
        private Dictionary<Type, ISimpleObjectPool> contextPools;

        public TweenScheduler()
        {
            timer = TimerScheduler.CreateInstance<TimerScheduler>();
            box = new TimerContextBox();
            contextPools = new Dictionary<Type, ISimpleObjectPool>();
        }

        void ITimerContextBox.AddTimer(ITimerContext context) => box.AddTimer(context);

        void ITimerContextBox.CancelTimer(ITimerContext context) => box.CancelTimer(context);

        void ITimerContextBox.CancelTimers() => box.CancelTimers();
        void ITimerContextBox.CompleteTimer(ITimerContext context) => box.CompleteTimer(context);
        void ITimerContextBox.CompleteTimers() => box.CompleteTimers();
        public void Dispose()
        {
            (this as ITimerContextBox).CancelTimers();
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
            Type type = typeof(T);
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
            scheduler.Dispose();
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


        public static IAwaiter<ITweenContext> GetAwaiter(this ITweenContext context) => new ITweenContextAwaitor(context);
        public static ITweenContext DoGoto<T>(T start, T end, float duration, Func<T> getter, Action<T> setter, bool snap)
        {
            var context = GetScheduler().AllocateContext<T>();
            context.Config(start, end, duration, getter, setter, snap);
            context.Run();
            return context;
        }

        public static T OnComplete<T>(this T t, Action action) where T : ITweenContext
        {
            (t as TweenContext).onComplete += action;
            return t;
        }
        public static T SetEvaluator<T>(this T t, IValueEvaluator evaluator) where T : ITweenContext
        {
            (t as TweenContext).evaluator = evaluator;
            return t;
        }
        public static T SetSourceDelta<T>(this T t, float delta) where T : ITweenContext
        {
            (t as TweenContext).sourceDelta = delta;
            return t;
        }
        public static T SetEase<T>(this T t, Ease ease) where T : ITweenContext => t.SetEvaluator(new EaseEvaluator(ease));

        public static T SetAnimationCurve<T>(this T t, AnimationCurve curve) where T : ITweenContext => t.SetEvaluator(new AnimationCurveEvaluator(curve));
        public static void CancelAllTween() => GetScheduler().CancelAllTween();

        public static T SetLoop<T>(this T context, LoopType type, int loops) where T : ITweenContext
        {
            var _context = (context as TweenContext);
            _context.loop = loops;
            _context.loopType = type;
            return context;
        }

        public static void Cancel(this ITweenContext context) => context.Complete(false);
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
    struct ITweenContextAwaitor : IAwaiter<ITweenContext>
    {
        private ITweenContext op;
        private Queue<Action> actions;
        public ITweenContextAwaitor(ITweenContext op)
        {
            this.op = op;
            actions = new Queue<Action>();
            op.OnComplete(OnCompleted);
        }

        private void OnCompleted()
        {
            while (actions.Count > 0)
            {
                actions.Dequeue()?.Invoke();
            }
        }

        public bool IsCompleted => op.isDone;

        public ITweenContext GetResult() => op;
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
        public abstract T Calculator(T start, T end, float percent, T srcValue, float srcPercent, bool snap);

        public abstract T CalculatorEnd(T start, T end);
    }
    class ValueCalculator_Float : ValueCalculator<float>
    {
        public override float Calculator(float start, float end, float percent, float srcValue, float srcPercent, bool snap)
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
        public override int Calculator(int start, int end, float percent, int srcValue, float srcPercent, bool snap)
        {
            float dest = Mathf.Lerp(start, end, percent);
            dest = Mathf.Lerp(srcValue, dest, srcPercent);
            return Mathf.RoundToInt(dest);
        }

        public override int CalculatorEnd(int start, int end) => end + end - start;
    }
    class ValueCalculator_Long : ValueCalculator<long>
    {
        public override long Calculator(long start, long end, float percent, long srcValue, float srcPercent, bool snap)
        {
            float dest = Mathf.Lerp(start, end, percent);
            dest = Mathf.Lerp(srcValue, dest, srcPercent);
            return Mathf.RoundToInt(dest);
        }

        public override long CalculatorEnd(long start, long end) => end + end - start;
    }
    class ValueCalculator_Short : ValueCalculator<short>
    {
        public override short Calculator(short start, short end, float percent, short srcValue, float srcPercent, bool snap)
        {
            float dest = Mathf.Lerp(start, end, percent);
            dest = Mathf.Lerp(srcValue, dest, srcPercent);
            return (short)Mathf.RoundToInt(dest);
        }

        public override short CalculatorEnd(short start, short end) => (short)(end + end - start);
    }



    class ValueCalculator_Bool : ValueCalculator<bool>
    {
        public override bool Calculator(bool start, bool end, float percent, bool srcValue, float srcPercent, bool snap)
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
        public override Color Calculator(Color start, Color end, float percent, Color srcValue, float srcPercent, bool snap)
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
        public override Quaternion Calculator(Quaternion start, Quaternion end, float percent, Quaternion srcValue, float srcPercent, bool snap)
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
        public override Rect Calculator(Rect start, Rect end, float percent, Rect srcValue, float srcPercent, bool snap)
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
        public override Vector2 Calculator(Vector2 start, Vector2 end, float percent, Vector2 srcValue, float srcPercent, bool snap)
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
        public override Vector3 Calculator(Vector3 start, Vector3 end, float percent, Vector3 srcValue, float srcPercent, bool snap)
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
        public override Vector4 Calculator(Vector4 start, Vector4 end, float percent, Vector4 srcValue, float srcPercent, bool snap)
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
        public override Vector2Int Calculator(Vector2Int start, Vector2Int end, float percent, Vector2Int srcValue, float srcPercent, bool snap)
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
        public override Vector3Int Calculator(Vector3Int start, Vector3Int end, float percent, Vector3Int srcValue, float srcPercent, bool snap)
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