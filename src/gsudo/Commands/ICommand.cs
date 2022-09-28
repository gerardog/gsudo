using System.Threading.Tasks;

namespace gsudo.Commands
{
    public interface ICommand
    {
        Task<int> Execute();
    }
}