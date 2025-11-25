namespace BbQ.CQRS.Samples;

public readonly record struct Unit;

public static class UnitExtensions
{
   extension(Unit)
   {
       public static Unit Value => new();
    }
}
