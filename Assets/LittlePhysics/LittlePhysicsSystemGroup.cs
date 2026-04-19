using Unity.Core;
using Unity.Entities;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial class LittlePhysicsSystemGroup : ComponentSystemGroup
{
    public int N = 1; // 1, 2, or 4

    protected override void OnUpdate()
    {
        var originalTime = World.Time;
        float alteredDt = originalTime.DeltaTime / N;

        for (int i = 0; i < N; i++)
        {
            World.SetTime(new TimeData(
                originalTime.ElapsedTime + alteredDt * i,
                alteredDt
            ));
            base.OnUpdate();
        }

        World.SetTime(originalTime); // clean restore
    }
}