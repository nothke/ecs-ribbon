using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SomeMotion : MonoBehaviour
{
    public float scale = 1;

    void Update()
    {
        transform.localPosition = new Vector3(
            -0.5f + Mathf.PerlinNoise(Time.time * 1.23f, 34.53123f),
            -0.5f + Mathf.PerlinNoise(12.43f, Time.time * 2.34f),
            0) * scale;
    }
}
