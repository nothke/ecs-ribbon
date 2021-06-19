using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Burst;
using Unity.Entities;
using UnityEngine.Profiling;

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

[DisableAutoCreation]
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

        var entities = new NativeArray<Entity>(ct, Allocator.TempJob);
        var commands = new NativeArray<RaycastCommand>(ct, Allocator.TempJob);
        float dt = Time.DeltaTime;

        // Prepare PASS

        Dependency = Entities
            .WithName("Prepare_Pass")
            .ForEach((Entity entity, int entityInQueryIndex, ref LiquidParticle particle) =>
            {
                commands[entityInQueryIndex] = new RaycastCommand(
                    particle.position, particle.velocity,
                    length(particle.velocity) * dt);

                entities[entityInQueryIndex] = entity;
            }).Schedule(Dependency);

        // Raycast PASS

        var hitResults = new NativeArray<RaycastHit>(ct, Allocator.TempJob);
        Dependency = RaycastCommand.ScheduleBatch(
            commands, hitResults, 32, Dependency);

        commands.Dispose(Dependency);

        // Kill PASS

        var ecb = commandBufferSystem.CreateCommandBuffer().AsParallelWriter();

        Dependency = Job
            .WithName("Kill_Pass")
            .WithReadOnly(hitResults)
            .WithCode(() =>
            {
                for (int i = 0; i < ct; i++)
                {
                    if (RaycastUtil.GetColliderID(hitResults[i]) != 0)
                        ecb.DestroyEntity(i, entities[i]);
                }
            })
            .WithDisposeOnCompletion(entities)
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

    LiquidParticleLinerManager lines;

    const int MAX_POINTS_IN_BUFFER = 256;
    NativeArray<Vector3> points;

    protected override void OnCreate()
    {
        query = GetEntityQuery(typeof(LiquidParticle));

        points = new NativeArray<Vector3>(MAX_POINTS_IN_BUFFER, Allocator.Persistent);
        lines = Object.FindObjectOfType<LiquidParticleLinerManager>();
    }

    protected override void OnDestroy()
    {
        points.Dispose();
    }

    public struct Sortable : System.IComparable<Sortable>
    {
        public float3 position;
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

        var ranges = new NativeList<int>(64, Allocator.TempJob);

        Job
            .WithName("Sort")
            .WithReadOnly(components)
            .WithReadOnly(entities)
            .WithCode(() =>
        {
            for (int i = 0; i < sortables.Length; i++)
            {
                sortables[i] = new Sortable()
                {
                    position = components[i].position,
                    entity = entities[i],
                    prev = components[i].prev,
                    index = components[i].sortIndex
                };
            }

            sortables.Sort();
        })
            .Schedule();

        Dependency.Complete();

        lines.Clear();

        Profiler.BeginSample("Find and form lines");


        {
            int len = 0;
            int start = 0;

            for (int i = sortables.Length - 1; i >= 1; i--)
            {
                if (sortables[i].prev == sortables[i - 1].entity)
                {
                    points[len++] = sortables[i].position;
                }
                else
                {
                    //lines.Form(points, len);

                    ranges.Add(start);
                    ranges.Add(len);
                    start += len;
                    len = 0;
                }

                if (len >= MAX_POINTS_IN_BUFFER)
                {
                    //lines.Form(points, len);

                    ranges.Add(start);
                    ranges.Add(len);
                    start += len;
                    len = 0;
                }
            }

            ranges.Add(start);
            ranges.Add(len);
            //start += len;
            //len = 0;
            //lines.Form(points, len);
        }

        for (int i = 0; i < ranges.Length; i += 2)
        {
            Debug.Log($"{ranges[i]}:{ranges[i + 1]}");

            var slice = points.GetSubArray(ranges[i], ranges[i + 1]);
            lines.Form(slice, ranges[i + 1]);
        }

        Profiler.EndSample();

        entities.Dispose();
        components.Dispose();
        sortables.Dispose();
    }
}