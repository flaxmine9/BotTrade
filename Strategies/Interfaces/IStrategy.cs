using DataBase;
using System.Threading.Tasks;

namespace Strategy.Interfaces
{
    public interface IStrategy
    {
        public Task Logic();
        public Task Start(string nameUser, string key, string secretKey, ApplicationContext dataBase);
    }
}
