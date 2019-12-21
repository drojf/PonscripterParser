using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser
{
    // Replace this class with the Serilog library if required later
    class Log
    {
        public static void Information(string s)
        {
            Console.WriteLine(s);
        }

        public static void Debug(string s)
        {
            Console.WriteLine(s);
        }
        public static void Error(string s)
        {
            Console.WriteLine(s);
        }

        public static void Warning(string s)
        {
            Console.WriteLine(s);
        }

    }
}
