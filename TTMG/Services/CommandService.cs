using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TTMG.Commands;
using TTMG.Interfaces;

namespace TTMG.Services
{
    public class CommandService : ICommandService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, (Type Type, CommandAttribute Attribute)> _commands = new();
        private readonly Dictionary<string, ICommand> _commandInstances = new();

        public List<CommandEntry> SystemCommands { get; }

        public CommandService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            SystemCommands = new List<CommandEntry>();
            DiscoverCommands();
        }

        private void DiscoverCommands()
        {
            var commandTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.GetCustomAttribute<CommandAttribute>() != null && typeof(ICommand).IsAssignableFrom(t));

            foreach (var type in commandTypes)
            {
                var attr = type.GetCustomAttribute<CommandAttribute>()!;
                _commands[attr.Code] = (type, attr);
                SystemCommands.Add(new CommandEntry() { Action = attr.Action, Code = attr.Code });
            }
        }

        public async Task<bool> TryExecuteCommand(string input)
        {
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            var code = parts[0].ToLower();
            if (_commands.TryGetValue(code, out var cmdInfo))
            {
                var commandInstance = GetCommandInstance(code, cmdInfo); 
                var args = parts.Skip(1).ToArray();
                await commandInstance.Execute(args);
                return true;
            }

            return false;
        }

        private ICommand GetCommandInstance(string code, (Type Type, CommandAttribute Attribute) cmdInfo)
        {
            if (!_commandInstances.ContainsKey(code))
            {
                _commandInstances[code] = (ICommand)ActivatorUtilities.CreateInstance(_serviceProvider, cmdInfo.Type);
            }

            return _commandInstances[code];
        }

        public IEnumerable<(string Code, string Action)> GetAvailableCommands()
        {
            return _commands.Values.Select(v => (v.Attribute.Code, v.Attribute.Action));
        }
    }
}
