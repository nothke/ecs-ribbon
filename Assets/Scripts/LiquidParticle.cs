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
    public int sortIndex;
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

public class LiquidParticlesRaycastBatchSystem : SystemBase
{
    EndSimulationEntityCommandBufferSystem commandBufferSystem;

    EntityQuery query;

    protected override void OnCreate()
    {
        commandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        query = GetEntityQuery(
            ComponentType.ReadOnly<LiquidParticle>());
    }

    protected override void OnUpdate()
    {
        int ct = query.CalculateEntityCount();

        var entityList = new NativeArray<Entity>(ct, Allocator.TempJob);
        var commandsList = new NativeArray<RaycastCommand>(ct, Allocator.TempJob);
        float dt = Time.DeltaTime;

        // Prepare PASS

        Dependency = Entities
            .WithName("Prepare_RaycastCommands")
            .ForEach((Entity entity, int entityInQueryIndex, ref LiquidParticle particle) =>
            {
                commandsList[entityInQueryIndex] = new RaycastCommand(
                    particle.position, particle.velocity,
                    length(particle.velocity) * dt);

                entityList[entityInQueryIndex] = entity;
            }).Schedule(Dependency);

        // Raycast PASS

        var hitResults = new NativeArray<RaycastHit>(ct, Allocator.TempJob);
        Dependency = RaycastCommand.ScheduleBatch(
            commandsList, hitResults, 32, Dependency);

        commandsList.Dispose(Dependency);

        // Kill PASS

        var ecb = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();

        Dependency = Job
            .WithReadOnly(hitResults)
            .WithCode(() =>
            {
                for (int i = 0; i < ct; i++)
                {
                    if (RaycastUtil.GetColliderID(hitResults[i]) != 0)
                        ecb.DestroyEntity(i, entityList[i]);
                }
            })
            .WithDisposeOnCompletion(entityList)
            .WithDisposeOnCompletion(hitResults)
            .WithBurst()
            .Schedule(Dependency);

        commandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}

[DisableAutoCreation]
public class LiquidParticlesRaycastSystem : SystemBase
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

        commandBufferSystem.AddJobHandleForProducer(Dependency);
    }
}

public class LiquidParticleLineRenderingSystem : SystemBase
{ 
    EntityQuery query;

    public LineRenderer[] renderers;

    protected override void OnCreate()
    {
        query = GetEntityQuery(typeof(LiquidParticle));
    }

    public struct Sortable : System.IComparable<Sortable>
    {
        public Entity entity;
        public Entity prev;
        public int index;

        public int CompareTo(Sortable other)
        {
            return index.CompareTo(other.index);
        }
    }

    protected override void OnUpdate()
    {
        //var cdfe = GetComponentDataFromEntity<LiquidParticle>(true);

        var entities = query.ToEntityArray(Allocator.TempJob);
        var components = query.ToComponentDataArray<LiquidParticle>(Allocator.TempJob);

        var sortables = new NativeArray<Sortable>(entities.Length, Allocator.TempJob);

        for (int i = 0; i < sortables.Length; i++)
        {
            sortables[i] = new Sortable()
            {
                entity = entities[i],
                prev = components[i].prev,
                index = components[i].sortIndex
            };
        }

        sortables.Sort();

        for (int i = sortables.Length - 1; i >= 1; i--)
        {
            if (sortables[i].prev == sortables[i - 1].entity)
            {
                Debug.Log("Connected");
            }
            else
                Debug.Log("DIS");
        }

        entities.Dispose();
        components.Dispose();
        sortables.Dispose();
    }
}