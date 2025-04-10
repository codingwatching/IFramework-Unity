using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IFramework;
public class TweenTest : MonoBehaviour
{
    // Start is called before the first frame update
    public float value = 0;
    async void OnEnable()
    {
        await Tween.DoGoto(this.value, 5f, 0.2f, () => this.value, (value) =>
            {
                this.value = value;
                Debug.LogError(value);
            }, true);
        Debug.LogError("xxl");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
