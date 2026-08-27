using BepInEx;
using BlockStoryCore;

namespace BlockStoryMod
{
    [BepInPlugin("com.malts.blockstory.unlimitedpets", "UnlimitedPets", "4.0.0")]
    [BepInDependency(Core.Guid)]
    public class UnlimitedPetsPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ModRegistry.Register(new ModInfo
            {
                Name = "Unlimited Pets",
                Description = "Allows summoning of more than 5 pets at once.",
                GetEnabled = () => true,
                SetEnabled = _ => { },
                HasConfig = false,
            });

            Core.Log?.LogInfo("[UnlimitedPets]: Loaded successfully.");
        }

        private void Update()
        {
            PetNames.currentSpawnCount = 0;
        }
    }
}