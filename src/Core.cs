using Vintagestory.API.Common;

namespace TemporalMirror
{
    public class Core : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("ItemMirror", typeof(ItemMirror));

            Config.Current = api.LoadOrCreateConfig<Config>(Constants.MOD_ID);
        }
    }
}