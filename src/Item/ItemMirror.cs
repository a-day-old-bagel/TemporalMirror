using SharedUtils;
using SharedUtils.Extensions;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace TemporalMirror
{
    public class ItemMirror : Item
    {
        private readonly SimpleParticleProperties _particles = new SimpleParticleProperties()
        {
            MinQuantity = 1,
            MinPos = new Vec3d(-.5, 0, -.5),
            AddPos = new Vec3d(1, 0.2, 1),
            MinVelocity = new Vec3f(-0.25f, 0.1f, -0.25f),
            AddVelocity = new Vec3f(0.5f, 0.2f, 0.5f),
            LifeLength = 0.5f,
            GravityEffect = -0.9f,
            MinSize = 0.1f,
            MaxSize = 0.2f,
            ParticleModel = EnumParticleModel.Quad
        };

        private ILoadedSound _sound;

        public bool IsFrame => Variant["type"] == "frame";
        public bool IsMagic => Variant["type"] == "magic";
        public bool IsWormhole => Variant["type"] == "wormhole";

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (IsFrame)
            {
                return;
            }

            if (IsMagic)
            {
                UpdateToNewPos(slot.Itemstack);

                slot.Itemstack.Attributes.SetBool("savePointMode", false);
                slot.Itemstack.Attributes.SetBool("teleported", false);

                // save point
                if (blockSel != null && byEntity.Controls.Sneak)
                {
                    Vec3i point = blockSel.Position.ToVec3i() + blockSel.Face.Normali;
                    slot.Itemstack.Attributes.SetVec3i("targetPos", point);
                    slot.Itemstack.Attributes.SetBool("savePointMode", true);
                    handling = EnumHandHandling.Handled;
                    return;
                }

                // teleport
                if (slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack) > 1 &&
                    slot.Itemstack.Attributes.HasAttribute("targetPosX"))
                {
                    if (byEntity.World is IClientWorldAccessor world)
                    {
                        _sound = world.LoadSound(new SoundParams()
                        {
                            Location = new AssetLocation(ConstantsCore.ModId, "sounds/teleport.ogg"),
                            ShouldLoop = false,
                            Position = byEntity.Pos.XYZ.ToVec3f(),
                            DisposeOnFinish = true,
                            Volume = 1f,
                            Pitch = 0.7f
                        });
                        _sound?.Start();
                    }

                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            if (IsWormhole)
            {
                if (byEntity.World is IClientWorldAccessor world)
                {
                    _sound = world.LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation(ConstantsCore.ModId, "sounds/teleport.ogg"),
                        ShouldLoop = false,
                        Position = byEntity.Pos.XYZ.ToVec3f(),
                        DisposeOnFinish = true,
                        Volume = 1f,
                        Pitch = 0.7f
                    });
                    _sound?.Start();
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (slot.Itemstack.Attributes.GetBool("savePointMode"))
            {
                return false;
            }

            // visual effects
            if (api.Side == EnumAppSide.Client)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float trans = Math.Min(secondsUsed, Constants.InteractionTime);
                tf.Translation.Set(-trans * 0.05f, trans * 0.025f, trans * 0.05f);
                byEntity.Controls.UsingHeldItemTransformAfter = tf;

                if (secondsUsed > 0.5)
                {
                    _particles.Color = GetRandomColor(api as ICoreClientAPI, slot.Itemstack);
                    _particles.MinPos.Set(byEntity.Pos.XYZ.Add(-.5, 0, -.5));
                    _particles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.SINUS, 0.1f);

                    byEntity.World.SpawnParticles(_particles);
                }
            }

            if (secondsUsed > Constants.InteractionTime)
            {
                if (IsMagic)
                {
                    if (byEntity.Teleporting)
                    {
                        return true;
                    }
                    else
                    {
                        if (slot.Itemstack.Attributes.GetBool("teleported"))
                        {
                            return false;
                        }
                        else
                        {
                            if (api.Side == EnumAppSide.Server && byEntity is EntityPlayer entityPlayer)
                            {
                                TeleportToPoint(slot, entityPlayer);
                                return true;
                            }
                        }
                    }
                }

                if (IsWormhole)
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        GuiDialogWormholeMirror dlg = new GuiDialogWormholeMirror(api as ICoreClientAPI);
                        dlg.TryOpen();
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            if (secondsUsed > Constants.InteractionTime)
            {
                if (IsMagic)
                {
                    return slot.Itemstack.Attributes.GetBool("teleported");
                }
            }

            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            _sound?.Stop();
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        private void TeleportToPoint(ItemSlot slot, EntityPlayer entityPlayer)
        {
            Vec3i startPos = entityPlayer.Pos.XYZInt;
            Vec3i targetPos = slot.Itemstack.Attributes.GetVec3i("targetPos");

            int maxDistance = slot.Itemstack.Collectible.Durability - 1;
            int distance = (int)startPos.DistanceTo(targetPos);
            bool partialTeleport = distance > maxDistance;

            if (partialTeleport)
            {
                float k = (float)maxDistance / distance;
                targetPos = new Vec3i
                {
                    X = startPos.X + (int)((targetPos.X - startPos.X) * k),
                    Y = startPos.Y + (int)((targetPos.Y - startPos.Y) * k),
                    Z = startPos.Z + (int)((targetPos.Z - startPos.Z) * k)
                };
            }

            string playerName = entityPlayer.Player.PlayerName;
            api.World.Logger.ModNotification("Teleporting {0} to {1}", playerName, targetPos);

            // fake teleport for prevent player movement
            entityPlayer.TeleportToDouble(targetPos.X + 0.5, targetPos.Y + 1, targetPos.Z + 0.5);

            var sapi = api as ICoreServerAPI;
            int chunkSize = sapi.WorldManager.ChunkSize;
            int chunkX = targetPos.X / chunkSize;
            int chunkZ = targetPos.Z / chunkSize;

            sapi.WorldManager.LoadChunkColumnPriority(chunkX, chunkZ, new ChunkLoadOptions()
            {
                OnLoaded = () =>
                {
                    api.World.Logger.ModNotification("Teleported {0} to {1}", playerName, targetPos);

                    int cost = (int)startPos.DistanceTo(entityPlayer.Pos.XYZInt);
                    slot.Itemstack.Attributes.SetBool("teleported", true);

                    // up to top surface block for partial teleport
                    if (partialTeleport)
                    {
                        int? surfacePos = sapi.WorldManager.GetSurfacePosY(targetPos.X, targetPos.Z);
                        targetPos.Y = surfacePos ?? targetPos.Y;

                        cost = GetRemainingDurability(slot.Itemstack);

                        entityPlayer.TeleportToDouble(targetPos.X + 0.5, targetPos.Y + 1, targetPos.Z + 0.5, () =>
                        {
                            api.World.Logger.ModNotification("Up {0} to surface {1}", playerName, targetPos);
                        });
                    }

                    DamageItem(entityPlayer.World, entityPlayer, slot, cost);
                }
            });
        }

        public override void DamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int amount = 1)
        {
            // prevent break mirror
            if (amount >= GetRemainingDurability(itemslot.Itemstack))
            {
                amount = GetRemainingDurability(itemslot.Itemstack) - 1;
            }

            base.DamageItem(world, byEntity, itemslot, amount);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (IsMagic)
            {
                return base.GetHeldInteractionHelp(inSlot).Append(new WorldInteraction[]
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
                        HotKeyCode = "shift"
                    }
                });
            }

            if (IsWormhole)
            {
                return base.GetHeldInteractionHelp(inSlot).Append(new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = ConstantsCore.ModId + ":heldhelp-teleport-to-player",
                        MouseButton = EnumMouseButton.Right
                    }
                });
            }

            return base.GetHeldInteractionHelp(inSlot);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (IsMagic)
            {
                UpdateToNewPos(inSlot.Itemstack);

                Vec3i targetPos = inSlot.Itemstack.Attributes.GetVec3i("targetPos", null);
                Vec3i playerPos = (world as IClientWorldAccessor)?.Player?.Entity?.Pos.XYZInt;

                if (targetPos != null && playerPos != null)
                {
                    Vec3i mapPos = ToVisiblePos(targetPos, world);
                    dsc.AppendLine(Lang.Get("Saved point: {0}, {1}, {2}", mapPos.X, mapPos.Y, mapPos.Z));
                    dsc.AppendLine(Lang.Get("Distance: {0} m", (int)playerPos.DistanceTo(targetPos)));
                }
            }
        }

        private static Vec3i ToVisiblePos(Vec3i pos, IWorldAccessor world)
        {
            int x = pos.X - world.DefaultSpawnPosition.XYZInt.X;
            int z = pos.Z - world.DefaultSpawnPosition.XYZInt.Z;
            return new Vec3i(x, pos.Y + 1, z);
        }

        /// <summary> Legacy fix 1.6 -> 1.7 </summary>
        private static void UpdateToNewPos(ItemStack itemstack)
        {
            if (itemstack.Attributes.HasAttribute("x"))
            {
                itemstack.Attributes.SetVec3i("targetPos", new Vec3i()
                {
                    X = itemstack.Attributes.GetInt("x"),
                    Y = itemstack.Attributes.GetInt("y"),
                    Z = itemstack.Attributes.GetInt("z")
                });

                itemstack.Attributes.RemoveAttribute("x");
                itemstack.Attributes.RemoveAttribute("y");
                itemstack.Attributes.RemoveAttribute("z");
            }
        }
    }
}
