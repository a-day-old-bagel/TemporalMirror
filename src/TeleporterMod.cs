using System;
using Vintagestory.API.Common;
//[assembly: ModInfo("TemporalMirror")]
public class TeleporterMod : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterItemClass("ItemMirror", Type.GetType("ItemMirror"));
    }
}