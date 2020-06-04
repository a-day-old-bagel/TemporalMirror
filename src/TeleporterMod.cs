using System;
using Vintagestory.API.Common;

public class TeleporterMod : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
          
        string[] items = {
            "ItemMirror"
        };

        foreach (string e in items)
        {
            api.RegisterItemClass(e, Type.GetType(e));
        }
    }
}