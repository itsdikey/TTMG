using TTMG.ConsoleEditor;

if (args.Length > 0)
{
    var editor = new Editor(args[0]);
    editor.Run();
}
else
{
    var editor = new Editor();
    editor.Run();
}
