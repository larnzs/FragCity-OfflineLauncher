#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class ServerInfo
{
    public string Ip { get; set; }
    public int Port { get; set; }
    public string Name { get; set; }
    public string Map { get; set; }
    public string Folder { get; set; }
    public string Game { get; set; }
    public short AppId { get; set; }
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public int Bots { get; set; }
    public char ServerType { get; set; }
    public char ServerOS { get; set; }
    public bool HasPassword { get; set; }
    public bool IsSecure { get; set; }
    public string Version { get; set; }
    public int Ping { get; set; }
}

public class PlayerInfo
{
    public byte Index { get; set; }
    public string Name { get; set; }
    public int Score { get; set; }
    public float Duration { get; set; }
}

public static class ServerQuery
{
    private static readonly byte[] A2S_INFO_REQUEST = new byte[]
    {
        0xFF, 0xFF, 0xFF, 0xFF, 0x54,
        0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45,
        0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75,
        0x65, 0x72, 0x79, 0x00
    };

    public static async Task<ServerInfo> GetServerDetailsAsync(IPEndPoint endpoint)
    {
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 3000;
        udp.Client.SendTimeout = 3000;

        try
        {
            await udp.SendAsync(A2S_INFO_REQUEST, A2S_INFO_REQUEST.Length, endpoint);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] response = udp.Receive(ref remote);

            byte[] challenge = new byte[4];
            Array.Copy(response, 5, challenge, 0, 4);

            byte[] requestWithChallenge = new byte[A2S_INFO_REQUEST.Length + 4];
            Buffer.BlockCopy(A2S_INFO_REQUEST, 0, requestWithChallenge, 0, A2S_INFO_REQUEST.Length);
            Buffer.BlockCopy(challenge, 0, requestWithChallenge, A2S_INFO_REQUEST.Length, 4);

            var start = DateTime.UtcNow;
            await udp.SendAsync(requestWithChallenge, requestWithChallenge.Length, endpoint);
            byte[] finalResponse = udp.Receive(ref remote);
            int ping = (int)(DateTime.UtcNow - start).TotalMilliseconds;

            using var stream = new MemoryStream(finalResponse);
            using var reader = new BinaryReader(stream);
            int header = reader.ReadInt32();
            if (header != -1) return null;
            byte protocol = reader.ReadByte();
            byte version = reader.ReadByte();
            string name = ReadSteamString(reader);
            string map = ReadSteamString(reader);
            string folder = ReadSteamString(reader);
            string game = ReadSteamString(reader);
            short appId = reader.ReadInt16();
            byte players = reader.ReadByte();
            byte maxPlayers = reader.ReadByte();
            byte bots = reader.ReadByte();
            byte serverType = reader.ReadByte();
            byte serverOs = reader.ReadByte();
            byte visibility = reader.ReadByte();
            byte vac = reader.ReadByte();
            string versionStr = ReadSteamString(reader);

            return new ServerInfo
            {
                Ip = endpoint.Address.ToString(),
                Port = endpoint.Port,
                Name = name,
                Map = map,
                Folder = folder,
                Game = game,
                AppId = appId,
                Players = players,
                MaxPlayers = maxPlayers,
                Bots = bots,
                ServerType = (char)serverType,
                ServerOS = (char)serverOs,
                HasPassword = (visibility == 1),
                IsSecure = (vac == 1),
                Version = versionStr,
                Ping = ping
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task<int> GetServerPingAsync(IPEndPoint endpoint)
    {
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 1000;
        udp.Client.SendTimeout = 1000;

        try
        {
            await udp.SendAsync(A2S_INFO_REQUEST, A2S_INFO_REQUEST.Length, endpoint);
            var receiveResult = await udp.ReceiveAsync();
            byte[] response = receiveResult.Buffer;

            byte[] challenge = new byte[4];
            Array.Copy(response, 5, challenge, 0, 4);

            byte[] requestWithChallenge = new byte[A2S_INFO_REQUEST.Length + 4];
            Buffer.BlockCopy(A2S_INFO_REQUEST, 0, requestWithChallenge, 0, A2S_INFO_REQUEST.Length);
            Buffer.BlockCopy(challenge, 0, requestWithChallenge, A2S_INFO_REQUEST.Length, 4);

            var start = DateTime.UtcNow;
            await udp.SendAsync(requestWithChallenge, requestWithChallenge.Length, endpoint);
            receiveResult = await udp.ReceiveAsync();
            int ping = (int)(DateTime.UtcNow - start).TotalMilliseconds;

            if (receiveResult.Buffer.Length > 5 && receiveResult.Buffer[4] == 0x49)
                return ping;
            else
                return -1;
        }
        catch
        {
            return -1;
        }
    }

    public static async Task<Dictionary<string, string>> GetServerRulesAsync(IPEndPoint endpoint)
    {
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 3000;
        byte[] request = { 0xFF, 0xFF, 0xFF, 0xFF, 0x56, 0xFF, 0xFF, 0xFF, 0xFF };
        try
        {
            await udp.SendAsync(request, request.Length, endpoint);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] response = udp.Receive(ref remote);
            if (response.Length > 5 && response[4] == 0x56)
            {
                int idx = 5;
                if (response[idx] != 0) idx += 4;
                var rules = new Dictionary<string, string>();
                while (idx < response.Length)
                {
                    string key = ReadNullTerminatedString(response, ref idx);
                    if (string.IsNullOrEmpty(key)) break;
                    string value = ReadNullTerminatedString(response, ref idx);
                    rules[key] = value;
                }
                return rules;
            }
        }
        catch { }
        return null;
    }

    public static async Task<List<PlayerInfo>> GetPlayersAsync(IPEndPoint endpoint)
    {
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 3000;
        udp.Client.SendTimeout = 3000;

        try
        {
            byte[] challengeReq = { 0xFF, 0xFF, 0xFF, 0xFF, 0x55, 0xFF, 0xFF, 0xFF, 0xFF };
            await udp.SendAsync(challengeReq, challengeReq.Length, endpoint);
            var remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] challengeResp = udp.Receive(ref remote);

            if (challengeResp.Length < 9 || challengeResp[4] != 0x41)
                return null;

            byte[] challenge = new byte[4];
            Array.Copy(challengeResp, 5, challenge, 0, 4);

            // 🔧 اصلاح: ساخت بسته ۹ بایتی با جایگذاری چالش
            byte[] playerReq = new byte[9];
            Array.Copy(challengeReq, 0, playerReq, 0, 5);
            Array.Copy(challenge, 0, playerReq, 5, 4);

            await udp.SendAsync(playerReq, playerReq.Length, endpoint);
            byte[] playersResp = udp.Receive(ref remote);

            if (playersResp.Length < 6 || playersResp[4] != 0x44)
                return null;

            using var ms = new MemoryStream(playersResp);
            using var br = new BinaryReader(ms);
            int header = br.ReadInt32();
            if (header != -1) return null;
            br.ReadByte(); // 0x44
            byte numPlayers = br.ReadByte();

            var players = new List<PlayerInfo>();
            for (int i = 0; i < numPlayers; i++)
            {
                byte index = br.ReadByte();
                string name = ReadSteamString(br);
                int score = br.ReadInt32();
                float duration = br.ReadSingle();
                players.Add(new PlayerInfo
                {
                    Index = index,
                    Name = name,
                    Score = score,
                    Duration = duration
                });
            }
            return players;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadSteamString(BinaryReader reader)
    {
        var bytes = new List<byte>();
        byte b;
        while ((b = reader.ReadByte()) != 0)
            bytes.Add(b);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static string ReadNullTerminatedString(byte[] data, ref int index)
    {
        int start = index;
        while (index < data.Length && data[index] != 0)
            index++;
        string result = Encoding.UTF8.GetString(data, start, index - start);
        index++;
        return result;
    }
}