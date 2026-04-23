using System;
using System.Threading;
using System.Windows;

namespace ChatServer
{
    public partial class MainWindow : Window
    {
        private Server server;
        private Thread serverThread;
        private bool isRunning = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtPort.Text.Trim(), out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Введите правильный порт (1-65535).");
                return;
            }

            server = new Server();
            server.OnLog += (msg) => AddLog(msg);
            server.OnClientConnected += (nick) => { AddLog("[ВХОД] " + nick); UpdateCount(); };
            server.OnClientDisconnected += (nick) => { AddLog("[ВЫХОД] " + nick); UpdateCount(); };
            server.OnMessageReceived += (nick, text) => AddLog("[МСГ] " + nick + ": " + text);

            serverThread = new Thread(() => server.Start(port));
            serverThread.IsBackground = true;
            serverThread.Start();

            isRunning = true;
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            TxtPort.IsEnabled = false;
            LblStatus.Content = "Статус: работает на порту " + port;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (server != null) server.Stop();
            isRunning = false;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            TxtPort.IsEnabled = true;
            LblStatus.Content = "Статус: остановлен";
        }

        private void AddLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                ListLog.Items.Add(msg);
                ListLog.ScrollIntoView(ListLog.Items[ListLog.Items.Count - 1]);
            });
        }

        private void UpdateCount()
        {
            Dispatcher.Invoke(() =>
            {
                LblCount.Content = "Клиентов: " + (server != null ? server.ClientCount.ToString() : "0");
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (server != null) server.Stop();
        }
    }
}