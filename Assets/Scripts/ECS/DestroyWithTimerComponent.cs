using Unity.Entities;

namespace LittlePhysics
{
    public struct DestroyWithTimerComponent : IComponentData
    {
        public float DestroyTime;
        public float TimeElapsed;
    }
}
