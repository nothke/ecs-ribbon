using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Burst;
using Unity.Entities;

public struct LiquidParticle : IComponentData
{
    public float3 position;
    public float3 velocity;

    public float amount;
    public float heat;

    public Entity prev;
}

public class LiquidParticlesAdvanceSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float dt = Time.DeltaTime;
        float3 gravity = Physics.gravity;

        Entities.ForEach((ref LiquidParticle particle) =>
        {
            particle.velocity += gravity * dt;
            particle.position += particle.velocity * dt;
        }).ScheduleParallel();
    }
}

[DisableAutoCreation]
public class LiquidParticlesDebugPositionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float3 r = right() * 0.02f;
        float3 u = up() * 0.02f;
        Entities.ForEach((ref LiquidParticle particle) =>
        {
            Debug.DrawRay(particle.position - r, r * 2);
            Debug.DrawRay(particle.position - u, u * 2);
        }).Schedule();
    }
}

// Just for testing
public class LiquidParticleDestroyBelowZeroSystem : SystemBase
{
    EndSimulationEntityCommandBufferSystem commandBufferSystem;

    protected override void OnCreate()
    {
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var ecb = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();

        Entities.ForEach((Entity entity, int entityInQueryIndex, ref LiquidParticle particle) =>
        {
            if (particle.position.y < 0)
                ecb.DestroyEntity(entityInQueryIndex, entity);
        }).ScheduleParallel();

        commandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}

public class LiquidParticlesDebugLinksSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var cdfe = GetComponentDataFromEntity<LiquidParticle>(isReadOnly: true);

        Entities
            .WithNativeDisableContainerSafetyRestriction(cdfe)
            .WithReadOnly(cdfe)
            .ForEach((ref LiquidParticle particle) =>
        {
            if (particle.prev == Entity.Null)
                return;

            if (!cdfe.HasComponent(particle.prev))
                return;

            LiquidParticle prev = cdfe[particle.prev];
            Debug.DrawLine(prev.position, particle.position);

        }).Schedule();
    }
}

public class LiquidParticleRaycastIntoWorldSystem : SystemBase
{
    EndSimulationEntityCommandBufferSystem commandBufferSystem;

    protected override void OnCreate()
    {
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var ecb = commandBufferSystem.CreateCommandBuffer();
        float dt = Time.DeltaTime;

        // TODO: Fill up a raycast command query instead

        Entities.ForEach((Entity entity, ref LiquidParticle particle) =>
        {
            RaycastHit hit;
            if (Physics.Raycast(particle.position, particle.velocity, out hit,
                length(particle.velocity) * dt))
            {
                ecb.DestroyEntity(entity);
                Debug.DrawRay(particle.position, hit.normal, Color.red);
            }
        }).WithoutBurst().Run();
    }
}