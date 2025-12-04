using System.Text;

namespace UzHunGen.Converter;


// Qo'shimchani berishdagi shartlar
public record AffixCondition
{
    // Shart turi
    public enum ConditionType
    {
        None,
        StartsWith,
        EndsWith
    }

    public ConditionType Type { get; set; } = ConditionType.EndsWith;
    public string RegexPattern { get; set; } = ".";
    public string Strip { get; set; } = "";

    public bool UseRegex => !string.IsNullOrEmpty(RegexPattern);
}

// Qo'shimchani saqlash uchun
public record SuffixElement
{
    public int Id { get; set; } = 0;
    public string Name { get; set; } = "";
    public string SetName { get; set; } = "";
    public string Suffix { get; set; } = "";
    public string MorphCode { get; set; } = "";
    public string Class { get; set; } = "";
    public bool OnlyRoot { get; set; }
    public AffixCondition Condition { get; set; } = new();
}

// Qo'shimchalar to'plami
public record SuffixSet
{
    public string Name { get; init; } = "";
    public string MorphCode { get; init; } = "";
    public bool HasClass { get; init; }
    public List<SuffixElement> Elements { get; init; } = [];
}

// Qoida ichidagi element
public record TagElement
{
    public string Name { get; init; } = "";
    public string MorphCode { get; init; } = "";
    public string Text { get; set; } = "";
    public List<string> Suffixes { get; init; } = [];
}

// Qoidani saqlash uchun
public record TagSet
{
    public string Name { get; init; } = "";
    public string MorphCode { get; init; } = "";
    public List<TagElement> Elements { get; init; } = [];
}

// So'zlarni saqlash uchun
public record WordElement
{
    public string Word { get; init; } = "";
    public string Tag { get; init; } = "";
}

public record TagAlternative
{
    public List<string> Items { get; init; } = [];
    public bool IsOptional { get; set; }
}

public record SuffixGrammar
{
    public Dictionary<string, SuffixSet> Suffixes { get; set; } = [];
    public Dictionary<string, TagSet> Tags { get; set; } = [];
    public List<WordElement> Words { get; set; } = [];
}

// Parser
public class RuleParser
{
    private readonly List<Token> _tokens;
    private int _position;
    private int _uniqueId;

    public RuleParser(List<Token> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _tokens = tokens;
    }

    private Token CurrentToken => _position < _tokens.Count ? _tokens[_position] : _tokens.Last();

    public SuffixGrammar Parse()
    {
        var grammar = new SuffixGrammar();

        while (CurrentToken.Type != TokenType.EOF)
        {
            if (CurrentToken.Type == TokenType.NEWLINE)
            {
                Consume(TokenType.NEWLINE);
            }
            else if (CurrentToken.Type == TokenType.SFX)
            {
                var suffixSet = ParseSuffixSet();

                grammar.Suffixes[suffixSet.Name] = suffixSet;
            }
            else if (CurrentToken.Type == TokenType.TAG)
            {
                var tagSet = ParseTagSet();

                grammar.Tags[tagSet.Name] = tagSet;
            }
            else
            {
                throw new InvalidOperationException($"SFX, PFX yoki TAG berilishi kerak, biroq {CurrentToken.Type} berilgan, qator nomeri => {CurrentToken.Line}:{CurrentToken.Column}");
            }
        }

        return grammar;
    }


    private SuffixSet ParseSuffixSet()
    {
        Consume(TokenType.SFX);

        var name = Consume(TokenType.IDENTIFIER).Value;
        var morph = name;

        // Qo'shimcha izohi
        if (CurrentToken.Type == TokenType.COLON)
        {
            Consume(TokenType.COLON);
            morph = Consume(TokenType.STRING).Value;
        }

        Consume(TokenType.NEWLINE);

        var elements = new List<SuffixElement>();

        var condition = new AffixCondition();

        var className = "";

        var onlyRoot = false;

        var hasClass = false;

        _uniqueId++;

        while (CurrentToken.Type != TokenType.END && CurrentToken.Type != TokenType.EOF)
        {
            a1:

            if (CurrentToken.Type == TokenType.LBRACKET)
            {
                Consume(TokenType.LBRACKET);

                if (CurrentToken.Type == TokenType.CLASS)
                {
                    if (elements.Count > 0) _uniqueId++;

                    Consume(TokenType.CLASS);

                    if (CurrentToken.Type == TokenType.IDENTIFIER)
                    {
                        className = Consume(TokenType.IDENTIFIER).Value;
                        hasClass = true;
                    }
                    else
                        className = "";

                    if (CurrentToken.Type == TokenType.ONLYROOT)
                    {
                        Consume(TokenType.ONLYROOT);
                        onlyRoot = true;
                    } else { onlyRoot = false; }

                    Consume(TokenType.RBRACKET);
                    Consume(TokenType.NEWLINE);
                    goto a1;
                }

                Consume(TokenType.ENDSWITH);

                condition = new AffixCondition();
                condition.Type = AffixCondition.ConditionType.EndsWith;
                condition.RegexPattern = Consume(TokenType.STRING).Value;

                // STRIP ni berish majburiy emas
                if (CurrentToken.Type == TokenType.STRIP)
                {
                    Consume(TokenType.STRIP);

                    if (CurrentToken.Type == TokenType.STRING)
                        condition.Strip = Consume(TokenType.STRING).Value;
                    else
                        condition.Strip = condition.RegexPattern;
                }
                
                Consume(TokenType.RBRACKET);
                Consume(TokenType.NEWLINE);
            }

            var element = ParseSuffixElement();
            element.Id = _uniqueId;
            element.SetName = name;
            element.Condition = condition;
            element.OnlyRoot = onlyRoot;
            element.Class = className;

            elements.Add(element);
            
            Consume(TokenType.NEWLINE);
        }

        Consume(TokenType.END);
        Consume(TokenType.SFX);

        return new SuffixSet
        {
            Name = name,
            MorphCode = morph,
            HasClass = hasClass,
            Elements = elements
        };
    }

    private SuffixElement ParseSuffixElement()
    {
        var name = Consume(TokenType.IDENTIFIER).Value;
        var morph = name;

        var elements = new List<SuffixElement>();

        // Qo'shimcha izohi
        if (CurrentToken.Type == TokenType.COLON)
        {
            Consume(TokenType.COLON);
            morph = Consume(TokenType.STRING).Value;
        }

        Consume(TokenType.EQUAL);

        var suffix = Consume(TokenType.STRING).Value;

        return new SuffixElement
        {
            Name = name,
            Suffix = suffix,
            MorphCode = morph
        };
    }

    private TagSet ParseTagSet()
    {
        Consume(TokenType.TAG);

        var name = Consume(TokenType.IDENTIFIER).Value;
        var morph = name;

        // Qoida izohi
        if (CurrentToken.Type == TokenType.COLON)
        {
            Consume(TokenType.COLON);
            morph = Consume(TokenType.STRING).Value;
        }

        Consume(TokenType.NEWLINE);

        var elements = new List<TagElement>();

        while (CurrentToken.Type != TokenType.END && CurrentToken.Type != TokenType.EOF)
        {
            var element = ParseTagElement();

            elements.AddRange(element);

            Consume(TokenType.NEWLINE);
        }

        Consume(TokenType.END);
        Consume(TokenType.TAG);

        return new TagSet
        {
            Name = name,
            MorphCode = morph,
            Elements = elements.DistinctBy(t => t.Text).ToList()
        };
    }

    private List<TagElement> ParseTagElement()
    {
        var name = Consume(TokenType.IDENTIFIER).Value;
        var morph = name;

        var elements = new List<TagElement>();

        // Qo'shimcha izohi
        if (CurrentToken.Type == TokenType.COLON)
        {
            Consume(TokenType.COLON);
            morph = Consume(TokenType.STRING).Value;
        }

        Consume(TokenType.EQUAL);

        var expression = new List<TagAlternative>();


        while (CurrentToken.Type != TokenType.NEWLINE && CurrentToken.Type != TokenType.EOF)
        {
            if (CurrentToken.Type == TokenType.IDENTIFIER)
            {
                var refName = Consume(TokenType.IDENTIFIER).Value;

                var alter = new TagAlternative();
                alter.Items.Add(refName);
                alter.IsOptional = false;
                expression.Add(alter);
            }
            else if (CurrentToken.Type == TokenType.LBRACE)
            {
                // {Egalik, ...}
                Consume(TokenType.LBRACE);

                var alter = new TagAlternative();

                while (CurrentToken.Type == TokenType.IDENTIFIER)
                {
                    var refName = Consume(TokenType.IDENTIFIER).Value;
                    alter.Items.Add(refName);
                    alter.IsOptional = false;
                    if (CurrentToken.Type == TokenType.COMMA) Consume(TokenType.COMMA);
                }
                Consume(TokenType.RBRACE);

                expression.Add(alter);
            }
            else if (CurrentToken.Type == TokenType.LBRACKET)
            {
                // [Egalik, ...]
                Consume(TokenType.LBRACKET);

                var alter = new TagAlternative();

                while (CurrentToken.Type == TokenType.IDENTIFIER)
                {
                    var refName = Consume(TokenType.IDENTIFIER).Value;
                    alter.Items.Add(refName);
                    alter.IsOptional = true;
                    if (CurrentToken.Type == TokenType.COMMA) Consume(TokenType.COMMA);
                }
                Consume(TokenType.RBRACKET);

                expression.Add(alter);
            }

            if (CurrentToken.Type != TokenType.PLUS) break;

            Consume(TokenType.PLUS);
        }

        // TAG qatorini yoyib yuborish
        if (expression.Count > 0)
        {
            var indexes = new int[expression.Count];

            for (int i = 0; i < expression.Count; i++)
            {
                if (expression[i].IsOptional) indexes[i] = 0; else indexes[i] = 1;
            }

            var stop = false;

            while (!stop)
            {
                var line = new StringBuilder();
                var tag = new TagElement() { Name = name, MorphCode = morph };

                for (int i = 0; i < expression.Count; i++)
                {
                    if (indexes[i] > 0)
                    {
                        tag.Suffixes.Add(expression[i].Items[indexes[i] - 1]);
                        line.Append(expression[i].Items[indexes[i] - 1]);
                        line.Append('+');
                    }
                }

                if (line.Length > 0) line.Length = line.Length - 1;

                tag.Text = line.ToString();

                if (line.Length > 0) elements.Add(tag);

                int q = 0;
                while (true)
                {
                    if (q >= expression.Count)
                    {
                        stop = true;
                        break;
                    }
                    indexes[q]++;
                    if (indexes[q] > expression[q].Items.Count)
                    {
                        if (q == expression.Count)
                        {
                            stop = true;
                            break;
                        }
                        indexes[q] = expression[q].IsOptional ?  0 : 1;
                        q++;
                        continue;
                    }
                    break;
                };

            };
        }

        return elements;
    }

    private Token Consume(TokenType expectedType)
    {
        if (CurrentToken.Type != expectedType)
            throw new InvalidOperationException($"{expectedType} berilishi kerak, biroq {CurrentToken.Type} berilgan, qator nomeri => {CurrentToken.Line}:{CurrentToken.Column}");

        return _tokens[_position++];
    }
}
