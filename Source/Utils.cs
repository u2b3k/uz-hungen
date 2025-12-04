namespace UzHunGen.Converter;

public class Utils
{
    private static readonly string ValidLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static (string Directory, string SearchPattern) SplitPatternAndDirectory(string fullPattern)
    {
        var directory = Path.GetDirectoryName(fullPattern);

        var searchPattern = Path.GetFileName(fullPattern);

        if (string.IsNullOrEmpty(directory))
        {
            directory = ".";
        }

        return (directory, searchPattern);
    }

    // Raqamdan ikki harfli long flag yaratish
    public static string CreateLongFlag(int num)
    {
        // 52*52 gacha (AA-zz)
        if (num < 0 || num > 2704)
        {
            throw new ArgumentOutOfRangeException(
                nameof(num),
                $"Qiymat {nameof(num)} 0 dan {char.MaxValue} gacha bo'lishi kerak."
            );
        }
        var length = ValidLetters.Length;
        var a = (num - 1) / length;
        var b = (num - 1) % length;
        return $"{ValidLetters[a]}{ValidLetters[b]}";
    }

    // Raqamdan utf8 flag yaratish
    public static char CreateUTF8Flag(int num)
    {
        // 65535 gacha
        if (num < 0 || num > char.MaxValue) 
        {
            throw new ArgumentOutOfRangeException(
                nameof(num),
                $"Qiymat {nameof(num)} 0 dan {char.MaxValue} gacha bo'lishi kerak."
            );
        }

        return (char)num;
    }

    public static bool SimpleRegexMatchEnd(string text, string condition)
    {
        if (condition == ".") return true;

        var length = SimpleRegexLength(condition);

        if (length == 0) return true;

        if (length > text.Length) return false;
        
        return SimpleRegexMatch(text.Substring(text.Length - length), condition);
    }


    private static bool SimpleRegexMatch(string text, string condition)
    {
        if (condition == ".") return true;

        var posText = 0;
        var posPattern = 0;
        var negate = false;
        var ingroup = false;
        var groupBegin = 0;
        var lenText = text.Length;
        var lenPattern = condition.Length;

        char ch = text[posText];

        while (posPattern < lenPattern && posText < lenText)
        {
            switch (condition[posPattern])
            {
                case '[':

                    negate = false;

                    ingroup = true;

                    groupBegin = posPattern + 1;

                    break;

                case ']':

                    if (posPattern > groupBegin)
                    {
                        string group = condition.Substring(groupBegin, posPattern - groupBegin);

                        if (negate == group.Contains(ch))
                        {
                            return false;
                        }

                        posText++;

                        if (posText < lenText) ch = text[posText];
                    }

                    ingroup = negate = false;

                    break;

                case '^':

                    negate = true;

                    break;

                case '.':

                    //negate = false;

                    posText++;

                    if (posText < lenText) ch = text[posText];

                    break;

                default:

                    if (!ingroup)
                    {
                        if (condition[posPattern] == '.' || !negate == ch.Equals(condition[posPattern]))
                        {
                            //negate = false;

                            posText++;

                            if (posText < lenText) ch = text[posText];
                        }
                        else
                        {
                            return false;
                        }
                    }
                    break;
            }

            posPattern++;

        }

        return posPattern == lenPattern && posText == lenText;

    }

    // Regex patterndan uning haqiqiy uzunligini aniqlash
    // Masalan: [aiou]moq => 4 ta harf
    // Suffikslarda qirqib tashlanadigan harflar sonini avtoaniqlashda ishlaydi
    public static int SimpleRegexLength(string condition)
    {
        if (condition.Length == 0) return 0;

        var count = 0;

        var ingroup = false;

        foreach (var ch in condition)
        {
            switch (ch)
            {
                case '[':

                    ingroup = true;
                    break;

                case ']':

                    ingroup = false;

                    count++;

                    break;

                case '^':

                    break;

                default:

                    if (!ingroup) count++;

                    break;
            }
        }

        return count;
    }

    
}
