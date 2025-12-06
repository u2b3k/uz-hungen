using System.Text;

namespace Test.Generator;

public class SimpleRegex
{
    // O'zbek tilining unli harflari (lotin alifbosida)
    private static readonly string[] UNLI1 = { "a", "e", "i", "o", "u" };
    private static readonly string[] UNLI2 = { "o‘" };

    // O'zbek tilining undosh harflari (lotin alifbosida)
    private static readonly string[] UNDOSH1 = {
        "b", "d", "f", "g", "h", "j", "k", "l", "m",
        "n", "p", "q", "r", "s", "t", "v", "x", "y", "z"
    };
    private static readonly string[] UNDOSH2 = { "sh", "ch", "g‘" };

    /// <summary>
    /// Oddiy regex naqshni Hunspell SFX condition formatiga o'tkazadi.
    /// 
    /// Qo'llab-quvvatlanadigan amallar:
    /// - [] - belgilar to'plami
    /// - | - yoki
    /// - ^ - inkor (belgilar to'plami, guruh yoki bitta harf uchun)
    /// - . - ihtiyoriy belgi
    /// - () - guruhlash
    /// - {UNLI} - o'zbek tilidagi unli harflar
    /// - {UNDOSH} - o'zbek tilidagi undosh harflar
    /// 
    /// Qaytaradi: Hunspell uchun oddiy regex naqshlar ro'yxati
    /// </summary>
    public static List<string> Convert(string pattern)
    {
        var result = ParsePattern(pattern, 0, out _);
        return result;
    }

    private static List<string> ParsePattern(string s, int startPos, out int endPos)
    {
        var variants = new List<string> { "" };
        int i = startPos;

        while (i < s.Length)
        {
            char c = s[i];

            if (c == '|')
            {
                // "Yoki" amali - chapni saqlab, o'ngni tahlil qilamiz
                var leftVariants = new List<string>(variants);
                var rightVariants = ParsePattern(s, i + 1, out endPos);

                variants = leftVariants.Concat(rightVariants).ToList();
                i = endPos;
                break;
            }
            else if (c == '{')
            {
                // {UNLI} yoki {UNDOSH} ni topamiz
                int j = i + 1;
                while (j < s.Length && s[j] != '}')
                {
                    j++;
                }

                if (j < s.Length)
                {
                    string macroName = s.Substring(i + 1, j - i - 1);

                    if (macroName == "UNLI")
                    {
                        // Unli harflarni qo'shamiz
                        var newVariants = new List<string>();
                        foreach (var v in variants)
                        {
                            newVariants.Add(v + "[" + string.Join("", UNLI1) + "]");
                        }
                        variants = newVariants;
                    }
                    else if (macroName == "UNDOSH")
                    {
                        // Undosh harflarni qo'shamiz
                        var newVariants = new List<string>();
                        foreach (var v in variants)
                        {
                            newVariants.Add(v + "[" + string.Join("", UNDOSH1) + "]");
                        }
                        variants = newVariants;
                    }

                    i = j + 1;
                }
                else
                {
                    i++;
                }
            }
            else if (c == '(')
            {
                // Guruhlashni topamiz
                int depth = 1;
                int j = i + 1;

                while (j < s.Length && depth > 0)
                {
                    if (s[j] == '(') depth++;
                    else if (s[j] == ')') depth--;
                    j++;
                }

                string groupContent = s.Substring(i + 1, j - i - 2);
                var groupVariants = ParsePattern(groupContent, 0, out _);

                // Har bir mavjud variant uchun guruh variantlarini qo'shamiz
                var newVariants = new List<string>();
                foreach (var v in variants)
                {
                    foreach (var gv in groupVariants)
                    {
                        newVariants.Add(v + gv);
                    }
                }
                variants = newVariants;
                i = j;
            }
            else if (c == '[')
            {
                // Belgilar to'plamini topamiz
                int j = i + 1;
                bool isNegated = false;

                if (j < s.Length && s[j] == '^')
                {
                    isNegated = true;
                    j++;
                }

                while (j < s.Length && s[j] != ']')
                {
                    j++;
                }

                string charset;
                if (isNegated)
                {
                    charset = s.Substring(i, j - i + 1); // [^...] shaklida
                }
                else
                {
                    charset = s.Substring(i, j - i + 1); // [...] shaklida
                }

                // Har bir variantga belgilar to'plamini qo'shamiz
                variants = variants.Select(v => v + charset).ToList();
                i = j + 1;
            }
            else if (c == '^' && i + 1 < s.Length)
            {
                // Inkor amali - keyingi elementni inkor qilish
                i++;

                // {UNLI} yoki {UNDOSH} inkorini tekshiramiz
                if (s[i] == '{')
                {
                    int j = i + 1;
                    while (j < s.Length && s[j] != '}')
                    {
                        j++;
                    }

                    if (j < s.Length)
                    {
                        string macroName = s.Substring(i + 1, j - i - 1);

                        if (macroName == "UNLI")
                        {
                            // Unli harflardan boshqa barcha harflar = undosh harflar
                            var newVariants = new List<string>();
                            foreach (var v in variants)
                            {
                                newVariants.Add(v + "[" + string.Join("", UNDOSH1) + "]");
                            }
                            variants = newVariants;
                        }
                        else if (macroName == "UNDOSH")
                        {
                            // Undosh harflardan boshqa barcha harflar = unli harflar
                            var newVariants = new List<string>();
                            foreach (var v in variants)
                            {
                                newVariants.Add(v + "[" + string.Join("", UNLI1) + "]");
                            }
                            variants = newVariants;
                        }

                        i = j + 1;
                        continue;
                    }
                }

                char nextChar = s[i];

                if (nextChar == '[')
                {
                    // To'plamni inkor qilish
                    int j = i + 1;
                    if (j < s.Length && s[j] == '^')
                    {
                        // [^...] shaklida allaqachon inkorlangan
                        while (j < s.Length && s[j] != ']') j++;
                        string charset = s.Substring(i, j - i + 1);
                        variants = variants.Select(v => v + charset).ToList();
                        i = j + 1;
                    }
                    else
                    {
                        // [...] ni [^...] ga aylantiramiz
                        while (j < s.Length && s[j] != ']') j++;
                        string charsetContent = s.Substring(i + 1, j - i - 1);
                        string negatedCharset = "[^" + charsetContent + "]";
                        variants = variants.Select(v => v + negatedCharset).ToList();
                        i = j + 1;
                    }
                }
                else if (nextChar == '(')
                {
                    // Guruhdagi har bir variantni inkor qilish kerak
                    int depth = 1;
                    int j = i + 1;
                    while (j < s.Length && depth > 0)
                    {
                        if (s[j] == '(') depth++;
                        else if (s[j] == ')') depth--;
                        j++;
                    }

                    string groupContent = s.Substring(i + 1, j - i - 2);
                    var groupVariants = ParsePattern(groupContent, 0, out _);

                    // Har bir guruh variantini inkor qilish
                    var negatedGroupVariants = groupVariants.Select(gv => NegatePattern(gv)).ToList();

                    var newVariants = new List<string>();
                    foreach (var v in variants)
                    {
                        foreach (var ngv in negatedGroupVariants)
                        {
                            newVariants.Add(v + ngv);
                        }
                    }
                    variants = newVariants;
                    i = j;
                }
                else if (nextChar == '.')
                {
                    // . ni inkor qilish
                    variants = variants.Select(v => v + "[^.]").ToList();
                    i++;
                }
                else
                {
                    // Bitta belgini inkor qilish
                    string negated = "[^" + nextChar + "]";
                    variants = variants.Select(v => v + negated).ToList();
                    i++;
                }
            }
            else if (c == '.')
            {
                // Ihtiyoriy belgi
                variants = variants.Select(v => v + ".").ToList();
                i++;
            }
            else if (c == ')')
            {
                // Guruh tugadi
                endPos = i;
                return variants;
            }
            else
            {
                // Oddiy belgi
                variants = variants.Select(v => v + c).ToList();
                i++;
            }
        }

        endPos = i;
        return variants;
    }

    private static string NegatePattern(string pattern)
    {
        // Oddiy naqshni inkor qilish
        if (pattern.Length == 1)
        {
            return "[^" + pattern + "]";
        }
        else if (pattern.StartsWith("[^") && pattern.EndsWith("]"))
        {
            // [^...] -> [...]
            return "[" + pattern.Substring(2, pattern.Length - 3) + "]";
        }
        else if (pattern.StartsWith("[") && pattern.EndsWith("]"))
        {
            // [...] -> [^...]
            return "[^" + pattern.Substring(1);
        }
        else if (pattern.Length == 2)
        {
            // Ikkita harfli harf (sh, ch, o', g')
            return "[^" + pattern + "]";
        }
        else
        {
            // Murakkab naqsh
            var sb = new StringBuilder();
            int i = 0;
            while (i < pattern.Length)
            {
                // Ikkita harfli harflarni tekshirish
                if (i + 1 < pattern.Length)
                {
                    string twoChar = pattern.Substring(i, 2);
                    if (UNDOSH2.Contains(twoChar) || UNLI2.Contains(twoChar))
                    {
                        sb.Append("[^" + twoChar + "]");
                        i += 2;
                        continue;
                    }
                }
                sb.Append("[^" + pattern[i] + "]");
                i++;
            }
            return sb.ToString();
        }
    }
}