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
        public Vector3 start;
        public Vector3 end;
        protected override ITweenContext<Vector3> OnCreate()
        {
            var _start = startType == StartValueType.Relative ? target.localScale : start;

            return Tween.DoGoto(_start, end, duration, () => target.localScale,
                (value) => target.localScale = value, snap);

        }
    }
    class DoPosition : TweenComponentActor<Transform, Vector3>
    {
        public Vector3 start;
        public Vector3 end;
        protected override ITweenContext<Vector3> OnCreate()
        {
            var _start = startType == StartValueType.Relative ? target.position : start;
            return Tween.DoGoto(_start, end, duration, () => target.position,
                (value) => target.position = value, snap);

        }
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