using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Tworzenie niezależnych instancji prefabu areny (G6+, trening wieloarenowy).</summary>
    public static class ArenaFactory
    {
        public const float DefaultSeparationMeters = 100f;

        public static ArenaController InstantiateArena(
            GameObject arenaPrefab,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Transform parent = null)
        {
            if (arenaPrefab == null)
                return null;

            var instance = Object.Instantiate(arenaPrefab, worldPosition, worldRotation, parent);
            var controller = instance.GetComponent<ArenaController>();
            controller?.RefreshPlayerRegistry();
            return controller;
        }
    }
}
