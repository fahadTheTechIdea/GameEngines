using GameContent.Generator;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("usage: GameContent.Generator <packs folder>   (writes the generated packs into the folder)");
    return 2;
}
foreach (var pack in PackGenerator.Generate(args[0])) Console.WriteLine("wrote " + pack);
return 0;
