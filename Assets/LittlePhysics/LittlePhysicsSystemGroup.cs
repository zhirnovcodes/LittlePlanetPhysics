using Unity.Core;
using Unity.Entities;

namespace LittlePhysics
{
    // Singleton component to hold the sub-stepped time
    public struct LittlePhysicsTimeComponent : IComponentData
    {
        public int TimeScale;
        public float DeltaTime;
        public double ElapsedTime;
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class LittlePhysicsSystemGroup : ComponentSystemGroup
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            // Create the singleton entity once
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new LittlePhysicsTimeComponent
            {
                TimeScale = 1
            });
        }

        protected override void OnUpdate()
        {
            var worldTime = World.Time;
            var physicsTime = SystemAPI.GetSingleton<LittlePhysicsTimeComponent>();

            float worldDeltaTime = worldTime.DeltaTime;
            double timeElapsed = physicsTime.ElapsedTime;
            int timeScale = physicsTime.TimeScale;

            SystemAPI.SetSingleton(new LittlePhysicsTimeComponent
            {
                ElapsedTime = timeElapsed,
                DeltaTime = 0,
                TimeScale = 0
            });

            for (int i = 0; i < timeScale; i++)
            {
                timeElapsed += worldDeltaTime;

                SystemAPI.SetSingleton(new LittlePhysicsTimeComponent
                {
                    ElapsedTime = timeElapsed,
                    DeltaTime = worldDeltaTime,
                    TimeScale = timeScale
                });

                base.OnUpdate();
            }
        }
    }


    [UpdateInGroup(typeof(LittlePhysicsSystemGroup), OrderFirst = true)]
    public partial class LittlePhysicsInternalSystemGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(LittlePhysicsSystemGroup), OrderLast = true)]
    public partial class LittlePhysicsUserSystemGroup : ComponentSystemGroup
    {
    }
}