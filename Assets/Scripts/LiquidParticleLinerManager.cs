using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Profiling;

public class LiquidParticleLinerManager : MonoBehaviour
{
    public LineRenderer prefab;

    Queue<LineRenderer> inUse = new Queue<LineRenderer>();
    Queue<LineRenderer> available = new Queue<LineRenderer>();

    private void Start()
    {
        prefab.positionCount = 0;
    }

    public void Clear()
    {
        Profiler.BeginSample("Clear");

        foreach (var l in inUse)
        {
            l.positionCount = 0;
            available.Enqueue(l);
        }

        inUse.Clear();

        Profiler.EndSample();
    }

    public void ClearUnused()
    {
        foreach (var l in available)
        {
            l.enabled = false;
        }
    }

    public LineRenderer Form(NativeArray<Vector3> points, int count)

    {
        Profiler.BeginSample("Form");
        var line = Get();
        line.positionCount = count;
        line.SetPositions(points);

        Profiler.EndSample();
        return line;
    }

    public LineRenderer Get()
    {
        LineRenderer l = available.Count == 0 ?
            CreateNew() : available.Dequeue();

        inUse.Enqueue(l);
        //l.enabled = true;

        return l;
    }

    LineRenderer CreateNew()
    {
        return Instantiate(prefab);
    }
}
