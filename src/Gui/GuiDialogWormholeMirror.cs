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
            : base(Lang.Get(Constants.MOD_ID + ":dlg-wormhole"), capi)
        {
            IsDuplicate = capi.OpenedGuis.FirstOrDefault((object dlg) => (dlg as GuiDialogGeneric)?.DialogTitle == Constants.MOD_ID + ":dlg-wormhole") != null;
        }

        protected void CloseIconPressed() => TryClose();

        public override bool TryOpen()
        {
            if (IsDuplicate)
            {
                return false;
            }

            SetupDialog();
            return base.TryOpen();
        }

        private void OnNewScrollbarValue(float value)
        {
            ElementBounds bounds = SingleComposer.GetContainer("stacklist").Bounds;

            bounds.fixedY = 3 - value;
            bounds.CalcWorldBounds();
        }

        private void SetupDialog()
        {
            ElementBounds[] buttons = new ElementBounds[Math.Max(capi.World.AllOnlinePlayers.Count() - 1, 1)];

            buttons[0] = ElementBounds.Fixed(0, 0, 300, 40);
            for (int i = 1; i < buttons.Length; i++)
            {
                buttons[i] = buttons[i - 1].BelowCopy(0, 1);
            }

            ElementBounds listBounds = ElementBounds.Fixed(0, 0, 302, 400).WithFixedPadding(1);
            listBounds.BothSizing = ElementSizing.Fixed;

            ElementBounds clipBounds = listBounds.ForkBoundingParent();
            ElementBounds insetBounds = listBounds.FlatCopy().FixedGrow(6).WithFixedOffset(-3, -3);

            ElementBounds scrollbarBounds = ElementStdBounds.VerticalScrollbar(insetBounds);


            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithFixedOffset(0, GuiStyle.TitleBarHeight);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(insetBounds, scrollbarBounds);


            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog;


            SingleComposer = capi.Gui
                .CreateCompo("mirror-wormhole-dialog", dialogBounds)
                .AddDialogTitleBar(DialogTitle, CloseIconPressed)
                .AddDialogBG(bgBounds, false)
                .BeginChildElements(bgBounds)
            ;

            if (capi.World.AllOnlinePlayers.Count() == 1)
            {
                SingleComposer
                        .AddStaticText(
                        Lang.Get("No available players"),
                        CairoFont.WhiteSmallText(),
                        buttons[0])
                    .EndChildElements()
                    .Compose()
                ;
                return;
            }

            SingleComposer
                    .BeginClip(clipBounds)
                        .AddInset(insetBounds, 3)
                        .AddContainer(listBounds, "stacklist")
                    .EndClip()
                    .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
                .EndChildElements()
            ;

            var stacklist = SingleComposer.GetContainer("stacklist");

            var otherPlayers = capi.World.AllOnlinePlayers.Where((IPlayer op) => op != capi.World.Player);
            for (int i = 0; i < buttons.Length; i++)
            {
                var tp = otherPlayers.ElementAt(i);

                bool playerLowStability = capi.World.Player?.Entity?.GetBehavior<EntityBehaviorTemporalStabilityAffected>()?.OwnStability < 0.2;
                bool nowStormActive = capi.ModLoader.GetModSystem<SystemTemporalStability>().StormData.nowStormActive;

                var font = CairoFont.WhiteSmallText();

                stacklist.Add(new GuiElementTextButton(capi,
                    (nowStormActive || playerLowStability) ? tp.PlayerName.Shuffle() : tp.PlayerName,
                    font,
                    CairoFont.WhiteSmallText(),
                    () => OnClickItem(tp),
                    buttons[i],
                    EnumButtonStyle.Normal
                ));
            }

            SingleComposer.GetScrollbar("scrollbar").SetHeights(
                (float)Math.Min(listBounds.fixedHeight, (buttons.Last().fixedHeight + buttons.Last().fixedY)),
                (float)(buttons.Last().fixedHeight + buttons.Last().fixedY)
            );
            //SingleComposer.GetScrollbar("scrollbar").ScrollToBottom();
            //SingleComposer.GetScrollbar("scrollbar").CurrentYPosition = 0;
            SingleComposer.Compose();
        }

        private bool OnClickItem(IPlayer toPlayer)
        {
            if (toPlayer.Entity == null)
            {
                TryClose();
                return false;
            }

            capi.World.Player.Entity?.WatchedAttributes?.SetString("playerUID", toPlayer.PlayerUID);

            double curr = capi.World.Player?.Entity?.WatchedAttributes.GetDouble("temporalStability") ?? 1;
            capi.World.Player?.Entity?.WatchedAttributes.SetDouble("temporalStability", Math.Max(0, (double)curr - 0.1));

            TryClose();

            return true;
        }
    }
}