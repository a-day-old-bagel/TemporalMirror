using Vintagestory.API.Common;

namespace TeleporterMod
{
    public class TeleporterMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("ItemMirror", typeof(ItemMirror));
        }
    }
}