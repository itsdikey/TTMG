namespace TTMG.Commands
{
    [Command(":qq", "exit")]
    public class ExitCommand : ICommand
    {
        public Task Execute(string[] args)
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }
    }

    [Command(":wq", "exit")]
    public class WqCommand : ICommand
    {
        public Task Execute(string[] args)
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }
    }
}
