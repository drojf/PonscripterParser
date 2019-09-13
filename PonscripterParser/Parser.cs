using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser
{
    class Parser
    {
        public static void Parse(List<Lexeme> lexemes)
        {
            foreach(Lexeme lexeme in lexemes)
            {
                Console.WriteLine(lexeme);
            }
        }
    }
}
