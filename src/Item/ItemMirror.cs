using System;
using System.Text;
using SharedUtils;
using SharedUtils.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace TemporalMirror
{
    public class ItemMirror : Item
    {
        readonly SimpleParticleProperties particles = new SimpleParticleProperties()
        {
            MinQuantity = 1,
            MinPos = new Vec3d(),
            AddPos = new Vec3d(0.1, 0.1, 0.1),
            MinVelocity = new Vec3f(-0.25f, 0.1f, -0.25f),
            AddVelocity = new Vec3f(0.5f, 0.2f, 0.5f),
            LifeLength = 0.2f,
            GravityEffect = 0.9f,
            MinSize = 0.1f,
            MaxSize = 0.1f,
            ParticleModel = EnumParticleModel.Cube
        };

        ILoadedSound sound;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            string mirrorType = slot.Itemstack.Item.Variant["type"];

            if (mirrorType == "frame") return;

            if (mirrorType == "magic")
            {
                slot.Itemstack.Attributes.SetBool("savePointMode", false);

                if (blockSel != null && byEntity.Controls.Sneak)
                {
                    slot.Itemstack.Attributes.SetInt("x", blockSel.Position.X);
                    slot.Itemstack.Attributes.SetInt("y", blockSel.Position.Y);
                    slot.Itemstack.Attributes.SetInt("z", blockSel.Position.Z);

                    handling = EnumHandHandling.Handled;
                    slot.Itemstack.Attributes.SetBool("savePointMode", true);
                    return;
                }

                if (slot.Itemstack.Collectible.Durability <= 1 || !slot.Itemstack.Attributes.HasAttribute("x"))
                {
                    return;
                }

                if (byEntity.World is IClientWorldAccessor world)
                {
                    sound = world.LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation(ConstantsCore.ModId, "sounds/teleport.ogg"),
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
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (slot.Itemstack.Attributes.GetBool("savePointMode", false)) return false;

            if (api.Side == EnumAppSide.Client)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float trans = Math.Min(secondsUsed, Constants.USE_SECONDS);
                tf.Translation.Set(-trans * 0.05f, trans * 0.025f, trans * 0.05f);
                byEntity.Controls.UsingHeldItemTransformAfter = tf;

                if (secondsUsed > 0.5)
                {
                    Vec3d pos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0)
                        .Ahead(0.5, byEntity.Pos.Pitch, byEntity.Pos.Yaw);

                    particles.Color = GetRandomColor(api as ICoreClientAPI, slot.Itemstack);
                    particles.MinPos.Set(pos).Add(-0.05, -0.05, -0.05);
                    particles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.SINUS, 0.5f);

                    byEntity.World.SpawnParticles(particles);
                }
            }

            if (secondsUsed > Constants.USE_SECONDS)
            {
                if (slot.Itemstack.Item.Variant["type"] == "magic")
                {
                    slot.Itemstack.Attributes.SetVec3i("beforeTpPos", byEntity.Pos.XYZInt.AddCopy(0, 0, 0));

                    if (api.Side == EnumAppSide.Server && !byEntity.Teleporting && byEntity is EntityPlayer entityPlayer)
                    {
                        TeleportToPoint(slot, entityPlayer);
                        return false;
                    }
                }

                else if (slot.Itemstack.Item.Variant["type"] == "wormhole")
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        GuiDialogWormholeMirror dlg = new GuiDialogWormholeMirror(api as ICoreClientAPI);
                        dlg.TryOpen();
                    }
                }
            }

            return true;
        }

        private void TeleportToPoint(ItemSlot slot, EntityPlayer entityPlayer)
        {
            Vec3i currentPoint = entityPlayer.Pos.XYZInt;
            Vec3i endPoint = new Vec3i(
                    slot.Itemstack.Attributes.GetInt("x"),
                    slot.Itemstack.Attributes.GetInt("y"),
                    slot.Itemstack.Attributes.GetInt("z"));

            int maxDistance = slot.Itemstack.Collectible.Durability - 1;
            int dist = (int)currentPoint.DistanceTo(endPoint);
            bool toEnd = (dist <= maxDistance);

            if (!toEnd)
            {
                int nX = currentPoint.X - endPoint.X;
                int nZ = currentPoint.Z - endPoint.Z;
                int k = maxDistance / dist;

                endPoint = new Vec3i(
                    currentPoint.X + nX / k,
                    api.World.BlockAccessor.MapSizeY,
                    currentPoint.Z + nZ / k
                );
            }

            var sapi = api as ICoreServerAPI;
            int chunkSize = sapi.WorldManager.ChunkSize;

            entityPlayer.TeleportToDouble(endPoint.X + 0.5, endPoint.Y + 1, endPoint.Z + 0.5);
            sapi.WorldManager.LoadChunkColumnPriority(endPoint.X / chunkSize, endPoint.Z / chunkSize, new ChunkLoadOptions()
            {
                OnLoaded = () =>
                {
                    if (!toEnd)
                    {
                        endPoint.Y = sapi.WorldManager.GetSurfacePosY(endPoint.X, endPoint.Z) ?? endPoint.Y;
                    }

                    string playerName = entityPlayer.Player.PlayerName;
                    api.World.Logger.ModNotification("Teleporting {0} to {1}", playerName, endPoint);

                    entityPlayer.TeleportToDouble(endPoint.X + 0.5, endPoint.Y + 1, endPoint.Z + 0.5, () =>
                    {
                        api.World.Logger.ModNotification("Teleported {0} to {1}", playerName, endPoint);

                        if (entityPlayer.Player.WorldData.CurrentGameMode != EnumGameMode.Creative || true)
                        {
                            bool savePointMode = slot.Itemstack.Attributes.GetBool("savePointMode", false);
                            Vec3i beforeTpPos = slot.Itemstack.Attributes.GetVec3i("beforeTpPos");

                            if (!savePointMode && beforeTpPos != null)
                            {
                                int cost = (int)beforeTpPos.DistanceTo(entityPlayer.Pos.XYZInt);
                                slot.Itemstack.Collectible.DamageItem(entityPlayer.World, entityPlayer, slot, cost);
                            }
                        }
                    });
                }
            });
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            bool flag = base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);

            if (flag)
            {
                sound?.Stop();
            }

            return flag;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            sound?.Stop();

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (inSlot.Itemstack.Item.Variant["type"] == "magic")
            {
                return new WorldInteraction[]
                {
                new WorldInteraction()
                {
                    ActionLangCode = ConstantsCore.ModId + ":heldhelp-teleport",
                    MouseButton = EnumMouseButton.Right
                },
                new WorldInteraction()
                {
                    ActionLangCode = ConstantsCore.ModId + ":heldhelp-savepoint",
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
                    ActionLangCode = ConstantsCore.ModId + ":heldhelp-teleport-to-player",
                    MouseButton = EnumMouseButton.Right
                },
                };
            }
            else return null;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.Attributes.HasAttribute("x"))
            {
                BlockPos tpPos = new BlockPos().Set(
                        inSlot.Itemstack.Attributes.GetInt("x"),
                        inSlot.Itemstack.Attributes.GetInt("y") + 1,
                        inSlot.Itemstack.Attributes.GetInt("z")
                );
                BlockPos mapTpPos = MapPos(tpPos);
                dsc.AppendLine(Lang.Get("Saved point: {0}, {1}, {2}", mapTpPos.X, mapTpPos.Y, mapTpPos.Z));
                dsc.AppendLine(Lang.Get("Distance: {0} m", (int)(api as ICoreClientAPI)?.World?.Player?.Entity?.Pos.AsBlockPos.DistanceTo(tpPos)));
            }
        }

        private BlockPos MapPos(BlockPos pos)
        {
            int x = pos.X - api.World.DefaultSpawnPosition.XYZInt.X;
            int z = pos.Z - api.World.DefaultSpawnPosition.XYZInt.Z;
            return new BlockPos(x, pos.Y + 1, z);
        }
    }
}