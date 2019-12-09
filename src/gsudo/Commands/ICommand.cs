using System.Threading.Tasks;

namespace gsudo.Commands
{
    interface ICommand
    {
        Task<int> Execute();
    }
}