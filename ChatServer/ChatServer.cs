using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChatServer
{
    public class Server
    {
        public event Action<string> OnLog = delegate { };
        public event Action<string> OnClientConnected = delegate { };
        public event Action<string> OnClientDisconnected = delegate { };
        public event Action<string, string> OnMessageReceived = delegate { };

        private TcpListener listener;
        private bool running = false;
        private readonly Dictionary<string, StreamWriter> clients = new Dictionary<string, StreamWriter>();
        private readonly object lockObj = new object();

        public int ClientCount
        {
            get { lock (lockObj) { return clients.Count; } }
        }

        public void Start(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            running = true;
            Log("Сервер запущен на порту " + port);

            while (running)
            {
                try
                {
                    TcpClient tcpClient = listener.AcceptTcpClient();
                    Thread t = new Thread(() => HandleClient(tcpClient));
                    t.IsBackground = true;
                    t.Start();
                }
                catch
                {
                    break;
                }
            }
        }

        public void Stop()
        {
            running = false;
            try { listener.Stop(); } catch { }
            Log("Сервер остановлен.");
        }

        private void HandleClient(TcpClient tcpClient)
        {
            string nickname = "";
            StreamReader reader = null;
            StreamWriter writer = null;

            try
            {
                NetworkStream stream = tcpClient.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream);
                writer.AutoFlush = true;

                // Первая строка — /join НикНейм
                string joinLine = reader.ReadLine();
                if (joinLine == null || !joinLine.StartsWith("/join "))
                {
                    writer.WriteLine("SERVER:Ошибка: ожидается /join nickname");
                    tcpClient.Close();
                    return;
                }

                nickname = joinLine.Substring(6).Trim();
                if (nickname == "") nickname = "Аноним";

                lock (lockObj)
                {
                    if (clients.ContainsKey(nickname))
                    {
                        writer.WriteLine("SERVER:Имя занято! Выберите другое.");
                        tcpClient.Close();
                        return;
                    }
                    clients[nickname] = writer;
                }

                Log("[+] " + nickname + " подключился. Клиентов: " + ClientCount);
                OnClientConnected(nickname);
                Broadcast("SERVER:" + nickname + " вошёл в чат.", nickname);
                BroadcastUserList();

                // Цикл чтения сообщений
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim() == "") continue;

                    if (line == "/users")
                    {
                        SendUserList(nickname);
                    }
                    else if (line.StartsWith("/pm "))
                    {
                        HandlePM(nickname, line);
                    }
                    else
                    {
                        // Формат: nickname:текст
                        string text = line;
                        int idx = line.IndexOf(':');
                        if (idx >= 0) text = line.Substring(idx + 1);

                        OnMessageReceived(nickname, text);
                        Broadcast(nickname + ":" + text, nickname);
                    }
                }
            }
            catch (Exception) { }
            finally
            {
                if (nickname != "")
                {
                    lock (lockObj) { clients.Remove(nickname); }
                    Log("[-] " + nickname + " отключился. Клиентов: " + ClientCount);
                    OnClientDisconnected(nickname);
                    Broadcast("SERVER:" + nickname + " покинул чат.", null);
                    BroadcastUserList();
                }
                try { reader.Close(); } catch { }
                try { writer.Close(); } catch { }
                try { tcpClient.Close(); } catch { }
            }
        }

        private void Broadcast(string message, string exclude)
        {
            lock (lockObj)
            {
                foreach (KeyValuePair<string, StreamWriter> pair in clients)
                {
                    if (pair.Key == exclude) continue;
                    try { pair.Value.WriteLine(message); } catch { }
                }
            }
        }

        private void HandlePM(string sender, string line)
        {
            // /pm Боб Привет
            string[] parts = line.Split(new char[] { ' ' }, 3);
            if (parts.Length < 3) return;
            string target = parts[1];
            string text = parts[2];

            lock (lockObj)
            {
                if (clients.ContainsKey(target))
                {
                    try { clients[target].WriteLine("[ЛС от " + sender + "]:" + text); } catch { }
                    try { clients[sender].WriteLine("[ЛС для " + target + "]:" + text); } catch { }
                }
                else
                {
                    if (clients.ContainsKey(sender))
                        try { clients[sender].WriteLine("SERVER:Пользователь " + target + " не найден."); } catch { }
                }
            }
        }

        private void SendUserList(string toNick)
        {
            lock (lockObj)
            {
                if (!clients.ContainsKey(toNick)) return;
                string userList = "";
                foreach (string k in clients.Keys)
                {
                    if (userList != "") userList += ",";
                    userList += k;
                }
                try { clients[toNick].WriteLine("/userlist " + userList); } catch { }
            }
        }

        private void BroadcastUserList()
        {
            lock (lockObj)
            {
                string userList = "";
                foreach (string k in clients.Keys)
                {
                    if (userList != "") userList += ",";
                    userList += k;
                }
                foreach (KeyValuePair<string, StreamWriter> pair in clients)
                {
                    try { pair.Value.WriteLine("/userlist " + userList); } catch { }
                }
            }
        }

        private void Log(string msg)
        {
            OnLog("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg);
        }
    }
}