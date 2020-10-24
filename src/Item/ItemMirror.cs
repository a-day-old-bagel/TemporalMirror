using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

public class ItemMirror : Item
{
    protected const int secondsNeed = 5;
    protected SimpleParticleProperties particles = new SimpleParticleProperties(
        1,                                      // min quantity
        1,                                      // add quantity
        ColorUtil.WhiteAhsl,                    // color
        new Vec3d(),                            // min pos
        new Vec3d(),                            // add pos
        new Vec3f(-0.25f, 0.1f, -0.25f),        // min velocity
        new Vec3f(0.25f, 0.1f, 0.25f),          // add velocity
        0.2f,                                   // life length
        0.075f,                                 // gravity effect
        0.25f,                                  // min size
        0.25f,                                  // max size
        EnumParticleModel.Cube                  // model
    );
    protected ILoadedSound sound;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (slot.Itemstack.Item.Variant["type"] == "frame")
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }
        if (slot.Itemstack.Item.Variant["type"] == "magic")
        {
            if (blockSel != null && byEntity.Controls.Sneak)
            {
                BlockPos pos = blockSel.Position.AddCopy(1, 0, 1);
                slot.Itemstack.Attributes.SetInt("point.x", pos.X);
                slot.Itemstack.Attributes.SetInt("point.y", pos.Y);
                slot.Itemstack.Attributes.SetInt("point.z", pos.Z);

                SendMessage("Return point saved at " + HumanCoord(pos), byEntity);
                handling = EnumHandHandling.Handled;
                return;
            }
            if (!slot.Itemstack.Attributes.HasAttribute("point.x")) return;
        }

        if (byEntity.World is IClientWorldAccessor)
        {
            IClientWorldAccessor world = byEntity.World as IClientWorldAccessor;
            sound = world.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("teleportermod:sounds/teleport.ogg"),
                ShouldLoop = false,
                Position = byEntity.Pos.XYZ.ToVec3f(),
                DisposeOnFinish = true,
                Volume = 1f,
                Pitch = 0.7f
            });
            sound?.Start();
        }

        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (secondsUsed >= secondsNeed) return false;

        if (api.Side == EnumAppSide.Client)
        {
            ModelTransform tf = new ModelTransform();
            tf.EnsureDefaultValues();

            tf.Translation.Set(-secondsUsed * 0.05f, secondsUsed * 0.025f, secondsUsed * 0.05f);
            byEntity.Controls.UsingHeldItemTransformAfter = tf;

            if (secondsUsed > 0.5)
            {
                Vec3d pos =
                   byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0)
                    .Ahead(0.5, byEntity.Pos.Pitch, byEntity.Pos.Yaw);
                ;

                Random rand = new Random();
                particles.Color = GetRandomColor(api as ICoreClientAPI, slot.Itemstack);

                particles.MinPos = pos.AddCopy(-0.05, -0.05, -0.05);
                particles.AddPos.Set(0.1, 0.1, 0.1);
                particles.MinSize = 0.01f;
                particles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.SINUS, 1);
                byEntity.World.SpawnParticles(particles);
            }
        }
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        sound?.Stop();
        if (secondsUsed >= secondsNeed)
        {
            if (slot.Itemstack.Item.Variant["type"] == "magic")
            {
                BlockPos tpPos = new BlockPos().Set(
                    slot.Itemstack.Attributes.GetInt("point.x"),
                    slot.Itemstack.Attributes.GetInt("point.y") + 1,
                    slot.Itemstack.Attributes.GetInt("point.z")
                );
                api.World.Logger.Notification("Teleport to " + HumanCoord(tpPos));
                SendMessage("Teleport to " + HumanCoord(tpPos), byEntity);
                byEntity.TeleportTo(tpPos.AddCopy(0, 1, 0));

                // TODO: Need check teleportation complete
                if ((byEntity as EntityPlayer)?.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);
                }
            }
            else
            {
                //TODO: Wormhole, open gui and select player
            }
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        if (inSlot.Itemstack.Item.Variant["type"] == "magic")
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "teleportermod:heldhelp-teleport",
                    MouseButton = EnumMouseButton.Right
                },
                new WorldInteraction()
                {
                    ActionLangCode = "teleportermod:heldhelp-savepoint",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak"
                }
            };
        }
        else if (inSlot.Itemstack.Item.Variant["type"] == "wormhole")
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "teleportermod:heldhelp-teleport-to-player",
                    MouseButton = EnumMouseButton.Right
                },
            };
        }
        else return null;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        if (inSlot.Itemstack.Attributes.HasAttribute("point.x"))
        {
            BlockPos tpPos = HumanCoord(new BlockPos().Set(
                    inSlot.Itemstack.Attributes.GetInt("point.x"),
                    inSlot.Itemstack.Attributes.GetInt("point.y") + 1,
                    inSlot.Itemstack.Attributes.GetInt("point.z")
            ));
            dsc.AppendLine("Saved point: " + tpPos.X + ", " + tpPos.Y + ", " + tpPos.Z);
        }
    }

    protected BlockPos HumanCoord(BlockPos trueCoord)
    {
        int x = (int)(trueCoord.X - api.World.DefaultSpawnPosition.XYZ.Z);
        int y = trueCoord.Y;
        int z = (int)(trueCoord.Z - api.World.DefaultSpawnPosition.XYZ.Z);
        return new BlockPos(x, y, z);
    }

    protected void SendMessage(string msg, EntityAgent byEntity)
    {
        IPlayer byPlayer = api.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
        if (api.Side == EnumAppSide.Server)
        {
            IServerPlayer sp = byPlayer as IServerPlayer;
            sp.SendMessage(GlobalConstants.InfoLogChatGroup, msg, EnumChatType.Notification);
        }
        else
        {
            //IClientPlayer cp = byPlayer as IClientPlayer;
            //cp.ShowChatNotification(msg);
        }
    }

}