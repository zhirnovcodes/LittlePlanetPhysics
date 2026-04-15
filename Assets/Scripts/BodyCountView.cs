using LittlePhysics;
using TMPro;
using Unity.Entities;
using UnityEngine;

public sealed class BodyCountView : MonoBehaviour
{
    public TMP_Text Text;

    private void Update()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return;

        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BodiesCountComponent>());

        if (query.IsEmpty)
        {
            query.Dispose();
            return;
        }

        var singleton = query.GetSingleton<BodiesCountComponent>();
        query.Dispose();

        Text.text = singleton.Count.ToString();
    }
}
