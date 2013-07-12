// Minecraft Paid User Verify Service
// Copyright (C) 2013, Jeremy Lam [JLChnToZ]
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using Chraft.Net;
using HexDump;
using System.Configuration;

namespace idv.JLChnToZ.MCPaidVerify {
    class Program {
        static void Main(string[] args) {
            Console.Title = "驗證正版服務伺服器";
            Console.WriteLine("Patch 0.2\n驗證正版服務伺服器 (C) 2013 Jeremy Lam [JLChnToZ].\n");
            Console.WriteLine("This program is based on C#raft dev build 0.0.0.435.\nFor more information, please visit http://c-raft.com.");
            Console.WriteLine(new StreamReader((System.Reflection.Assembly.GetExecutingAssembly()).GetManifestResourceStream("idv.JLChnToZ.MCPaidVerify.LicenseNotice.txt")).ReadToEnd());
            try {
                TCPCallBackHandles T = new TCPCallBackHandles();
                while(Console.ReadKey(true).Key != ConsoleKey.Escape) { }
            } catch(Exception EX) {
                Client.ConsoleWriteTime();
                Console.WriteLine(EX.ToString());
                Console.ReadKey(true);
            }
        }
    }

    public class TCPCallBackHandles {
        TcpListener T;

        /// <summary>
        /// 處理 TCP 通訊的類別
        /// </summary>
        public TCPCallBackHandles() {
            AppSettingsSection config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings;
            this.T = new TcpListener(IPAddress.Any, Convert.ToInt16(config.Settings["port"].Value));
            this.T.Start();
            Client.ConsoleWriteTime();
            Console.WriteLine("正版驗證系統開始從 {0} 號通訊埠接聽請求.", config.Settings["port"].Value);
            T.BeginAcceptTcpClient(new AsyncCallback(acceptTCPClientCallback), T);
        }

        /// <summary>
        /// 接受 TCP 通訊
        /// </summary>
        /// <param name="r">非同步連線狀態</param>
        private void acceptTCPClientCallback(IAsyncResult r) {
            TcpClient TC = T.EndAcceptTcpClient(r);
            TC.ReceiveBufferSize = 2048;
            byte[] buffer = new Byte[TC.ReceiveBufferSize];
            Client C = new Client(TC, buffer);
            NetworkStream NS = C.NetworkStream;
            NS.BeginRead(C.Buffer, 0, C.Buffer.Length, new AsyncCallback(readCallback), C);
            T.BeginAcceptTcpClient(new AsyncCallback(acceptTCPClientCallback), null);
        }

        /// <summary>
        /// 讀取完成後會執行的程式
        /// </summary>
        /// <param name="r">非同步連線狀態</param>
        private void readCallback(IAsyncResult r) {
            Client C = (Client)r.AsyncState;
            if(C == null) return;
            NetworkStream NS = C.NetworkStream;
            int read = NS.EndRead(r);
            if(read == 0) return;
            switch(C.HandleStep()) {
                case 0:
                    NS.Close();
                    C.TcpClient.Close();
                    break;
                case 1:
                    NS.BeginRead(C.Buffer, 0, C.Buffer.Length, new AsyncCallback(readCallback), C);
                    break;
                case 2:
                    NS.BeginWrite(C.OutputBuffer, 0, C.OutputBuffer.Length, new AsyncCallback(writeCallBack), C);
                    break;
            }
        }

        /// <summary>
        /// 寫入完成後會執行的程式
        /// </summary>
        /// <param name="r">非同步連線狀態</param>
        private void writeCallBack(IAsyncResult r) {
            Client C = (Client)r.AsyncState;
            Client.ConsoleWriteTime();
            Console.WriteLine("@{0} 封包已送出.", C.User);
            if(C == null) return;
            NetworkStream NS = C.NetworkStream;
            NS.EndWrite(r);
            if(C.haveToDisconnect) {
                NS.Close();
                C.TcpClient.Close();
            } else {
                NS.BeginRead(C.Buffer, 0, C.Buffer.Length, new AsyncCallback(readCallback), C);
            }
        }

    }

    /// <summary>
    /// 處理客戶端連線的類別
    /// </summary>
    public class Client {
        string UserName, remoteIP;
        RSAParameters ServerKey;
        byte[] publicKey, token, bytes, _output;
        short keyLength, tokenLength;
        string ConnectionId;
        /// <summary>
        /// 是否應該斷線
        /// </summary>
        public bool haveToDisconnect { get; private set; }
        /// <summary>
        /// 與客戶端連線中的 TCP 物件
        /// </summary>
        public TcpClient TcpClient { get; private set; }
        /// <summary>
        /// 客戶端傳來待處理的資料
        /// </summary>
        public byte[] Buffer { get; private set; }
        /// <summary>
        /// 準備傳去客戶端的資料
        /// </summary>
        public byte[] OutputBuffer { get { return _output; } }
        /// <summary>
        /// 與客戶端溝通的資料流
        /// </summary>
        public NetworkStream NetworkStream { get { return TcpClient.GetStream(); } }
        /// <summary>
        /// 玩家 ID
        /// </summary>
        public string User { get { return UserName; } }

        /// <summary>
        /// 建立新的物件處理現在的連線
        /// </summary>
        /// <param name="tcpClient">連線中的 TCP 物件</param>
        /// <param name="buffer">傳來的資料</param>
        public Client(TcpClient tcpClient, byte[] buffer) {
            this.TcpClient = tcpClient;
            this.Buffer = buffer;
            ServerKey = PacketCryptography.GenerateKeyPair();
            publicKey = PacketCryptography.PublicKeyToAsn1(ServerKey);
            token = PacketCryptography.GetRandomToken();
            keyLength = (short)publicKey.Length;
            tokenLength = (short)token.Length;
            bytes = new byte[8];
            
            remoteIP = UserName = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
            new Random().NextBytes(bytes);
            ConnectionId = BitConverter.ToString(bytes).Replace("-", "");
            haveToDisconnect = false;
        }

        /// <summary>
        /// 有新資料到達時呼叫的程式
        /// </summary>
        /// <returns>旗標 (0=斷線, 1=不用回覆, 2=有資料等候回覆)</returns>
        /// <remarks>詳細運作方法, 請參考 http://wiki.vg/Protocol_Encryption </remarks>
        public int HandleStep() {
            byte version;
            bool isDisconnect = false;
            string ServerHost;
            int ServerPort;
            try {
                PacketReader PR = new PacketReader(Buffer, Buffer.Length);
                PacketWriter PW = PacketWriter.CreateInstance();
                Queue<byte[]> strings = new Queue<byte[]>();
                AppSettingsSection config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings;
                // 分析客戶端指令
                switch(Buffer[0]) {
                    case 0xFE: // 查詢伺服器資料: 0xFE
                        ConsoleWriteTime();
                        Console.WriteLine("@{0} 接收伺服器資訊請求. ", UserName);
                        // 傳回踢走: 0xFF + 0x00 + (通訊協定版本) + 0x00 + (伺服器版本) + 0x00 + (伺服器名稱) + 0x00 + (在線玩家數) + 0x00 + (在線玩家上限)
                        PW = kick(String.Format("§1\0{0}\0{1}\0{2}\0{3}\0{4}",
                            51,
                            config.Settings["serverVersion"].Value,
                            config.Settings["motd"].Value,
                            config.Settings["onlinePlayers"].Value,
                            config.Settings["maxPlayers"].Value));
                        isDisconnect = true;
                        break;
                    case 0x02: // 接收握手: 0x02 + (客戶端版本) + (玩家 ID) + (伺服器 IP / 網址) + (伺服器連接埠)
                        version = PR.ReadByte();
                        UserName = PR.ReadString16(64);
                        ServerHost = PR.ReadString16(64);
                        ServerPort = PR.ReadInt();
                        ConsoleWriteTime();
                        Console.WriteLine("@{1} 接收握手指令.\n  玩家 {1} 使用通訊協定版本 {0} 連接到 {2}:{3}.",
                            (int)version, UserName, ServerHost, ServerPort);
                        strings.Enqueue(ASCIIEncoding.BigEndianUnicode.GetBytes(ConnectionId));
                        // 傳回加密請求: 0xFD + (伺服器 ID) + (密鑰長度) + (密鑰) + (金幣長度) + (金幣) 
                        PW = PacketWriter.CreateInstance(7 + keyLength + tokenLength, strings);
                        PW.WriteByte(0xFD);
                        PW.Write(ConnectionId);
                        PW.Write(keyLength);
                        PW.Write(publicKey, 0, keyLength);
                        PW.Write(tokenLength);
                        PW.Write(token, 0, tokenLength);
                        ConsoleWriteTime();
                        Console.WriteLine("@{3} 送出加密請求指令.\n  伺服器 ID: {0}, 密鑰長度: {1}, 金幣長度: {2}.",
                            ConnectionId, keyLength, tokenLength, UserName);
                        break;
                    case 0xFC: // 接受加密: 0xFC + (分享密碼長度) + (分享密碼) + (已加密的金幣長度) + (已加密的金幣)
                        short sharedSecretLength = PR.ReadShort();
                        byte[] sharedSecret = PR.ReadBytes(sharedSecretLength);
                        short variefyTokenLength = PR.ReadShort();
                        byte[] variefyToken = PacketCryptography.Decrypt(PR.ReadBytes(variefyTokenLength));
                        ConsoleWriteTime();
                        Console.WriteLine("@{2} 接收接受加密指令.\n  分享密碼長度: {0}, 已加密的金幣長度: {1}. ",
                            sharedSecretLength, variefyTokenLength, UserName);
                        // 檢查解密後的金幣是否跟傳出去的一樣
                        if(!variefyToken.SequenceEqual(PacketCryptography.VerifyToken)) {
                            // 不一樣, 密鑰無效
                            ConsoleWriteTime();
                            Console.Write("@{0} 金幣不正確.", UserName);
                            PW = kick(config.Settings["tokenInvalidText"].Value);
                        } else {
                            // 是一樣, 密鑰有效
                            ConsoleWriteTime();
                            Console.WriteLine("@{0} 金幣吻合.", UserName);
                            ConsoleWriteTime();
                            Console.WriteLine("@{0} 檢查玩家是否正版...", UserName);
                            // 檢查是否正版
                            // 查詢地址: http://session.minecraft.net/game/checkserver.jsp?user=(玩家ID)&serverId=(伺服器混湊值)
                            // 伺服器混湊值 = (伺服器 ID 的 ASCII 碼) + (客戶端傳來的分享密碼) + (伺服器密鑰) -> ...
                            // ... -> 轉換成 SHA -> 除掉前面的 0 再以二進制補碼補上負號
                            switch(verifyMinecraft(UserName, PacketCryptography.JavaHexDigest(
                                Encoding.UTF8.GetBytes(ConnectionId)
                                .Concat(PacketCryptography.Decrypt(sharedSecret))
                                .Concat(PacketCryptography.PublicKeyToAsn1(ServerKey)).ToArray()))) {
                                case -1: // 與官方伺服器連接失敗
                                    ConsoleWriteTime();
                                    Console.WriteLine("@{0} 連接失敗.", UserName);
                                    PW = kick(config.Settings["connectionFailText"].Value);
                                    break;
                                case 0: // 官方伺服器沒有玩家的登入進程記錄 (不是傳回 YES)
                                    ConsoleWriteTime();
                                    Console.WriteLine("@{0} 得出結果不是正版.", UserName);
                                    PW = kick(config.Settings["verifyFailText"].Value);
                                    break;
                                case 1: // 官方伺服器有玩家的登入進程記錄 (傳回 YES)
                                    ConsoleWriteTime();
                                    Console.WriteLine("@{0} 得出結果是正版.", UserName);
                                    ConsoleWriteTime();
                                    Console.WriteLine("@{0} 傳送驗證碼...", UserName);
                                    // 傳送驗證碼到指定的網頁
                                    string randomcode = getRandomCapcha();
                                    if(sendCode(config.Settings["successWebPage"].Value, UserName, randomcode, remoteIP)) {
                                        // 傳送成功, 把傳送出去的驗證碼也傳給客戶端
                                        ConsoleWriteTime();
                                        Console.WriteLine("@{0} 驗證碼已傳送.", UserName);
                                        PW = kick(String.Format(config.Settings["successText"].Value, randomcode));
                                    } else {
                                        // 傳送失敗
                                        ConsoleWriteTime();
                                        Console.WriteLine("@{0} 連接失敗.", UserName);
                                        PW = kick(config.Settings["connectionFailText"].Value);
                                    }
                                    break;
                            }
                        }
                        isDisconnect = true;
                        break;
                    case 0xFF: // 取消: 0xFF
                        ConsoleWriteTime();
                        Console.WriteLine("@{0} 客戶端取消連接.", UserName);
                        isDisconnect = true;
                        break;
                    default: // 其他
                        ConsoleWriteTime();
                        Console.WriteLine("@{1} 接收到無法辨識的請求, 以下是其內容:\n{0}", Utils.HexDump(Buffer), UserName);
                        break;
                }
                Stream US = PW.UnderlyingStream;
                // 如果有傳回的資料, 把它封裝輸出成二進制
                if(US.Length > 0) {
                    US.Position = 0;
                    _output = new byte[US.Length];
                    US.Read(_output, 0, (int)US.Length);
                    haveToDisconnect = isDisconnect;
                    return 2;
                } else if(isDisconnect) return 0;
                else return 1;
            } catch(Exception ex) {
                Console.WriteLine("@{1} 發生錯誤!\n{0}", ex.ToString(), UserName);
            }
            return 0;
        }

        /// <summary>
        /// 產生新驗證碼
        /// </summary>
        /// <returns>驗證碼</returns>
        private string getRandomCapcha() {
            byte[] randomBytes = new byte[8];
            new Random().NextBytes(randomBytes);
            return Convert.ToBase64String(randomBytes).Replace("=", "").Replace("+","").Replace("/","");
        }

        /// <summary>
        /// 踢掉客戶端前呼叫的函數
        /// </summary>
        /// <param name="Reason">為甚麼要踢掉呢?</param>
        /// <returns>C#raft 封裝好的踢掉指令</returns>
        private PacketWriter kick(String Reason) {
            Queue<byte[]> strings = new Queue<byte[]>();
            strings.Enqueue(ASCIIEncoding.BigEndianUnicode.GetBytes(Reason));
            PacketWriter packetWriter = PacketWriter.CreateInstance(3, strings);
            packetWriter.WriteByte(0xFF);
            packetWriter.Write(Reason);
            ConsoleWriteTime();
            Console.WriteLine("@{0} 送出踢走指令.", UserName);
            return packetWriter;
        }

        /// <summary>
        /// 傳送驗證碼
        /// </summary>
        /// <param name="uri">目標 URL</param>
        /// <param name="user">玩家 ID</param>
        /// <param name="capcha">驗證碼</param>
        /// <param name="hostip">對方的 IP</param>
        /// <returns>成功與否</returns>
        private bool sendCode(string uri, string user, string capcha, string hostip) {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(String.Format(uri, user, capcha, hostip));
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            return response.StatusCode == HttpStatusCode.OK;
        }

        /// <summary>
        /// 驗證正版
        /// </summary>
        /// <param name="user">玩家 ID</param>
        /// <param name="hash">伺服器混湊值</param>
        /// <returns>查詢結果 (伺服器死掉=-1, 盜版=0, 正版=1)</returns>
        private int verifyMinecraft(string user, string hash) {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(String.Format(
             "http://session.minecraft.net/game/checkserver.jsp?user={0}&serverId={1}", user, hash));
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if(response.StatusCode != HttpStatusCode.OK) return -1;
            return new StreamReader(response.GetResponseStream()).ReadToEnd().Contains("YES") ? 1 : 0;
        }

        /// <summary>
        /// 在控制台畫面寫入現在時間
        /// </summary>
        static public void ConsoleWriteTime() {
            DateTime DT = DateTime.Now;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[{0} {1}] ", DT.ToShortDateString(), DT.ToLongTimeString());
            Console.ResetColor();
        }
    }
}
