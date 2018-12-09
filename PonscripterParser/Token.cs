using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser
{
    public class Token
    {
        public TokenType tokenType;
        public string tokenString;

        public Token(TokenType tokenType, string tokenValue)
        {
            this.tokenType = tokenType;
            this.tokenString = tokenValue;
        }

        public override string ToString()
        {
            return $"{tokenType,8}: [{tokenString}]";
        }
    }
}
