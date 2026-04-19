using Unity.Entities;
using UnityEngine.InputSystem;

namespace LittlePhysics
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LittlePhysicsTimeScaleSystem : SystemBase
    {
        private Key IncreaseKey = Key.NumpadPlus;
        private Key DecreaseKey = Key.NumpadMinus;
        private Key PauseKey = Key.Numpad0;

        private bool IsPaused = false;
        private int LastValue;

        protected override void OnCreate()
        {
            RequireForUpdate<LittlePhysicsTimeComponent>();
        }

        protected override void OnUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            ref var timeComp = ref SystemAPI.GetSingletonRW<LittlePhysicsTimeComponent>().ValueRW;

            if (keyboard[IncreaseKey].wasPressedThisFrame)
            {
                timeComp.TimeScale = timeComp.TimeScale switch
                {
                    1 => 2,
                    2 => 4,
                    _ => 4
                };
            }
            else if (keyboard[DecreaseKey].wasPressedThisFrame)
            {
                timeComp.TimeScale = timeComp.TimeScale switch
                {
                    4 => 2,
                    2 => 1,
                    _ => 1
                };
            }
            else if (keyboard[PauseKey].wasPressedThisFrame)
            {
                IsPaused = !IsPaused;

                if (IsPaused)
                {
                    LastValue = timeComp.TimeScale;
                    timeComp.TimeScale = 0;
                }
                else
                {
                    timeComp.TimeScale = LastValue;
                }
            }
        }
    }
}