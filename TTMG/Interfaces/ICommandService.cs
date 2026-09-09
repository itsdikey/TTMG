
namespace TTMG.Interfaces
{
        public interface ICommandService
        {
            List<CommandEntry> SystemCommands { get; }
            Task<bool> TryExecuteCommand(string input);
            IEnumerable<(string Code, string Action)> GetAvailableCommands();
            IEnumerable<string> GetSuggestions(string input);
        }
    }
