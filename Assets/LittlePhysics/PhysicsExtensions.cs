namespace LittlePhysics
{
    public static class PhysicsExtensions
    {
        public static bool IsColliding(this PhysicsSettingsComponent physicsSettings, int layer1, int layer2)
        {
            var settingsRef = physicsSettings.BlobRef;
            ref var settings = ref settingsRef.Value;

            if (layer1 < 0 || layer1 >= 32 || layer2 < 0 || layer2 >= 32)
                return false;

            int layerMask = settings.LayersMaps[layer1];
            bool isLayersMatch = (layerMask & (1 << layer2)) != 0;
            return isLayersMatch;
        }
    }
}
