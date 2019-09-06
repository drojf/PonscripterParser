using System.Text.RegularExpressions;

namespace PonscripterParser
{
    class NamedRegex
    {
        public Regex regex;
        public TokenType tokenType;

        public NamedRegex(Regex r, TokenType t) {
            regex = r;
            tokenType = t;
        }
    }
}
