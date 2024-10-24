namespace DataImport.CommandLine.Commands;

public abstract class BaseCommand
{
    public bool Successful { get; protected set; }

    public abstract void Execute();
}