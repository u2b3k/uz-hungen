namespace UzHunGen.Converter;

// AFF faylda FLAG qiymati
public enum FlagType
{
    Long = 0,   // ikki harfli flaglar AA-zz
    Num = 1,    // raqamli flaglar
    UTF8 = 2,   // UTF8 belgili flaglar
}

// Dastur sozlamalari
public class AppSettings
{
    public bool Use2FoldRefs { get; set; }
    public bool UseMorphCodes { get; set; }
    public bool UseSingleDic { get; set; }
    public FlagType FlagType { get; set; } = FlagType.Long;
    public bool ShowHelp { get; set; }
    public bool ShowGrammar { get; set; }
    public List<string> RuleFiles { get; set; } = [];
    public List<string> DictionaryFiles { get; set; } = [];
    public string OutputFolder { get; set; } = "";
}

public class CommandLineParser
{
    public AppSettings Parse(string[] args)
    {
        var currentOption = "";
        var options = new AppSettings();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // - yoki -- bilan boshlansa
            if (arg.StartsWith('-'))
            {
                currentOption = arg.TrimStart('-');

                switch (currentOption)
                {
                    case "r":
                        options.Use2FoldRefs = true;
                        currentOption = null;
                        break;
                    case "p":
                        options.ShowGrammar = true;
                        currentOption = null;
                        break;
                    case "m":
                        options.UseMorphCodes = true;
                        currentOption = null;
                        break;
                    case "j":
                        options.UseSingleDic = true;
                        currentOption = null;
                        break;
                    case "h" or "help":
                        options.ShowHelp = true;
                        currentOption = null;
                        PrintHelp();
                        break;
                }
            }
            else if (currentOption != null)
            {
                if (currentOption == "s")
                    options.RuleFiles.Add(arg);
                else if (currentOption == "d")
                    options.DictionaryFiles.Add(arg);
                else if (currentOption == "o")
                    options.OutputFolder = arg;
            }
        }

        if (options.RuleFiles.Count <= 0 || options.DictionaryFiles.Count <= 0)
        {
            // default fayllar
            options.RuleFiles.Add(@".\Affixes\*.qoida");
            options.DictionaryFiles.Add(@".\Dictionaries\*.txt");
        }

        options.RuleFiles = CreateFileList(options.RuleFiles);

        options.DictionaryFiles = CreateFileList(options.DictionaryFiles);

        return options;
    }

    // Fayl nomlari maska bilan berilganda, masalan, "\Affixes\*.qoida" kabi
    // Ushbu maskaga mos keluvchi fayllar ro'yxatini yaratish
    private List<string> CreateFileList(List<string> files)
    {
        var realFiles = new List<string>();

        foreach (var file in files)
        {
            var result = Utils.SplitPatternAndDirectory(file);

            try
            {
                IEnumerable<string> foundFiles = Directory.EnumerateFiles(
                    result.Directory,
                    result.SearchPattern,
                    SearchOption.TopDirectoryOnly
                );
                realFiles.AddRange(foundFiles);
            }
            catch (Exception ex)
            {
                throw new IOException($"XATO ({result.SearchPattern}): {ex.Message}");
            }
        }
        return realFiles;
    }

    public void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Hunspell lug'at yaratish uchun asbob");
        Console.WriteLine();
        Console.WriteLine("Foydalanish: hungen.exe [sozlamalar]");
        Console.WriteLine();
        Console.WriteLine("Sozlamalar:");
        Console.WriteLine("  -s <files>     Qoida fayllarini yuklash");
        Console.WriteLine("  -d <files>     Lug'at fayllarini yuklash");
        Console.WriteLine("  -r             AFF faylda ikkitalik havolalardan foydalanish");
        Console.WriteLine("  -m             AFF faylga morfologik ma'lumotlarni qo'shish");
        Console.WriteLine("  -p             Parsing natijalarini chiqarish");
        Console.WriteLine("  -h, --help     Foydalanish yo'riqnomasi");
    }
}