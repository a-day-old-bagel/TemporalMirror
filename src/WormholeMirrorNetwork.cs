using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;

namespace TemporalMirror
{
    public class WormholeMirrorNetwork : ModSystem
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Network
                .RegisterChannel(Mod.Info.ModID + "-wormhole-mirror")
                .RegisterMessageType<string>()
                .SetMessageHandler<string>((player, targetPlayerName) =>
                {
                    IPlayer targetPlayer = player.Entity.World.PlayerByUid(targetPlayerName);
                    if (targetPlayer != null)
                    {
                        player.Entity.TeleportTo(targetPlayer.Entity.Pos.XYZ);
                    }
                    else
                    {
                        Mod.Logger.Error("Player {0} not exists", targetPlayerName);
                    }
                });
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network
                .RegisterChannel(Mod.Info.ModID + "-wormhole-mirror")
                .RegisterMessageType<string>();
        }
    }
}
