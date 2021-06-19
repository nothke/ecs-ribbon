using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        foreach (var l in inUse)
        {
            l.positionCount = 0;
            available.Enqueue(l);
        }

        inUse.Clear();
    }

    public LineRenderer Form(Vector3[] points)
    {
        var line = Get();
        line.positionCount = points.Length;
        line.SetPositions(points);
        return line;
    }

    public LineRenderer Get()
    {
        LineRenderer l = available.Count == 0 ?
            CreateNew() : available.Dequeue();

        inUse.Enqueue(l);

        return l;
    }

    LineRenderer CreateNew()
    {
        return Instantiate(prefab);
    }
}
