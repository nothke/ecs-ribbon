using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Entities;

public class LiquidParticleSpawner : MonoBehaviour
{
    EntityManager manager;
    EntityArchetype arch;

    public float speed = 3;

    private void Start()
    {
        manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        arch = manager.CreateArchetype(typeof(LiquidParticle));
    }

    Entity prev;

    private void Update()
    {
        var e = manager.CreateEntity(arch);
        manager.SetComponentData(e, new LiquidParticle()
        {
            position = transform.position,
            velocity = transform.forward * speed,

            amount = 1,
            heat = 0,
            prev = prev
        });

        prev = e;
    }
}
