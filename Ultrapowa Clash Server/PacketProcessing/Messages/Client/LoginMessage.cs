using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using System.Security.Cryptography;
using UCS.Helpers;
using UCS.Core;
using UCS.Network;
using UCS.Logic;
using UCS.GameFiles;

namespace UCS.PacketProcessing
{
    //Packet 10101
    class LoginMessage : Message
    {
        private long m_vAccountId;
        private string m_vPassToken;
        private int m_vClientMajorVersion;
        private int m_vClientBuild;
        private int m_vClientContentVersion;
        private string m_vResourceSha;
        private string m_vUDID;
        private string m_vOpenUDID;
        private string m_vMacAddress;
        private string m_vDevice;
        private string m_vPreferredDeviceLanguage;
        //unchecked
        private string m_vPhoneId;
        private string m_vGameVersion;
        private string m_vSignature2;
        private string m_vSignature3;
        private string m_vSignature4;
        private uint m_vClientSeed; 

        public LoginMessage(Client client, BinaryReader br) : base (client, br)
        {
        }

        public override void Decode()
        {
            using (var br = new BinaryReader(new MemoryStream(GetData())))
            {
                m_vAccountId = br.ReadInt64WithEndian();
                m_vPassToken = br.ReadScString();
                m_vClientMajorVersion = br.ReadInt32WithEndian();
                m_vClientContentVersion = br.ReadInt32WithEndian();
                m_vClientBuild = br.ReadInt32WithEndian();
                m_vResourceSha = br.ReadScString();
                m_vUDID = br.ReadScString();
                m_vOpenUDID = br.ReadScString();
                m_vMacAddress = br.ReadScString();
                m_vDevice = br.ReadScString();
                br.ReadInt32WithEndian();//00 1E 84 81, readDataReference for m_vPreferredLanguage
                m_vPreferredDeviceLanguage = br.ReadScString();
                //unchecked
                m_vPhoneId = br.ReadScString();
                m_vGameVersion = br.ReadScString();
                br.ReadByte();//01
                br.ReadInt32WithEndian();//00 00 00 00
                m_vSignature2 = br.ReadScString();
                m_vSignature3 = br.ReadScString();
                br.ReadByte();//01
                m_vSignature4 = br.ReadScString();
                m_vClientSeed = br.ReadUInt32WithEndian();
                if(GetMessageVersion() >=7 )//7.156
                {
                    br.ReadByte();
                    br.ReadUInt32WithEndian();
                    br.ReadUInt32WithEndian();
                }
            }
        }

        //R-C5: dump de diagnostico - resolve o 'data' de cada predio da vila no CSV do
        //servidor, para conferir se a ordem do CSV bate com a ordem de ids do jogo.
        private static void DumpBuildingData(Level level)
        {
            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append("[UCS] R-C5 buildings: ");
                for (int ci = 0; ci < 12; ci++)
                {
                    List<GameObject> objs = null;
                    try { objs = level.GameObjectManager.GetGameObjects(ci); }
                    catch { objs = null; }
                    if (objs == null)
                        continue;
                    foreach (GameObject go in objs)
                    {
                        Building b = go as Building;
                        if (b == null)
                            continue;
                        BuildingData bd = b.GetBuildingData();
                        if (bd == null)
                            continue;
                        sb.Append("[" + bd.GetGlobalID() + "=>" + bd.TID + " lvl" + b.GetUpgradeLevel() + "]");
                    }
                }
                Console.WriteLine(sb.ToString());

                //R-C5: dump dos storages de tropa (capacidade, uso e slots).
                System.Text.StringBuilder st2 = new System.Text.StringBuilder();
                st2.Append("[UCS] R-C5 storages: ");
                List<Component> comps = level.GetComponentManager().GetComponents(0);
                for (int i = 0; i < comps.Count; i++)
                {
                    UnitStorageComponent usc = comps[i] as UnitStorageComponent;
                    if (usc == null)
                        continue;
                    Building sb2 = usc.GetParent() as Building;
                    string who = (sb2 != null && sb2.GetBuildingData() != null)
                        ? sb2.GetBuildingData().GetGlobalID() + ":" + sb2.GetBuildingData().TID + " lvl" + sb2.GetUpgradeLevel()
                        : "?";
                    st2.Append("{" + who + " max=" + usc.GetMaxCapacity() +
                        " used=" + usc.GetUsedCapacity() + " slots=" + usc.GetUnitSlotCount() + " [");
                    for (int s = 0; s < usc.GetUnitSlotCount(); s++)
                    {
                        CombatItemData cd = usc.GetUnitType(s);
                        st2.Append((cd != null ? cd.GetName() : "?") + "x" + usc.GetUnitCount(s) + " ");
                    }
                    st2.Append("]} ");
                }
                Console.WriteLine(st2.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("[UCS] R-C5: erro no dump de buildings: " + ex.Message);
            }
        }

        public override void Process(Level level)
        {
            Console.WriteLine("LOGIN: major=" + m_vClientMajorVersion + " content=" + m_vClientContentVersion + " build=" + m_vClientBuild + " gameVersion=" + m_vGameVersion + " accountId=" + m_vAccountId);
            if (Convert.ToBoolean(ConfigurationManager.AppSettings["maintenanceMode"]))
            {
                var p = new LoginFailedMessage(this.Client);
                p.SetErrorCode(10);
                PacketManager.ProcessOutgoingPacket(p);
                return;
            }

            string[] versionData = ConfigurationManager.AppSettings["clientVersion"].Split('.');
            if(versionData.Length >= 2)
            {
                if(m_vClientMajorVersion != Convert.ToInt32(versionData[0]) || m_vClientBuild != Convert.ToInt32(versionData[1]))
                {
                    //R-C8: servidor PRIVADO - nao recusa outras versoes (o client v8
                    //8.67.8 e usado para comparar o castelo do cla). So avisa no log.
                    Console.WriteLine("[UCS] R-C8: versao do client " + m_vClientMajorVersion + "." +
                        m_vClientBuild + " (esperado " + versionData[0] + "." + versionData[1] + ") - aceito (servidor privado)");
                }
            }
            else
            {
                Debugger.WriteLine("Connection failed. UCS config key clientVersion is not properly set.");
            }

            //R-C5: o app guarda localmente o accountId que o servidor devolveu
            //(LoginOk). Depois de limpar os dados ele ficou preso na conta nova (id 25),
            //cuja vila inicial crasha o cliente (crash do tutorial, ja documentado).
            //Com DefaultAccountId > 0 o login SEMPRE cai na conta principal.
            if (ObjectManager.DefaultAccountId > 0)
            {
                if (m_vAccountId != ObjectManager.DefaultAccountId)
                    Console.WriteLine("[UCS] R-C5: login " + m_vAccountId + " -> conta padrao " + ObjectManager.DefaultAccountId);
                m_vAccountId = ObjectManager.DefaultAccountId;
            }
            level = ResourcesManager.GetPlayer(m_vAccountId);
            if(level != null)
            {
                if(level.GetAccountStatus() == 99)
                {
                    var p = new LoginFailedMessage(this.Client);
                    p.SetErrorCode(11);
                    PacketManager.ProcessOutgoingPacket(p);
                    return;
                }
            }

            if(Convert.ToBoolean(ConfigurationManager.AppSettings["useCustomPatch"]))
            {
                if (m_vResourceSha != ObjectManager.FingerPrint.sha)
                {
                    var p = new LoginFailedMessage(this.Client);
                    p.SetErrorCode(7);
                    p.SetResourceFingerprintData(ObjectManager.FingerPrint.SaveToJson());
                    p.SetContentURL(ConfigurationManager.AppSettings["patchingServer"]);
                    p.SetUpdateURL("market://details?id=com.supercell.clashofclans");
                    PacketManager.ProcessOutgoingPacket(p);
                    return;
                }
            }

            this.Client.ClientSeed = m_vClientSeed;
            PacketManager.ProcessOutgoingPacket(new SessionKeyMessage(this.Client));
            //Console.WriteLine("Debug: Retrieve Player Data for player " + auth.PlayerId.ToString());
            //New player
            if (level == null)
            {
                level = ObjectManager.CreateAvatar(m_vAccountId);
                byte[] tokenSeed = new byte[20];
                new Random().NextBytes(tokenSeed);
                SHA1 sha = new SHA1CryptoServiceProvider();
                m_vPassToken = BitConverter.ToString(sha.ComputeHash(tokenSeed)).Replace("-","");
            }
            if (level.GetAccountPrivileges() > 0)
                level.GetPlayerAvatar().SetLeagueId(21);
            if (level.GetAccountPrivileges() > 4)
                level.GetPlayerAvatar().SetLeagueId(22);
            ResourcesManager.LogPlayerIn(level, this.Client);
            level.Tick();

            var loginOk = new LoginOkMessage(this.Client);
            var avatar = level.GetPlayerAvatar();
            loginOk.SetAccountId(avatar.GetId());
            loginOk.SetPassToken(m_vPassToken);
            loginOk.SetServerMajorVersion(m_vClientMajorVersion);
            loginOk.SetServerBuild(m_vClientBuild);
            loginOk.SetContentVersion(m_vClientContentVersion);
            loginOk.SetServerEnvironment("prod");
            loginOk.SetDaysSinceStartedPlaying(10);
            loginOk.SetServerTime(Math.Round((level.GetTime().Subtract(new DateTime(1970, 1, 1))).TotalSeconds * 1000).ToString());
            loginOk.SetAccountCreatedDate("1414003838000");
            loginOk.SetStartupCooldownSeconds(0);
            loginOk.SetCountryCode("FR");
            PacketManager.ProcessOutgoingPacket(loginOk);

            Alliance alliance = ObjectManager.GetAlliance(level.GetPlayerAvatar().GetAllianceId());
            if (alliance == null)
                level.GetPlayerAvatar().SetAllianceId(0);
            //R-C5: sincroniza o contador de tropas do avatar com o storage dos quartéis.
            UnitStorageComponent.SyncAvatarUnitCounts(level);
            //R-C5: ativa o Rei/Rainha (sem isso eles ficam "dormindo" e nao aparecem
            //na tela de ataque). So mexe nos herois cuja vila tem o altar.
            level.GetPlayerAvatar().ActivateHeroes(level);
            DumpBuildingData(level);
            PacketManager.ProcessOutgoingPacket(new OwnHomeDataMessage(this.Client, level));
            //X3: historico do mural - o servidor real NAO manda 24311 (conn27), mas sem ele o mural some ao relogar.
            //     Reativado de proposito (desvio consciente do real) p/ o mural persistir entre relogins do cliente.
            if (alliance != null)
                PacketManager.ProcessOutgoingPacket(new AllianceStreamMessage(this.Client, alliance));
        }
    }
}
