using System.Text;

namespace UzHunGen.Converter;

public record SFXFlagItem
{
    public string Text { get; init; } = "";
    public string Condition { get; init; } = "";
    public string Strip { get; init; } = "";
    public string MorphCode { get; init; } = "";
    public string NextFlag { get; init; } = "";
}
public record SFXFlag
{
    public string TagName { get; init; } = "";
    public string SetName { get; init; } = "";
    public string FlagName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string MorphCode { get; init; } = "";
    public bool OnlyRoot { get; init; } = false;
    public List<SFXFlagItem> Lines { get; init; } = [];
}

public record AliasFlagItem (string ClassName = "", string FlagName = "", string SetName = "");

public record AliasFlag ()
{
    public string TagName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public int AliasIndex { get; init; } = 0;
    public string TextFlags { get; init; } = "";
    public List<AliasFlagItem> Flags { get; init; } = [];
}

public class HunspellConverter
{
    private int _uniqueId = 0;

    private AppSettings _options;

    private SuffixGrammar _grammar;

    private const string OutputAffPath = @".\Generated\uz.aff";

    private const string OutputDicPath = @".\Generated\uz.dic";

    public HunspellConverter(AppSettings options)
    {
        _options = options;

        _grammar = new SuffixGrammar
        {
            Suffixes = [],
            Tags = [],
            Words = []
        };

        AddRuleFiles(_options.RuleFiles);

        AddDictionaryFiles(_options.DictionaryFiles);

        ValidateTagReferences(_grammar);

        PrintGrammar();
    }

    private void AddRuleFile(string file)
    {
        var input = File.ReadAllText(file, Encoding.UTF8);

        var lexer = new RuleLexer(input);

        var tokens = lexer.Tokenize();

        var parser = new RuleParser(tokens);

        var singleGrammar = parser.Parse();

        foreach (var (key, value) in singleGrammar.Suffixes)
        {
            _grammar.Suffixes[key] = value;
        }

        foreach (var (key, value) in singleGrammar.Tags)
        {
            _grammar.Tags[key] = value;
        }

        Console.WriteLine($"- Qoidalar fayli yuklandi: {file}");
    }

    private void AddRuleFiles(List<string> files)
    {
        foreach (var file in files)
        {
            AddRuleFile(file);
        }

    }

    private void AddDictionaryFile(string file)
    {
        string[] lines = File.ReadAllLines(file);

        foreach (string line in lines)
        {
            var i = line.IndexOf('/');

            if (i < 0)
            {
                _grammar.Words.Add(new WordElement() { Word = line });
            }
            else
            {
                _grammar.Words.Add(new WordElement() { Word = line.Substring(0, i), Tag = line.Substring(i + 1).Trim() });
            }
        }

        Console.WriteLine($"- Lug'atlar fayli yuklandi: {file}");
    }

    private void AddDictionaryFiles(List<string> files)
    {
        foreach (var file in files)
        {
            AddDictionaryFile(file);
        }
    }

    // Qo'shimchalar to'plamlari mavjudligini tekshirish
    private static void ValidateTagReferences(SuffixGrammar grammar)
    {
        var missingSuffixes = grammar.Tags.Values
            .SelectMany(tag => tag.Elements)
            .SelectMany(element => element.Suffixes)
            .Where(sfx => !grammar.Suffixes.ContainsKey(sfx))
            .Distinct()
            .ToList();

        if (missingSuffixes.Count > 0)
            throw new InvalidOperationException(
                $"Quyidagi qo'shimchalar to'plami topilmadi: {string.Join(", ", missingSuffixes)}");
    }

    private void PrintGrammar()
    {
        if (_options.ShowGrammar)
        {
            foreach (var set in _grammar.Suffixes.Values)
            {
                Console.WriteLine($"\nSFX {set.Name}");

                foreach (var item in set.Elements)
                {
                    Console.WriteLine($"   {item.Id}: {item.Name} \"{item.Suffix}\" (Shart: \"{item.Condition.RegexPattern}\" Qirqish: {item.Condition.Strip})");
                }
            }

            foreach (var set in _grammar.Tags.Values)
            {
                Console.WriteLine($"\nTAG {set.Name}");

                foreach (var element in set.Elements)
                {
                    Console.WriteLine("   " + string.Join(" + ", element.Suffixes));
                }
            }
        }
    }

    
    // SFX flaglarni yaratish
    private List<SFXFlag> CreateSFXFlags()
    {
        var lastId = 0;

        var flag = 0;

        var list = new List<SFXFlag>();

        var tagName = "";

        foreach (var tag in _grammar.Tags.Values)
        {
            tagName = tag.Name;

            foreach (var tagItem in tag.Elements)
            {
                var result = new SuffixSet();

                foreach (var suffix in tagItem.Suffixes)
                {
                    result = JoinSuffixSets(result, _grammar.Suffixes[suffix]);
                }

                lastId = 0;

                var sfx = new SFXFlag();

                foreach (var item in result.Elements)
                {
                    if (lastId != item.Id)
                    {
                        if (sfx.Lines.Count > 0) list.Add(sfx);

                        flag++;

                        sfx = new SFXFlag()
                        {
                            FlagName = Utils.CreateLongFlag(flag),
                            TagName = tagName,
                            SetName = item.SetName,
                            ClassName = item.Class,
                            MorphCode = item.MorphCode,
                            OnlyRoot = item.OnlyRoot
                        };

                        lastId = item.Id;
                    }

                    sfx.Lines.Add(new SFXFlagItem()
                    {
                        Text = item.Suffix,
                        Condition = item.Condition.RegexPattern.Length > 0 ? item.Condition.RegexPattern : ".",
                        Strip = item.Condition.Strip.Length > 0 ? item.Condition.Strip : "0",
                        MorphCode = item.MorphCode
                    });
                }
                if (sfx.Lines.Count > 0) list.Add(sfx);

            }
        }

        return list;

    }

    // Ikkita to'plamdagi qo'shimchalarni biriktirish
    public SuffixSet JoinSuffixSets(SuffixSet set1, SuffixSet set2)
    {
        var result = new SuffixSet();

        var lastId = 0;

        var lastId2 = 0;

        if (set1.Elements.Count <= 0)
        {
            foreach (var item1 in set2.Elements)
            {
                //if (className.Length > 0 && !item1.Class.Equals(className)) continue;

                if (item1.Id != lastId)
                {
                    lastId = item1.Id;
                    _uniqueId++;
                }

                result.Elements.Add(item1 with { Id = _uniqueId });
            }
        }
        else
        {
            foreach (var item1 in set1.Elements)
            {
                //if (className.Length > 0 && !item1.Class.Equals(className)) continue;

                if (item1.Id != lastId2)
                {
                    lastId2 = item1.Id;
                    _uniqueId++;
                }
                foreach (var item2 in set2.Elements)
                {
                    if (item2.Class.Length > 0) continue;

                    if (Utils.SimpleRegexMatchEnd(item1.Suffix, item2.Condition.RegexPattern))
                    {
                        var suffix = item1.Suffix;

                        if (item2.Condition.Strip.Equals("!"))
                        {
                            var length = Utils.SimpleRegexLength(item2.Condition.RegexPattern);
                            if (length > 0)
                                suffix = suffix.Substring(0, suffix.Length - length);
                        }
                        else if (item2.Condition.Strip.Length > 0)
                        {
                            suffix = suffix.Substring(0, suffix.Length - item2.Condition.Strip.Length);
                        }

                        if (item2.Suffix.Length == 0 || item2.OnlyRoot) continue;

                        if (item2.Id != lastId)
                        {
                            lastId = item2.Id;
                            _uniqueId++;
                        }

                        result.Elements.Add(new SuffixElement()
                        {
                            Id = _uniqueId,
                            Name = $"{item1.Name}+{item2.Name}",
                            SetName = item1.SetName,
                            Suffix = suffix + item2.Suffix,
                            OnlyRoot = item1.OnlyRoot,
                            Class = item1.Class,
                            MorphCode = $"{item1.MorphCode}:{item2.MorphCode}",
                            Condition = item1.Condition
                        });
                    }
                }
            }
        }

        return result;
    }

    // AF - alias flaglarini yaratish
    private Dictionary<string, AliasFlag> CreateAliasFlags(List<SFXFlag> sfxList)
    {
        var tagAliases = new Dictionary<string, AliasFlag>();

        foreach (var sfx in sfxList)
        {
            var s1 = sfx.TagName;

            if (tagAliases.ContainsKey(s1))
            {
                tagAliases[s1].Flags.Add(new AliasFlagItem() { ClassName = sfx.ClassName, FlagName = sfx.FlagName, SetName = sfx.SetName });
            }
            else
            {
                var a = new AliasFlag()
                    {
                        TagName = s1,
                        Flags = new()
                        {
                            new AliasFlagItem() { ClassName = sfx.ClassName, FlagName = sfx.FlagName, SetName = sfx.SetName }
                        }
                    };

                tagAliases.Add(s1, a);
            }
        }

        return SplitAliasesByClass(tagAliases);
    }

    // Aliaslarni class bo'yicha ajratib yuborish
    private Dictionary<string, AliasFlag> SplitAliasesByClass(Dictionary<string, AliasFlag> afList)
    {
       
        var newList = new Dictionary<string, AliasFlag>();
        
        var aliasIndex = 0;

        foreach (var (key, entry) in afList)
        {
            var groupedFlags = entry.Flags.GroupBy(f => f.ClassName);

            foreach (var group in groupedFlags)
            {
                var className = group.Key;

                var sb = new StringBuilder();

                var setName = "";

                foreach (var f in group)
                {
                    sb.Append(f.FlagName);

                    setName = f.SetName;
                }

                if (className.Length == 0)
                {
                    newList.Add(entry.TagName, new AliasFlag() { AliasIndex = ++aliasIndex, TextFlags = sb.ToString(), TagName = entry.TagName });
                }
                else
                {
                    var g = entry.Flags.Where(f => f.SetName != setName);

                    foreach (var f in g)
                    {
                        sb.Append(f.FlagName);
                    }

                    newList.Add(entry.TagName + className, new AliasFlag() { AliasIndex = ++aliasIndex, ClassName = className, TextFlags = sb.ToString(), TagName = entry.TagName + className });
                }
            }
        }

        return newList;
    }

    // Morfologik kodlarni aliaslarini yaratish
    private Dictionary<string, int> CreateMorphAliases(List<SFXFlag> sfxList)
    {
        var list = new Dictionary<string, int>();
        
        var index = 0;

        foreach (var sfx in sfxList)
        {
            foreach (var item in sfx.Lines)
            {
                if (!list.ContainsKey(item.MorphCode)) list.Add(item.MorphCode, ++index);
            }
        }

        return list;
    }

    // AFF fayl yaratish
    private void WriteToAFFFile(List<SFXFlag> sfxList, Dictionary<string, AliasFlag> afList, Dictionary<string, int> morphList)
    {

        var sb = new StringBuilder();

        sb.AppendLine("LANG uz_UZ");
        sb.AppendLine("SET UTF-8");
        sb.AppendLine("FLAG long");
        sb.AppendLine("WORDCHARS -‘");
        sb.AppendLine();

        // Alias larni faylga yozish
        // AF <count>
        // AF Flags1
        // AF Flags2
        sb.AppendLine("AF " + afList.Count);

        foreach (var (key, entry) in afList)
        {
            sb.AppendLine($"AF {entry.TextFlags} # {entry.AliasIndex} {entry.TagName}");

            if (_options.ShowGrammar) Console.WriteLine(entry.AliasIndex + ": " + entry.TagName + " = " + entry.TextFlags);
        }
        sb.AppendLine();

        // Morph aliaslarni faylga yozish
        // AM <count>
        // AM morphcode1 
        // AM morphcode2
        if (morphList.Count > 0 && _options.UseMorphCodes)
        {
            sb.AppendLine("AM " + morphList.Count);

            foreach (var (key, entry) in morphList)
            {
                sb.AppendLine($"AM {key}");
            }
            sb.AppendLine();
        }


        foreach (var sfx in sfxList)
        {
            sb.AppendLine($"# {sfx.TagName}{sfx.ClassName}/{sfx.SetName}{sfx.ClassName}" + (sfx.MorphCode.Length > 0 && !sfx.MorphCode.Equals("_") ? " : " + sfx.MorphCode : ""));
            sb.AppendLine($"SFX {sfx.FlagName} Y {sfx.Lines.Count}");

            foreach (var item in sfx.Lines)
            {
                var morphIndex = 0;

                if (_options.UseMorphCodes && morphList.ContainsKey(item.MorphCode)) morphIndex = morphList[item.MorphCode];

                sb.AppendLine($"SFX {sfx.FlagName} {item.Strip} {item.Text} {item.Condition}" + (morphIndex > 0 ? $" {morphIndex}" : ""));
            }

            sb.AppendLine();
        }

        File.WriteAllText(OutputAffPath, sb.ToString());

    }

    // DIC fayl yaratish
    private void WriteToDICFile(Dictionary<string, AliasFlag> aliasList)
    {
        var sb = new StringBuilder();

        foreach (var word in _grammar.Words)
        {
            if (word.Tag.Length > 0)
            {
                var tags = word.Tag.Split("/");
                var ff = new StringBuilder();
                foreach (var tag in tags)
                {
                    if (aliasList.ContainsKey(tag))
                    {
                        ff.Append(aliasList[tag].AliasIndex);
                        ff.Append('/');
                    }
                }
                var flags = ff.ToString();
                if (flags.Length > 0) flags = flags.Substring(0, flags.Length - 1);
                sb.AppendLine(word.Word + "/" + flags);
            }
            else
            {
                sb.AppendLine(word.Word);
            }
        }
        sb.Insert(0, _grammar.Words.Count + "\n");

        File.WriteAllText(OutputDicPath, sb.ToString());
    }

    public void Convert()
    {
        var sfxList = CreateSFXFlags();

        var afList = CreateAliasFlags(sfxList);

        var morphList = CreateMorphAliases(sfxList);

        WriteToAFFFile(sfxList, afList, morphList);

        WriteToDICFile(afList);
    }

}
