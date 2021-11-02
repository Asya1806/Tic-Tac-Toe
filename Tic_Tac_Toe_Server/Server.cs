using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Tic_Tac_Toe_Server.Classes;
using System.Threading;
using System.Data.SqlClient;
using System.IO;

namespace Tic_Tac_Toe_Server
{
    class Server
    {   
        // Максимальное количество одновременных пользователей
        private static readonly int maxUsers = 4;

        // Количество подключенных в данный момент пользователей
        private static int currentUsers = 0;


        // Обработка запроса на подключение.
        // Возвращает структуру с информацией о принятом игроке
        private static Player AcceptPlayer()
        {
        start:
            using (UdpClient client = new UdpClient(8800)) // автоматичесий вызов
            {
                IPEndPoint playerIp = null;
                string playerName = Encoding.ASCII.GetString(client.Receive(ref playerIp));//получение имени 
                byte[] answer = new byte[4];

                client.Connect(playerIp);
                if (currentUsers >= maxUsers)
                {
                    answer[0] = 0;
                    client.Send(answer, answer.Length);
                    goto start;
                }

                answer[0] = 1;

                client.Send(answer, answer.Length);

                Interlocked.Increment(ref currentUsers);

                Console.WriteLine("Accepted player: {0} with IP: {1}", playerName, playerIp);
                return new Player(playerName, playerIp);
            }
        }

        // Игра между двумя игроками
        private static Task PlayTheGame(Player player1, Player player2)
        {
            Console.WriteLine("Started the game between {0} and {1}", player1.Ip, player2.Ip);

            // Создание поля для игры
            Figure[] playField = new Figure[9];
            for (int i = 0; i < playField.Length; i++) playField[i] = Figure.None;

            // Ждем, пока игроки начнут слушать
            Thread.Sleep(500);
            using (UdpClient client1 = new UdpClient())
            using (UdpClient client2 = new UdpClient())
            {
                client1.Connect(player1.Ip);
                client2.Connect(player2.Ip);

                //Отправляем информацию о порядке очереди
                client1.Send(new byte[1] { 0 }, 1);
                client2.Send(new byte[1] { 1 }, 1);

                Result res;
                while (true)
                {
                    // Получаем информацию о ходе первого игрока
                    res = MakeMove(client1, client2, ref playField, Figure.Cross);
                    if (res != Result.None) break;

                    //  Получаем информацию о ходе второго игрока
                    res = MakeMove(client2, client1, ref playField, Figure.Circle);
                    if (res != Result.None) break;
                }

                // Игра окончена. Отправляем информацию клиентам
                client1.Send(new byte[1] { 0 }, 1);
                client2.Send(new byte[1] { 0 }, 1);

                 // Отправить информацию о результатах игры
                if (res == Result.Draw)
                {
                    client1.Send(new byte[1] { 2 }, 1);
                    client2.Send(new byte[1] { 2 }, 1);
                }
                else if (res == Result.Crosses)
                {
                    client1.Send(new byte[1] { 1 }, 1);
                    client2.Send(new byte[1] { 0 }, 1);
                } 
                else if (res == Result.Circles)
                {
                    client1.Send(new byte[1] { 0 }, 1);
                    client2.Send(new byte[1] { 1 }, 1);
                }
            }
            Interlocked.Add(ref currentUsers, -2);
            return Task.CompletedTask;
        }

        // Обрабатываем ход игрока
        // Возвращаем резльтат движения 
        private static Result MakeMove(UdpClient player1, UdpClient player2,
                                       ref Figure[] playField, Figure figure)
        {
            var lastContactedIp = new IPEndPoint(IPAddress.Any, 8800);

            player1.Send(new byte[1] { 1 }, 1);
            player2.Send(new byte[1] { 1 }, 1);

            // Получаем информацию о движении первого игрока
            byte[] moveInfo = player1.Receive(ref lastContactedIp);
            player2.Send(new byte[1] { moveInfo[0] }, 1);
            playField[moveInfo[0]] = figure;

            return CheckTheField(playField);
        }

        // Проверяем состояние игрового поля
        // Возвращаем текущий результат, например, ничья, выигрыш ноликов, выигрыш крестиков
        private static Result CheckTheField(Figure[] playField)
        {
            // Проверька по горизонтали
            for (int i = 0; i < playField.Length; i += 3)
            {
                if (playField[i] != Figure.None
                    && playField[i] == playField[i + 1]
                    && playField[i] == playField[i + 2])
                    return playField[i] == Figure.Cross ? Result.Crosses : Result.Circles;
            }

            // Проверка по вертикали
            for (int i = 0; i < playField.Length / 3; ++i)
            {
                if (playField[i] != Figure.None
                    && playField[i] == playField[i + 3]
                    && playField[i] == playField[i + 6])
                    return playField[i] == Figure.Cross ? Result.Crosses : Result.Circles;
            }

            // Проверка по диагонали
            if (playField[0] != Figure.None
                    && playField[0] == playField[4]
                    && playField[0] == playField[8])
                return playField[0] == Figure.Cross ? Result.Crosses : Result.Circles;
            if (playField[2] != Figure.None
                    && playField[2] == playField[4]
                    && playField[2] == playField[6])
                return playField[2] == Figure.Cross ? Result.Crosses : Result.Circles;

            // Проверка наличии ничьи, например, все клетки заполнены.
            foreach (Figure field in playField) if (field == Figure.None) return Result.None;

            return Result.Draw;
        }

        static void Main(string[] args)
        {
            while (true)
            {
                Player player1 = AcceptPlayer();
                Player player2 = AcceptPlayer();
                //ставим в очередь уазанную функцию для запуска
                Task.Run(() => PlayTheGame(player1, player2));
            }
        }
    }
}
