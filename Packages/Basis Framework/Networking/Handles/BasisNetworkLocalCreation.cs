using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Networking.Factorys;
using Basis.Scripts.Networking.NetworkedAvatar;
using Basis.Scripts.Networking.NetworkedPlayer;
using DarkRift;
using DarkRift.Server.Plugins.Commands;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using static SerializableDarkRift;
namespace Basis.Scripts.Networking
{
    public static class BasisNetworkLocalCreation
    {
        public static async Task HandleAuthSuccess(Transform Parent)
        {
            BasisNetworkedPlayer NetworkedPlayer = await BasisPlayerFactoryNetworked.CreateNetworkedPlayer(new InstantiationParameters(Parent.position, Parent.rotation, Parent));
            ushort playerID = BasisNetworkManagement.Instance.Client.ID;
            BasisLocalPlayer BasisLocalPlayer = BasisLocalPlayer.Instance;
            NetworkedPlayer.ReInitialize(BasisLocalPlayer, playerID);
            if (BasisNetworkManagement.AddPlayer(NetworkedPlayer))
            {

                Debug.Log("added local Player " + playerID);
            }
            else
            {
                Debug.LogError("Cant add " + playerID);
            }
            using (DarkRiftWriter writer = DarkRiftWriter.Create())
            {
                byte[] Information = BasisBundleConversionNetwork.ConvertBasisLoadableBundleToBytes(BasisLocalPlayer.AvatarMetaData);
                BasisNetworkAvatarCompressor.Compress(NetworkedPlayer.NetworkSend, BasisLocalPlayer.Avatar.Animator);

                NetworkedPlayer.NetworkSend.ReSizeAndErrorIfNeeded();
                ReadyMessage ReadyMessage = BasisNetworkManagement.Instance.readyMessage;
             //  Debug.Log("Ready Local LASM Size is " + NetworkedPlayer.NetworkSend.LASM.array.Length);
                ReadyMessage.localAvatarSyncMessage = NetworkedPlayer.NetworkSend.LASM;
                ReadyMessage.clientAvatarChangeMessage = new ClientAvatarChangeMessage
                {
                    byteArray = Information,
                    loadMode = BasisLocalPlayer.AvatarLoadMode,
                };
                ReadyMessage.playerMetaDataMessage = new PlayerMetaDataMessage
                {
                    playerUUID = BasisLocalPlayer.UUID,
                    playerDisplayName = BasisLocalPlayer.DisplayName
                };
                ValidateData(ref ReadyMessage);
                writer.Write(ReadyMessage);
              //  Debug.Log("Ready Local UUID " + ReadyMessage.playerMetaDataMessage.playerUUID);
                BasisNetworkManagement.Instance.readyMessage = ReadyMessage;
                using (Message ReadyMessages = Message.Create(BasisTags.ReadyStateTag, writer))
                {
                    BasisNetworkManagement.Instance.Client.SendMessage(ReadyMessages, BasisNetworking.EventsChannel, DeliveryMethod.ReliableOrdered);
                    BasisNetworkManagement.OnLocalPlayerJoined?.Invoke(NetworkedPlayer, BasisLocalPlayer);
                    BasisNetworkManagement.HasSentOnLocalPlayerJoin = true;
                }
            }
        }
        public static void ValidateData(ref ReadyMessage ReadyMessage)
        {
            if (string.IsNullOrEmpty(ReadyMessage.playerMetaDataMessage.playerDisplayName))
            {
                Debug.LogError("Missing playerMetaDataMessage Player Name!");
            }
            if (string.IsNullOrEmpty(ReadyMessage.playerMetaDataMessage.playerUUID))
            {
                Debug.LogError("Missing playerMetaDataMessage playerUUID Name!");
            }
            if (ReadyMessage.clientAvatarChangeMessage.byteArray == null || ReadyMessage.clientAvatarChangeMessage.byteArray.Length == 0)
            {
                Debug.LogError("Missing clientAvatarChangeMessage byteArray!");
            }
            if (ReadyMessage.localAvatarSyncMessage.array == null || ReadyMessage.localAvatarSyncMessage.array.Length == 0)
            {
                Debug.LogError("Missing localAvatarSyncMessage array!");
            }
        }
    }
}