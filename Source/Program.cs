using UzHunGen.Converter;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var options = new CommandLineParser().Parse(args);

var converter = new HunspellConverter(options);

converter.Convert();