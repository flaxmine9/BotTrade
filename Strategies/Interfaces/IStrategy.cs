using System.Threading.Tasks;

namespace Strategy.Interfaces
{
    public interface IStrategy
    {
        public Task Logic();
        public Task Start(string key, string secretKey);
    }
}
