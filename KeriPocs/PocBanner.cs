using System;

namespace KeriPocs
{
    /// <summary>
    /// Tiny console-formatting helper. Keeps the POCs themselves uncluttered.
    /// </summary>
    internal static class PocBanner
    {
        public static void Section(string title)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine(" " + title);
            Console.WriteLine("--------------------------------------------------------------");
            Console.ForegroundColor = prev;
        }

        public static void Header(string text)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine("==============================================================");
            Console.WriteLine(" " + text);
            Console.WriteLine("==============================================================");
            Console.ForegroundColor = prev;
        }
    }
}
