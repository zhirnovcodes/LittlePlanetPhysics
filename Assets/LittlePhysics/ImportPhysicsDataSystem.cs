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
        [NoAlias] public NativeReference<uint> BodiesCount;
        [NoAlias] public NativeArray<PhysicsBodyData> BodiesList;
        [NoAlias] public NativeArray<PhysicsVelocityData> PhysicsVelocities;

        private NativeArray<uint> bodiesInLodCount;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsSettingsComponent>();
            state.RequireForUpdate<PhysicsStepComponent>();
            BodiesCount = new NativeReference<uint>(Allocator.Persistent);
            bodiesInLodCount = new NativeArray<uint>(2, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (BodiesCount.IsCreated)
                BodiesCount.Dispose();
            if (bodiesInLodCount.IsCreated)
                bodiesInLodCount.Dispose();
            if (BodiesList.IsCreated)
                BodiesList.Dispose();
            if (PhysicsVelocities.IsCreated)
                PhysicsVelocities.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BodiesList.IsCreated)
            {
                var settings = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
                var capacity = settings.BlobRef.Value.LodData.MaxEntityCount;
                BodiesList = new NativeArray<PhysicsBodyData>(capacity, Allocator.Persistent);
                PhysicsVelocities = new NativeArray<PhysicsVelocityData>(capacity, Allocator.Persistent);
                return;
            }

            if (!SystemAPI.HasSingleton<PhysicsSingleton>())
                return;

            var singleton = SystemAPI.GetSingleton<PhysicsSingleton>();

            var settings2 = SystemAPI.GetSingleton<PhysicsSettingsComponent>();
            var lodSettings = settings2.BlobRef.Value.LodData;
            var lodMax = lodSettings.MaxEntityCount;
            var rootMax = settings2.BlobRef.Value.MaxEntitiesCount;
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
                BodiesCount = BodiesCount,
                BodyInLodCount = bodiesInLodCount,
            }.Schedule(lodJob);

            var velocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocityComponent>(true);

            var importJob = new ImportPhysicsDataJob
            {
                BodiesList = BodiesList,
                BodiesCount = BodiesCount,
                BodyInLodCount = bodiesInLodCount,
                MaxEntitiesCount = maxEntitiesCount,
                MaxBodiesPerLod = maxEntitiesCount,
                DeltaTime = SystemAPI.Time.DeltaTime,
                PhysicsVelocities = PhysicsVelocities,
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
        public NativeReference<uint> BodiesCount;
        public NativeArray<uint> BodyInLodCount;

        public void Execute()
        {
            BodiesCount.Value = 0;
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
        public NativeArray<PhysicsBodyData> BodiesList;
        public NativeReference<uint> BodiesCount;
        public NativeArray<uint> BodyInLodCount;
        public int MaxEntitiesCount;
        public int MaxBodiesPerLod;
        public float DeltaTime;
        public NativeArray<PhysicsVelocityData> PhysicsVelocities;
        [ReadOnly] public ComponentLookup<PhysicsVelocityComponent> VelocityLookup;

        public void Execute(Entity entity, in LocalTransform transform, in PhysicsBodyComponent body, ref PhysicsBodyUpdateComponent tag)
        {
            if (BodiesCount.Value >= (uint)MaxEntitiesCount)
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
            int index = (int)BodiesCount.Value;
            BodiesCount.Value++;
            BodyInLodCount[lod]++;

            var bodyData = body.ToBodyData(entity, transform, tag.LodIndex, shouldUpdateMap);
            tag.Index = index;
            BodiesList[index] = bodyData;

            PhysicsVelocityData velocityData = default;

            if (VelocityLookup.TryGetComponent(entity, out var velComp))
            {
                velocityData = velComp.ToVelocityData();
            }

            PhysicsVelocities[index] = velocityData;
        }
    }
}
