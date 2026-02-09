namespace TTMG.Commands
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandAttribute : Attribute
    {
        public string Code { get; }
        public string Action { get; }

        public CommandAttribute(string code, string action)
        {
            Code = code;
            Action = action;
        }
    }

    public interface ICommand
    {
        Task Execute(string[] args);
    }
}
