using System;
using System.Text;
using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Net.Sockets;
using System.Threading;

namespace Nxun.Fetion
{
    class FetionSender
    {
        private const string ClientVersion = "4.0.0";
        private const int BufferSize = 8192;

        private string mobile; // 手机号
        private string sid; // 飞信号
        private string uid; // 用户编号
        private string password; // 密码

        private string ssiServer; // SSI服务器
        private string picServer; // 认证图片服务器
        private string sipcServer; // SIPC服务器

        private string verifyId; // 认证图片id
        private string verifyText; // 认证图片文字

        private Socket socket;

        public FetionSender(string mobile, string password)
        {
            this.mobile = mobile;
            this.password = password;

            GetSystemConfig();
        }

        public int Initialize()
        {
            return SsiSignIn();
        }

        public int Initialize2()
        {
            int status = SsiSignIn();
            if (status != 200)
            {
                return status;
            }
            OpenSocket();
            try
            {
                status = SipcSignIn();
            }
            finally
            {
                CloseSocket();
            }
            return status;
        }

        public int SendMessage(string message)
        {
            return SendMessage(message, string.Empty);
        }

        public int SendMessage(string message, string mobile)
        {
            int status;
            OpenSocket();
            try
            {
                status = SipcSignIn();
                if (status == 200) // 登录成功
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("M fetion.com.cn SIP-C/4.0");
                    sb.AppendLine(string.Format("F: {0}", this.sid));
                    sb.AppendLine("I: 1");
                    sb.AppendLine("Q: 1 M");
                    if (string.IsNullOrEmpty(mobile) || mobile == this.mobile) // 发给自己
                    {
                        sb.AppendLine(string.Format("T: sip:{0}@fetion.com.cn;p=6904", this.sid));
                    }
                    else // 发给指定号码
                    {
                        string sid = GetSidByMobile(mobile);
                        if (string.IsNullOrEmpty(sid)) // 找不到此用户
                        {
                            return 400;
                        }
                        sb.AppendLine(string.Format("T: sip:{0}@fetion.com.cn;p=6904", sid));
                    }
                    sb.AppendLine("N: SendCatSMS");
                    sb.AppendLine(string.Format("L: {0}", Encoding.UTF8.GetByteCount(message)));
                    sb.AppendLine(string.Empty);
                    sb.AppendLine(message);
                    string response = GetSocketResponse(sb.ToString());
                    try
                    {
                        status = int.Parse(GetInnerText(response, "SIP-C/4.0 ", " "));
                    }
                    catch
                    {
                        status = 404;
                    }
                    SignOut();
                }
            }
            finally
            {
                CloseSocket();
            }
            return status;
        }

        public void Verify(string id, string text)
        {
            this.verifyId = id;
            this.verifyText = text;
        }

        public void GetVerifyPic(out string id, out string pic)
        {
            string uri = string.Format("{0}?algorithm=picc-ChangeMachine", this.picServer);
            string response = HttpGet(uri);
            id = GetInnerText(response, "id=\"", "\"");
            pic = GetInnerText(response, "pic=\"", "\"");
        }

        private void OpenSocket()
        {
            string[] server = this.sipcServer.Split(':'); // 分离ip和port
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(server[0]), int.Parse(server[1]));
            socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveTimeout = 10000;
            socket.ReceiveTimeout = 10000;
            socket.Connect(endPoint);
        }

        private void CloseSocket()
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        private void SignOut()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("R fetion.com.cn SIP-C/4.0");
            sb.AppendLine(string.Format("F: {0}", this.sid));
            sb.AppendLine("I: 1");
            sb.AppendLine("Q: 3 R");
            sb.AppendLine("X: 0");
            sb.AppendLine(string.Empty);

            GetSocketResponse(sb.ToString());
        }

        private string GetSidByMobile(string mobile)
        {
            string content = string.Format("<args><contact uri=\"tel:{0}\" /></args>", mobile);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("S fetion.com.cn SIP-C/4.0");
            sb.AppendLine(string.Format("F: {0}", this.sid));
            sb.AppendLine("I: 1");
            sb.AppendLine("Q: 1 S");
            sb.AppendLine("N: GetContactInfoV4");
            sb.AppendLine(string.Format("L: {0}", Encoding.UTF8.GetByteCount(content)));
            sb.AppendLine(string.Empty);
            sb.AppendLine(content);
            sb.AppendLine(string.Empty);
            string response = GetSocketResponse(sb.ToString());

            if ("200" != GetInnerText(response, "SIP-C/4.0 ", " "))
            {
                return "";
            }
            return GetInnerText(response, "sid=\"", "\"");
        }

        private void ClearVerifyInfo()
        {
            this.verifyId = string.Empty;
            this.verifyText = string.Empty;
        }

        private void GetSystemConfig()
        {
            string data = string.Format("<config><user mobile-no=\"{0}\" /><client type=\"PC\" version=\"{1}\" platform=\"W6.1\" /><servers version=\"0\" /><service-no version=\"0\" /><parameters version=\"0\" /><hints version=\"0\" /><http-applications version=\"0\" /><client-config version=\"0\" /><services version=\"0\" /></config>",
                mobile, ClientVersion);
            string response = HttpPost("http://nav.fetion.com.cn/nav/getsystemconfig.aspx", data);
            this.ssiServer = GetInnerText(response, "<ssi-app-sign-in-v2>", "<");
            this.picServer = GetInnerText(response, "<get-pic-code>", "<");
            this.sipcServer = GetInnerText(response, "<sipc-proxy>", "<");
        }

        private int SsiSignIn()
        {
            byte[] bytes = GetHashByteArray(Encoding.UTF8.GetBytes("fetion.com.cn:"), Encoding.UTF8.GetBytes(password));
            string uri = string.Empty;
            if (string.IsNullOrEmpty(this.verifyText)) // 未提供验证码
            {
                uri = string.Format("{0}?mobileno={1}&&domains=fetion.com.cn&v4digest-type=1&v4digest={2}",
                    ssiServer, mobile, BitConverter.ToString(bytes).Replace("-", ""));
            }
            else // 提供了验证码
            {
                uri = string.Format("{0}?mobileno={1}&&domains=fetion.com.cn&pid={3}&pic={4}&algorithm=picc-ChangeMachine&v4digest-type=1&v4digest={2}",
                    ssiServer, mobile, BitConverter.ToString(bytes).Replace("-", ""), verifyId, verifyText);
                ClearVerifyInfo();
            }
            string response = string.Empty;
            try
            {
                response = HttpGet(uri);
            }
            catch (WebException ex)
            {
                int status = (int)((HttpWebResponse)ex.Response).StatusCode;
                return status;
            }

            this.sid = GetInnerText(response, "uri=\"sip:", "@");
            this.uid = GetInnerText(response, "user-id=\"", "\"");
            return 200;
        }

        private int SipcSignIn()
        {
            // Step 1
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("R fetion.com.cn SIP-C/4.0");
            sb.AppendLine("F: " + this.sid);
            sb.AppendLine("I: 1");
            sb.AppendLine("Q: 1 R");
            sb.AppendLine("CN: 8ea102c4a91155104f0164007229c9ee");
            sb.AppendLine(string.Format("CL: type=\"pc\",version=\"{0}\"", ClientVersion));
            sb.AppendLine(string.Empty);
            string response = GetSocketResponse(sb.ToString());

            // Step 2.1 计算hash
            string key = GetInnerText(response, "key=\"", "\"");
            string nonce = GetInnerText(response, "nonce=\"", "\"");
            byte[] b1 = Encoding.UTF8.GetBytes(nonce);
            byte[] b2 = GetHashByteArray(Encoding.UTF8.GetBytes("fetion.com.cn:"), Encoding.UTF8.GetBytes(this.password));
            b2 = GetHashByteArray(BitConverter.GetBytes(int.Parse(this.uid)), b2);
            byte[] b3 = HexStringToByteArray("568AC8CA87A03B388903BFD6C7836B6A00FB32755CD68EEEE9CEDFD234DC8451");
            byte[] b4 = new byte[b1.Length + b2.Length + b3.Length];
            Array.Copy(b1, 0, b4, 0, b1.Length);
            Array.Copy(b2, 0, b4, b1.Length, b2.Length);
            Array.Copy(b3, 0, b4, b1.Length + b2.Length, b3.Length);

            // Step 2.2 RSA加密
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            RSAParameters param = new RSAParameters();
            param.Modulus = HexStringToByteArray(key.Substring(0, 0x100));
            param.Exponent = HexStringToByteArray(key.Substring(0x100));
            rsa.ImportParameters(param);
            byte[] b5 = rsa.Encrypt(b4, false);
            string result = BitConverter.ToString(b5).Replace("-", "");

            // Step 2.3 发送
            string content = string.Format("<args><device accept-language=\"default\" machine-code=\"1465F67D516FDFDA0B9822793C3A3CE4\" /><caps value=\"1FFF\" /><events value=\"7F\" /><user-info user-id=\"{0}\"><personal version=\"0\" attributes=\"v4default\" /><custom-config version=\"0\" /><contact-list version=\"0\" buddy-attributes=\"v4default\" /></user-info><credentials domains=\"fetion.com.cn\" /><presence><basic value=\"0\" desc=\"\" /><extendeds /></presence></args>",
                this.uid);
            sb = new StringBuilder();
            sb.AppendLine("R fetion.com.cn SIP-C/4.0");
            sb.AppendLine("F: " + this.sid);
            sb.AppendLine("I: 1");
            sb.AppendLine("Q: 2 R");
            sb.AppendLine(string.Format("A: Digest algorithm=\"SHA1-sess-v4\",response=\"{0}\"", result));
            if (!string.IsNullOrEmpty(verifyText)) // 提供了验证信息
            {
                sb.AppendLine(string.Format("A: Verify response=\"{1}\",algorithm=\"picc-ChangeMachine\",type=\"GeneralPic\",chid=\"{0}\"", verifyId, verifyText));
                ClearVerifyInfo();
            }
            sb.AppendLine(string.Format("L: {0}", Encoding.UTF8.GetByteCount(content)));
            sb.AppendLine(string.Empty);
            sb.AppendLine(content);

            response = GetSocketResponse(sb.ToString());
            try
            {
                return int.Parse(GetInnerText(response, "SIP-C/4.0 ", " "));
            }
            catch
            {
                return 404;
            }
        }

        private string GetSocketResponse(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            socket.Send(bytes);

            byte[] buffer = new byte[BufferSize];
            string response = string.Empty;
            do
            {
                socket.Receive(buffer);
                response += Encoding.UTF8.GetString(buffer);
                //Thread.Sleep(20);
            }
            while (socket.Available > 0 || !response.Contains("SIP-C/4.0 "));
            //while (socket.Available > 0);
            return response;
        }

        private static byte[] GetHashByteArray(byte[] b1, byte[] b2)
        {
            byte[] b3 = new byte[b1.Length + b2.Length];
            Array.Copy(b1, 0, b3, 0, b1.Length);
            Array.Copy(b2, 0, b3, b1.Length, b2.Length);
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] b4 = sha1.ComputeHash(b3);
                return b4;
            }
        }

        private static byte[] HexStringToByteArray(string str)
        {
            byte[] bytes = new byte[str.Length / 2];
            for (int i = 0; i < str.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(str.Substring(i, 2), 16);
            }
            return bytes;
        }

        private static string GetInnerText(string str, string token1, string token2)
        {
            int i = str.IndexOf(token1);
            if (i == -1)
            {
                return string.Empty;
            }
            i += token1.Length;
            if (string.IsNullOrEmpty(token2))
            {
                return str.Substring(i);
            }
            int j = str.IndexOf(token2, i);
            if (j == -1)
            {
                return string.Empty;
            }
            return str.Substring(i, j - i);
        }

        private string HttpGet(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.Accept = "*/*";
            request.ContentType = "application/oct-stream";
            request.UserAgent = "IIC2.0/PC " + ClientVersion;

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            return (new StreamReader(response.GetResponseStream(), Encoding.UTF8)).ReadToEnd();
        }

        private string HttpPost(string uri, string data)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            request.ContentLength = bytes.Length;
            request.Method = "POST";
            request.Accept = "*/*";
            request.ContentType = "application/oct-stream";
            request.UserAgent = "IIC2.0/PC " + ClientVersion;

            request.GetRequestStream().Write(bytes, 0, bytes.Length);

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            return (new StreamReader(response.GetResponseStream(), Encoding.UTF8)).ReadToEnd();
        }
    }
}
