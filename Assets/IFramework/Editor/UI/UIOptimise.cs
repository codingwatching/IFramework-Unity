/*********************************************************************************
 *Author:         OnClick
 *Date:           2025-04-03
*********************************************************************************/
using System;
using UnityEditor;
using UnityEngine.UI;

namespace IFramework.UI
{
    class UIOptimize
    {
        [OnAddComponent(typeof(Text))]
        static void Text(Text txt)
        {
            txt.raycastTarget = false;
            txt.supportRichText = false;
            txt.resizeTextForBestFit = false;
        }
        [OnAddComponent(typeof(Image))]
        static void Image(Image img)
        {
            img.raycastTarget = false;
        }
        [OnAddComponent(typeof(RawImage))]
        static void RawImage(RawImage img)
        {
            img.raycastTarget = false;
        }
        [OnAddComponent(typeof(InputField))]
        static void InputField(InputField input)
        {
            if (input.targetGraphic)
                input.targetGraphic.raycastTarget = true;
        }

        [OnAddComponent(typeof(Dropdown))]
        static void Dropdown(Dropdown dropdown)
        {
            if (dropdown.targetGraphic)
                dropdown.targetGraphic.raycastTarget = true;
        }
        [OnAddComponent(typeof(Button))]
        static void Button(Button btn)
        {
            if (btn.targetGraphic)
                btn.targetGraphic.raycastTarget = true;
        }
        static void DelayCall(Action action)
        {
            EditorApplication.delayCall += () =>
            {

                action?.Invoke();
            };
        }

        [OnAddComponent(typeof(Slider))]
        static void Slider(Slider slider)
        {
            DelayCall(() =>
            {
                var graphic = slider.transform.GetChild(0)?.GetComponent<Graphic>();
                if (graphic)
                {
                    graphic.raycastTarget = true;

                }
                if (slider.targetGraphic)
                    slider.targetGraphic.raycastTarget = true;

            });
        }

        [OnAddComponent(typeof(Scrollbar))]
        static void Scrollbar(Scrollbar bar)
        {
            DelayCall(() =>
            {
                if (bar.targetGraphic)
                    bar.targetGraphic.raycastTarget = true;

            });

        }
        [OnAddComponent(typeof(ScrollRect))]
        static void ScrollRect(ScrollRect rect)
        {
            DelayCall(() =>
            {

                if (rect.viewport)
                {
                    var graphic = rect.viewport.GetComponent<Graphic>();
                    if (graphic)
                    {
                        graphic.raycastTarget = true;
                    }
                }
            });
        }


        [OnAddComponent(typeof(Toggle))]
        static void Toggle(Toggle toggle)
        {
            DelayCall(() =>
            {
                if (toggle.targetGraphic)
                    toggle.targetGraphic.raycastTarget = true;
                if (toggle.graphic)
                    toggle.graphic.raycastTarget = true;
            });
        }

    }
}
