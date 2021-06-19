using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;

public class LiquidParticleSpawner : MonoBehaviour
{
    EntityManager manager;
    EntityArchetype arch;

    public LineRenderer prefab;

    public float speed = 3;

    private void Start()
    {
        manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        arch = manager.CreateArchetype(
            typeof(ParticleMotion),
            typeof(LiquidParticle));
    }

    Entity prev;

    int i = 0;

    private void Update()
    {
        transform.position +=
            new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        var e = manager.CreateEntity(arch);
        manager.SetComponentData(e, new ParticleMotion()
        {
            position = transform.position,
            velocity = transform.forward * speed,
        });

        manager.SetComponentData(e, new LiquidParticle()
        {
            amount = 1,
            heat = 0,
            prev = prev,

            sortIndex = i++
        });

        prev = e;
    }
}
