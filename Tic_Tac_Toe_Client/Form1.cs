using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tic_Tac_Toe_Client
{
    public partial class ticTacToe : Form
    {
        // Начальный порт
        private static int startingPort = 8800;
        // IP - адрес сервера
        private IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), startingPort);
        // Событие для получения информации о ходах в асинхронной игре
        private AutoResetEvent moveMadeEvent = new AutoResetEvent(false);
        // Игровое поле
        private PictureBox[] playField;
        // Изображение круга
        private Image circle => Properties.Resources.circle;
        // Изображение креста
        private Image cross => Properties.Resources.cross;
        // ID клиента. Для идентификации уникального порта
        private int clientId;
        // Клиент этого пользователя.
        private UdpClient client;
        // Номер плитки, в которой был выполнен ход
        private byte moveIndex;

        public ticTacToe()
        {
            InitializeComponent();
            playField = fieldGrpBox.Controls.OfType<PictureBox>().Reverse().ToArray();

            // Дайте уникальный порт.
            string processName = Process.GetCurrentProcess().ProcessName;            
            clientId = Process.GetProcesses().Count(p => p.ProcessName == processName); //
            //MessageBox.Show(clientId.ToString());
        }

        // Нажимаем на кнопку подключения

        private void ConnectButtonClick(object sender, EventArgs e)
        {
            // Чтобы продолжить, нам нужно имя игрока
            if (nameBox.Text == string.Empty)
            {
                MessageBox.Show("Please enter your name.");
                return;
            }

            // Отправьте имя игрока и получите статистику.
            byte[] answer;
            using (client = new UdpClient(startingPort + clientId))
            {
                byte[] data = Encoding.ASCII.GetBytes(nameBox.Text);
                client.Connect(endPoint);
                client.Send(data, data.Length);
                answer = client.Receive(ref endPoint);
            }

            if (answer[0] == 1)
            {
                StatusLbl.Text = "Waiting for an opponent";
                connectButton.Enabled = false;

                winTxtStat.Text  = answer[1].ToString();
                loseTxtStat.Text = answer[2].ToString();
                drawTxtStat.Text = answer[3].ToString();

                Task.Run(() => Play());
            }
            else if (answer[0] == 0) StatusLbl.Text = "Server is busy, try again later";
        }

        // Играть в игру
        private void Play()
        {
            IPEndPoint hostIp = null;
            Image mySymbol, opponentSymbol;

            // Создайте UDP-соединение и привяжите его к локальному порту
            using (client = new UdpClient(startingPort + clientId))
            {
                byte[] data = client.Receive(ref hostIp);
                client.Connect(hostIp);
                mySymbol = data[0] == 0 ? cross : circle;
                symbolTxt.Text = data[0] == 0 ? "cross" : "circle";
                opponentSymbol = data[0] == 0 ? circle : cross;

                bool myTurn = data[0] == 0;

                // 1 - продолжить игру, 0 - остановить.
                while (client.Receive(ref hostIp)[0] == 1)
                {
                    if (myTurn)
                    {
                        // Используем invoke для изменения объектов выигрышных форм
                        Invoke(new MethodInvoker(() =>
                        {
                            fieldGrpBox.Enabled = true;
                            StatusLbl.Text = "Your turn";
                        }));
                        moveMadeEvent.WaitOne();

                        Invoke(new MethodInvoker(() => playField[moveIndex].Image = mySymbol));
                        client.Send(new byte[1] { moveIndex }, 1);
                        myTurn = false;
                    }
                    else
                    {
                        Invoke(new MethodInvoker(() =>
                        {
                            fieldGrpBox.Enabled = false;
                            StatusLbl.Text = "Opponent's turn";
                        }));
                        moveIndex = client.Receive(ref hostIp)[0];

                        Invoke(new MethodInvoker(() => playField[moveIndex].Image = opponentSymbol));
                        myTurn = true;
                    }
                }

                // Конец игры
                data = client.Receive(ref hostIp);
                if (data[0] == 2)
                {
                    MessageBox.Show("It's a draw!");
                    drawTxtStat.Text = (Convert.ToInt32(drawTxtStat.Text) + 1).ToString();
                }
                else if (data[0] == 1)
                {
                    MessageBox.Show("Decisive victory!");
                    winTxtStat.Text = (Convert.ToInt32(winTxtStat.Text) + 1).ToString();
                }
                else if (data[0] == 0)
                {
                    MessageBox.Show("Better luck next time!");
                    loseTxtStat.Text = (Convert.ToInt32(loseTxtStat.Text) + 1).ToString();
                }
                Invoke(new MethodInvoker(() =>
                {
                    foreach (PictureBox field in playField) field.Image = null;
                    fieldGrpBox.Enabled = false;
                    connectButton.Enabled = true;
                    StatusLbl.Text = "Disconnected";
                    symbolTxt.Text = "none";
                }));
            }
        }

        // После нажатия на поле.
        private void FieldClick(object sender, EventArgs e)
        {
            PictureBox field = (PictureBox)sender;
            if (field.Image != null) return;

            moveIndex = byte.Parse(field.Name.Remove(0, 5));
            moveMadeEvent.Set();
        }

        private void ticTacToe_Load(object sender, EventArgs e)
        {

        }

        private void nameBox_Enter(object sender, EventArgs e)
        {
            ActiveForm.AcceptButton = connectButton;
        }
    }
}
