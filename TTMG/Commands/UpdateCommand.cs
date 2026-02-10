using TTMG.Interfaces;

namespace TTMG.Commands
{
    [Command(":update", "check_updates")]
    public class UpdateCommand : ICommand
    {
        private readonly IUpdaterService _updaterService;

        public UpdateCommand(IUpdaterService updaterService)
        {
            _updaterService = updaterService;
        }

        public async Task Execute(string[] args)
        {
            await _updaterService.CheckForUpdates(true);
        }
    }
}
