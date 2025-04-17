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


    public abstract class TweenComponentActor<TTarget, T> : TweenComponentActor
    {
        [NonSerialized] private float _percent;
        internal sealed override float percent => _percent;
        internal sealed override void ResetPercent() => _percent = 0;
        internal override ITweenContext Create()
        {
            if (target == null)
                target = transform.GetComponent<TTarget>();
            _percent = 0;
            var context = OnCreate();
            context.SetLoop(loopType, loops).SetDelay(delay).SetSnap(snap).SetDuration(duration);
            if (curveType == CurveType.Ease)
                context.SetEase(ease);
            else
                context.SetAnimationCurve(curve);
#if UNITY_EDITOR
            context.OnTick(OnTick);
            context.OnComplete(OnEnd);

#endif
            return context;
        }

        private void OnEnd(ITweenContext context) => _percent = 1;

        private void OnTick(ITweenContext context, float time, float delta) => _percent = time / duration;

        protected abstract ITweenContext<T> OnCreate();

        public TTarget target;


    }
    [System.Serializable]
    public abstract class TweenComponentActor
    {
        internal abstract float percent { get; }
        internal Transform transform { get; set; }
        public enum CurveType
        {
            Ease,
            AnimationCurve,
        }
        public CurveType curveType = CurveType.Ease;
        public LoopType loopType = LoopType.Restart;
        public int loops = 1;
        public float delay = 0;
        public float duration = 1;
        public bool snap = false;
        public AnimationCurve curve = new AnimationCurve();
        public Ease ease;
        public enum StartValueType
        {
            Relative,
            Direct
        }
        public StartValueType startType;
        internal abstract ITweenContext Create();

        internal abstract void ResetPercent();
    }
    class DoLocalScale : TweenComponentActor<Transform, Vector3>
    {
        public Vector3 start = Vector3.one;
        public Vector3 end = Vector3.one;
        protected override ITweenContext<Vector3> OnCreate()
        {
            if (startType == StartValueType.Relative)
                return Tween.DoLocalScale(target, end, duration, snap);
            return Tween.DoLocalScale(target, start, end, duration, snap);
        }
    }
    class DoPosition : TweenComponentActor<Transform, Vector3>
    {
        public bool local;
        public Vector3 start = Vector3.zero;
        public Vector3 end = Vector3.one;
        protected override ITweenContext<Vector3> OnCreate()
        {
            if (local)
            {
                if (startType == StartValueType.Relative)
                    return Tween.DoLocalPosition(target, end, duration, snap);
                return Tween.DoLocalPosition(target, start, end, duration, snap);
            }
            if (startType == StartValueType.Relative)
                return Tween.DoPosition(target, end, duration, snap);
            return Tween.DoPosition(target, start, end, duration, snap);

        }
    }
    class DoRotate : TweenComponentActor<Transform, Vector3>
    {
        public bool local;
        public Vector3 start = Vector3.zero;
        public Vector3 end = Vector3.one;

        protected override ITweenContext<Vector3> OnCreate()
        {
            if (local)
            {
                if (startType == StartValueType.Relative)
                    return Tween.DoLocalRotate(target, end, duration, snap);
                return Tween.DoLocalRotate(target, start, end, duration, snap);
            }
            if (startType == StartValueType.Relative)
                return Tween.DoRotate(target, end, duration, snap);
            return Tween.DoRotate(target, start, end, duration, snap);

        }
    }


    class DoShakePosition : DoPosition
    {
        public Vector3 strength = Vector3.one;
        public int frequency = 10;
        public float dampingRatio = 1;
        protected override ITweenContext<Vector3> OnCreate()
        {
            if (local)
            {
                if (startType == StartValueType.Relative)
                    return Tween.DoShakeLocalPosition(target, end, duration, strength, frequency, dampingRatio, snap);
                return Tween.DoShakeLocalPosition(target, start, end, duration, strength, frequency, dampingRatio, snap);
            }
            if (startType == StartValueType.Relative)
                return Tween.DoShakePosition(target, end, duration, strength, frequency, dampingRatio, snap);
            return Tween.DoShakePosition(target, start, end, duration, strength, frequency, dampingRatio, snap);

        }
    }

    class DoShakeRotation : DoRotate
    {
        public Vector3 strength = Vector3.one;
        public int frequency = 10;
        public float dampingRatio = 1;
        protected override ITweenContext<Vector3> OnCreate()
        {
            if (local)
            {
                if (startType == StartValueType.Relative)
                    return Tween.DoShakeLocalRotate(target, end, duration, strength, frequency, dampingRatio, snap);
                return Tween.DoShakeLocalRotate(target, start, end, duration, strength, frequency, dampingRatio, snap);
            }
            if (startType == StartValueType.Relative)
                return Tween.DoShakeRotate(target, end, duration, strength, frequency, dampingRatio, snap);
            return Tween.DoShakeRotate(target, start, end, duration, strength, frequency, dampingRatio, snap);

        }
    }
    class DoShakeScale : DoLocalScale
    {
        public Vector3 strength = Vector3.one;
        public int frequency = 10;
        public float dampingRatio = 1;
        protected override ITweenContext<Vector3> OnCreate()
        {
            if (startType == StartValueType.Relative)
                return Tween.DoShakeLocalScale(target, end, duration, strength, frequency, dampingRatio, snap);
            return Tween.DoShakeLocalScale(target, start, end, duration, strength, frequency, dampingRatio, snap);

        }
    }



    partial class Tween
    {
        public static ITweenContext<Vector3> DoLocalScale(Transform target, Vector3 start, Vector3 end, float duration, bool snap = false)
            => Tween.DoGoto(start, end, duration, () => target.localScale, (value) => target.localScale = value, snap);
        public static ITweenContext<Vector3> DoLocalScale(Transform target, Vector3 end, float duration, bool snap = false)
            => DoLocalScale(target, target.localScale, end, duration, snap);
        public static ITweenContext<Vector3> DoPosition(Transform target, Vector3 start, Vector3 end, float duration, bool snap = false)
           => Tween.DoGoto(start, end, duration, () => target.position, (value) => target.position = value, snap);
        public static ITweenContext<Vector3> DoPosition(Transform target, Vector3 end, float duration, bool snap = false)
            => DoPosition(target, target.position, end, duration, snap);
        public static ITweenContext<Vector3> DoLocalPosition(Transform target, Vector3 start, Vector3 end, float duration, bool snap = false)
          => Tween.DoGoto(start, end, duration, () => target.localPosition, (value) => target.localPosition = value, snap);
        public static ITweenContext<Vector3> DoLocalPosition(Transform target, Vector3 end, float duration, bool snap = false)
            => DoLocalPosition(target, target.localPosition, end, duration, snap);


        public static ITweenContext<Vector3> DoRotate(Transform target, Vector3 start, Vector3 end, float duration, bool snap = false)
            => Tween.DoGoto(start, end, duration, () => target.eulerAngles, (value) => target.eulerAngles = value, snap);
        public static ITweenContext<Vector3> DoRotate(Transform target, Vector3 end, float duration, bool snap = false)
            => DoRotate(target, target.eulerAngles, end, duration, snap);


        public static ITweenContext<Vector3> DoLocalRotate(Transform target, Vector3 start, Vector3 end, float duration, bool snap = false)
            => Tween.DoGoto(start, end, duration, () => target.localEulerAngles, (value) => target.localEulerAngles = value, snap);
        public static ITweenContext<Vector3> DoLocalRotate(Transform target, Vector3 end, float duration, bool snap = false)
            => DoLocalRotate(target, target.localEulerAngles, end, duration, snap);




        public static ITweenContext<Vector3> DoShakeRotate(Transform target, Vector3 start, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
     => Tween.DoShake(start, end, duration, () => target.eulerAngles, (value) => target.eulerAngles = value, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakeRotate(Transform target, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
            => DoShakeRotate(target, target.eulerAngles, end, duration, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakeLocalRotate(Transform target, Vector3 start, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
            => Tween.DoShake(start, end, duration, () => target.localEulerAngles, (value) => target.localEulerAngles = value, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakeLocalRotate(Transform target, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
            => DoShakeLocalRotate(target, target.localEulerAngles, end, duration, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakeLocalPosition(Transform target, Vector3 start, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
         => Tween.DoShake(start, end, duration, () => target.localPosition, (value) => target.localPosition = value, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakeLocalPosition(Transform target, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
            => DoShakeLocalPosition(target, target.localPosition, end, duration, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakePosition(Transform target, Vector3 start, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
     => Tween.DoShake(start, end, duration, () => target.position, (value) => target.position = value, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakePosition(Transform target, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
            => DoShakePosition(target, target.position, end, duration, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakeLocalScale(Transform target, Vector3 start, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
            => Tween.DoShake(start, end, duration, () => target.localScale, (value) => target.localScale = value, strength, frequency, dampingRatio, snap);
        public static ITweenContext<Vector3> DoShakeLocalScale(Transform target, Vector3 end, float duration, Vector3 strength, int frequency = 10, float dampingRatio = 1, bool snap = false)
            => DoShakeLocalScale(target, target.localScale, end, duration, strength, frequency, dampingRatio, snap);
    }


    [AddComponentMenu("IFramework/TweenComponent"), DisallowMultipleComponent]
    public class TweenComponent : MonoBehaviour
    {
        private enum Mode
        {
            Sequence,
            Parallel
        }
        [SerializeField] private Mode mode = Mode.Sequence;
        [SerializeField] private float timeScale = 1;
        [SerializeField] private bool PlayOnAwake;

        [SerializeReference]
        [HideInInspector]
        internal List<TweenComponentActor> actors = new List<TweenComponentActor>();

        public Action<ITweenContext> OnBegin { get; private set; }
        public Action<ITweenContext> OnComplete { get; private set; }
        public Action<ITweenContext, float, float> OnTick { get; private set; }
        public Action<ITweenContext> OnCancel { get; private set; }
        internal bool hasValue => context != null;

        public bool paused => !hasValue ? true : context.paused;

        private ITweenContext context;

        private void ResetActorsPercent()
        {
#if UNITY_EDITOR
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                actor.ResetPercent();
            }
#endif
        }
        private void Awake()
        {
            context = null;
            if (PlayOnAwake)
            {
                Play();
            }
        }
        private void RecyleContext()
        {
            context?.SetAutoCycle(true);
            context?.Cancel();
            context = null;
        }
        public void Play()
        {
            ResetActorsPercent();
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                RecyleContext();
            }
#endif
            if (context == null)
            {
                ITweenGroup group = null;
                if (mode == Mode.Sequence)
                    context = group = Tween.Sequence();
                else
                    context = group = Tween.Parallel();



                for (int i = 0; i < actors.Count; i++)
                {

                    var actor = actors[i];
                    actor.transform = transform;
                    group.NewContext(actor.Create);
                }
                group.SetAutoCycle(false)
                     .SetTimeScale(timeScale)
                     .OnBegin(OnBegin)
                     .OnComplete(OnComplete)
                     .OnTick(OnTick)
                     .OnCancel(OnCancel).Run();
            }
            else
            {
                ReStart();
            }
        }



        public void UnPause() => context?.UnPause();
        public void Pause() => context?.Pause();
        public void ReStart()
        {
            ResetActorsPercent();
            context.SetTimeScale(timeScale);
            context?.ReStart();
        }

        public void Stop() => context?.Stop();
        public void Cancel()
        {
            context?.Cancel();
        }

        private void OnDisable()
        {
            RecyleContext();
        }


    }

}