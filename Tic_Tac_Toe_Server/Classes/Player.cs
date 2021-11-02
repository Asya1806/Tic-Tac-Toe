using System.Net;

namespace Tic_Tac_Toe_Server.Classes
{
    // Структура с информаией об игроке
    public class Player
    {
        // Имя игрока
        public readonly string Name;
        // IP адрес игрока
        public readonly IPEndPoint Ip;

        public Player(string name, IPEndPoint ip)
        {
            Name = name;
            Ip = ip;
        }
    }
}

