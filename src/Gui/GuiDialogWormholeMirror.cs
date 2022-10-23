using SharedUtils;
using SharedUtils.Extensions;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace TemporalMirror
{
    public class GuiDialogWormholeMirror : GuiDialogGeneric
    {
        public bool IsDuplicate { get; }
        public override bool PrefersUngrabbedMouse => false;
        public override bool UnregisterOnClose => true;

        public GuiDialogWormholeMirror(ICoreClientAPI capi)
            : base(Lang.Get(ConstantsCore.ModId + ":dlg-wormhole-title"), capi)
        {
            IsDuplicate = capi.OpenedGuis.FirstOrDefault((object dlg)
                => (dlg as GuiDialogGeneric)?.DialogTitle == DialogTitle) != null;
        }

        public override bool TryOpen()
        {
            if (IsDuplicate)
            {
                return false;
            }

            SetupDialog();
            return base.TryOpen();
        }

        private void SetupDialog()
        {
            ElementBounds[] buttons = new ElementBounds[Math.Max(capi.World.AllOnlinePlayers.Length - 1, 1)];

            buttons[0] = ElementBounds.Fixed(0, 0, 300, 40);
            for (int i = 1; i < buttons.Length; i++)
            {
                buttons[i] = buttons[i - 1].BelowCopy(0, 1);
            }

            ElementBounds listBounds = ElementBounds.Fixed(0, 0, 302, 400).WithFixedPadding(1);
            listBounds.BothSizing = ElementSizing.Fixed;

            ElementBounds clipBounds = listBounds.ForkBoundingParent();
            ElementBounds insetBounds = listBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = insetBounds
                .CopyOffsetedSibling(insetBounds.fixedWidth + 3.0)
                .WithFixedWidth(GuiElementScrollbar.DefaultScrollbarWidth)
                .WithFixedPadding(GuiElementScrollbar.DeafultScrollbarPadding);


            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithFixedOffset(0, GuiStyle.TitleBarHeight);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, scrollbarBounds);


            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog;


            SingleComposer = capi.Gui
                .CreateCompo(ConstantsCore.ModId + ":dlg-wormhole-title", dialogBounds)
                .AddDialogTitleBar(DialogTitle, () => TryClose())
                .AddDialogBG(bgBounds, false)
                .BeginChildElements(bgBounds);

            if (capi.World.AllOnlinePlayers.Count() == 1)
            {
                SingleComposer
                        .AddStaticText(Lang.Get(ConstantsCore.ModId + ":dlg-wormhole-empty"),
                            CairoFont.WhiteSmallText(),
                            buttons[0])
                    .EndChildElements()
                    .Compose();

                return;
            }

            SingleComposer
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddContainer(listBounds, "stacklist")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                .EndChildElements();

            SetupPlayerList(buttons);

            SingleComposer.Compose();
            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)insetBounds.fixedHeight,
                (float)Math.Max(insetBounds.fixedHeight, listBounds.fixedHeight));
        }

        private void SetupPlayerList(ElementBounds[] buttons)
        {
            var stacklist = SingleComposer.GetContainer("stacklist");
            var otherPlayers = capi.World.AllOnlinePlayers.Where((IPlayer op) => op != capi.World.Player);
            for (int i = 0; i < buttons.Length; i++)
            {
                var targetPlayer = otherPlayers.ElementAt(i);

                bool playerLowStability = capi.World.Player?.Entity?.GetBehavior<EntityBehaviorTemporalStabilityAffected>()?.OwnStability < 0.2;
                bool nowStormActive = capi.ModLoader.GetModSystem<SystemTemporalStability>().StormData.nowStormActive;

                var font = CairoFont.WhiteSmallText();

                stacklist.Add(new GuiElementTextButton(capi,
                    (nowStormActive || playerLowStability) ? targetPlayer.PlayerName.Shuffle() : targetPlayer.PlayerName,
                    font,
                    CairoFont.WhiteSmallText(),
                    () => OnPlayerButtonClick(targetPlayer),
                    buttons[i],
                    EnumButtonStyle.Normal
                ));
            }
        }

        private void OnNewScrollbarValue(float value)
        {
            ElementBounds bounds = SingleComposer.GetContainer("stacklist").Bounds;
            bounds.fixedY = 3 - value;
            bounds.CalcWorldBounds();
        }

        private bool OnPlayerButtonClick(IPlayer targetPlayer)
        {
            var activeSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            if (activeSlot.Itemstack.Collectible is ItemMirror)
            {
                activeSlot.Itemstack.Collectible.DamageItem(capi.World, capi.World.Player.Entity, activeSlot, 1);

                capi.Network
                    .GetChannel(ConstantsCore.ModId + "-wormhole-mirror")
                    .SendPacket(targetPlayer.PlayerUID);
            }

            return TryClose();
        }
    }
}
