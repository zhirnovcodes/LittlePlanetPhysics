using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace LittlePhysics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    public partial struct ImportPhysicsDataSystem : ISystem
    {
        [NoAlias] public NativeArray<uint> BodyInLodCount;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSingleton>();
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<PhysicsStepComponent>();
            BodyInLodCount = new NativeArray<uint>(2, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (BodyInLodCount.IsCreated)
                BodyInLodCount.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();

            if (!singleton.BodiesList.IsCreated)
                return;

            var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
            var lodSettings = settings.BlobRef.Value.LodData;
            var lodMax = lodSettings.MaxEntityCount;
            var rootMax = settings.BlobRef.Value.MaxEntitiesCount;
            var maxEntitiesCount = lodMax > 0 ? lodMax : rootMax;
            var step = SystemAPI.GetSingleton<PhysicsStepComponent>();

            var combinedDep = JobHandle.CombineDependencies(state.Dependency, singleton.PhysicsJobHandle);

            var lodJob = new ComputePhysicsLodJob
            {
                Camera = step.Camera,
                DistanceRange = lodSettings.DistanceRange
            }.ScheduleParallel(combinedDep);

            var clearJob = new ClearJob
            {
                BodiesList = singleton.BodiesList,
                BodyInLodCount = BodyInLodCount,
            }.Schedule(lodJob);

            var velocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocityComponent>(true);

            var importJob = new ImportPhysicsDataJob
            {
                BodiesList = singleton.BodiesList,
                BodyInLodCount = BodyInLodCount,
                MaxEntitiesCount = maxEntitiesCount,
                MaxBodiesPerLod = maxEntitiesCount,
                DeltaTime = SystemAPI.Time.DeltaTime,
                PhysicsVelocities = singleton.PhysicsVelocities,
                VelocityLookup = velocityLookup,
            }.Schedule(clearJob);

            state.Dependency = importJob;

            singleton.PhysicsJobHandle = state.Dependency;
            SystemAPI.SetSingleton(singleton);
        }
    }

    [BurstCompile]
    public partial struct ClearJob : IJob
    {
        public NativeList<PhysicsBodyData> BodiesList;
        public NativeArray<uint> BodyInLodCount;

        public void Execute()
        {
            BodiesList.Clear();
            for (int i = 0; i < BodyInLodCount.Length; i++)
                BodyInLodCount[i] = 0;
        }
    }

    [BurstCompile]
    public partial struct ComputePhysicsLodJob : IJobEntity
    {
        public CameraData Camera;
        public float2 DistanceRange;

        public void Execute(in LocalTransform transform, in PhysicsBodyComponent body, ref PhysicsBodyUpdateComponent tag)
        {
            float3 worldPos = transform.Position + body.LocalPosition;
            float dist = math.distance(Camera.CameraPosition, worldPos);
            bool inDist = dist >= DistanceRange.x && dist <= DistanceRange.y;

            float4 clip = math.mul(Camera.WorldToClipMatrix, new float4(worldPos, 1f));
            float invW = math.rcp(clip.w);
            float3 ndc = clip.xyz * invW;
            bool inVp = ndc.x >= -1f && ndc.x <= 1f && ndc.y >= -1f && ndc.y <= 1f && ndc.z >= -1f && ndc.z <= 1f;

            tag.LodIndex = (inDist && inVp) ? 1 : 0;
        }
    }

    [BurstCompile]
    public partial struct ImportPhysicsDataJob : IJobEntity
    {
        public NativeList<PhysicsBodyData> BodiesList;
        public NativeArray<uint> BodyInLodCount;
        public int MaxEntitiesCount;
        public int MaxBodiesPerLod;
        public float DeltaTime;
        public NativeArray<PhysicsVelocityData> PhysicsVelocities;
        [ReadOnly] public ComponentLookup<PhysicsVelocityComponent> VelocityLookup;

        public void Execute(Entity entity, in LocalTransform transform, in PhysicsBodyComponent body, ref PhysicsBodyUpdateComponent tag)
        {

            if (BodiesList.Length >= MaxEntitiesCount)
            {
                return;
            }

            int lod = tag.LodIndex;

            if (BodyInLodCount[lod] >= (uint)MaxBodiesPerLod)
            {
                tag.IsEnabled = false;
                return;
            }

            bool shouldUpdateMap = false;

            switch (tag.Type)
            {
                case UpdateType.EveryFrame:
                    shouldUpdateMap = true;
                    break;
                case UpdateType.Once:
                    if (tag.WasUpdated == false)
                    {
                        tag.WasUpdated = true;
                        shouldUpdateMap = true;
                    }
                    break;
                case UpdateType.WithInterval:
                    if (tag.WasUpdated)
                    {
                        shouldUpdateMap = (int)math.floor(tag.TimeElapsed / tag.Interval) != (int)math.floor((tag.TimeElapsed - DeltaTime) / tag.Interval);
                    }
                    else
                    {
                        tag.WasUpdated = true;
                        shouldUpdateMap = true;
                    }

                    tag.TimeElapsed += DeltaTime;
                    break;
            }

            tag.IsEnabled = true;
            BodyInLodCount[lod]++;
            int index = BodiesList.Length;
            var bodyData = body.ToBodyData(entity, transform, tag.LodIndex, shouldUpdateMap);

            tag.Index = index;
            BodiesList.Add(bodyData);

            PhysicsVelocityData velocityData = default;

            if (VelocityLookup.TryGetComponent(entity, out var velComp))
            {
                velocityData = velComp.ToVelocityData();
            }
            
            PhysicsVelocities[index] = velocityData;
        }
    }
}
