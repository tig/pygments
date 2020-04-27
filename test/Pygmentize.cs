using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Pyg {
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using FearTheCowboy.Pygments;

    public static class Extensions {
        public static bool ContainsAny<T>(this IEnumerable<T> collection, params T[] items) {
            return collection.Any(items.Contains);
        }

        public static string Value(this IEnumerable<string> options, string optionName, string defaultValue) {
            var v = Value(options, optionName);
            if (string.IsNullOrEmpty(v)) {
                return defaultValue;
            }
            return v;
        }


        public static string Value(this IEnumerable<string> options, string optionName) {
            optionName = optionName + "=";
            return (options.LastOrDefault(each => each.StartsWith(optionName)) ?? optionName).Substring(optionName.Length);
        }

        public static string[] Values(this IEnumerable<string> options, string optionName) {
            optionName = optionName + "=";
            var p = optionName.Length;
            return (options.Where(each => each.StartsWith(optionName)).Select(each => each.Substring(p))).ToArray();
        }

        public static string[] Values(this IEnumerable<string> options, string optionName, string defaultValue) {
            var v = Values(options, optionName);
            if (v == null || !v.Any()) {
                return new[] {defaultValue};
            }
            return v;
        }
    }



    class Pygmentize {
        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        static void SetAnsiConsoleMode()
        {
            var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
            if (!GetConsoleMode(iStdOut, out uint outConsoleMode))
            {
                Console.WriteLine("failed to get output console mode");
                return;
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
            if (!SetConsoleMode(iStdOut, outConsoleMode))
            {
                Console.WriteLine($"failed to set output console mode, error code: {GetLastError()}");
                return;
            }
        }

        static void Help() {
            Console.WriteLine("pygmentize --console [--liststyles | [--output=[html, bbcode, rtf, latex, terminal256] --style=<style> files]");
        }

        static void ListStyles()
        {
            var highlighter = new Highlighter();
            Console.WriteLine(string.Join(", ", highlighter.Styles.ToArray()));
        }


        static void Main(string[] args)
        {
            var opts = args.Where(each => each.StartsWith("--")).Select( each=> each.TrimStart('-').ToLower()).ToArray();
            var files = args.Where(each => !each.StartsWith("--")).ToArray();
            bool writeToConsole = false;

            if (files.Length == 0 && opts.Contains("liststyles"))
            {
                ListStyles();
                return;
            }

            if (files.Length == 0 || opts.ContainsAny("help", "?", "h" )) {
                Help();
                return;
            }

            writeToConsole = opts.ContainsAny("console", "c");

            var language = opts.Value("language",null);
            var style = opts.Value("style","scite") ;
            var outputs = opts.Values("output", "html").Distinct().ToArray();

            var highlighter = new Highlighter();
            

            foreach (var o in outputs) {
                switch (o) {
                    case "html":
                        try {
                            foreach (var f in files) {
                                Console.WriteLine("Highlighting : [{0}] to [{0}.html]" , f);

                                File.WriteAllText(f + ".html", highlighter.HighlightToHtml(File.ReadAllText(f), language, style, f, preStyles: "font-family: consolas, courier", generateInlineStyles: true));
                            }
                        }
                        catch (Exception e) {
                            Console.Error.WriteLine("{0},{1}/{2}", e.Message, e.GetType().Name, e.StackTrace);
                        }
                        break;
                    case "bbcode":
                        break;
                    case "rtf":
                        foreach (var f in files) {
                            Console.WriteLine("Highlighting : [{0}] to [{0}.rtf]", f);
                            File.WriteAllText(f + ".rtf",
                                highlighter.HighlightToRTF(File.ReadAllText(f), language, style, f, fontFace: "consolas"));
                        }
                        break;
                    case "latex":
                        break;

                    case "terminal256":
                    case "console256":
                    case "256":
                    case "terminal16":
                    case "console16":
                    case "16m":
                        foreach (var f in files)
                        {
                            if (writeToConsole)
                            {
                                SetAnsiConsoleMode();
                                var output = highlighter.HighlightToTerminal256(File.ReadAllText(f), language, style, f);
                                Console.Write(output.Replace("\n", "\r\n"));
                            }
                            else
                            {
                                Console.WriteLine("Highlighting : [{0}] to [{0}.ans]", f);
                                File.WriteAllText(f + ".ans",
                                    highlighter.HighlightToTerminal256(File.ReadAllText(f), language, style, f));
                            }
                        }
                        break;
                    default:
                        Console.Error.WriteLine("Unknown output type '{0}' -- skipping", o);
                        break;
                }
            }
        }
    }
}
