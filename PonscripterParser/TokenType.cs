using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser
{
    public enum TokenType
    {
        ClickWait,
        PageWait,
        IgnoreNewLine,
        Text,
        FnCall,
        Colon,
        Literal,
        Operator,
        Comma,
        Alias, //numAlias, stringalias etc
        WhiteSpace, //there is no RegexDef for this - it's just in the main parsing loop
        StringVar,     //eg $Example_String_variable1
        NumericVar,    //eg %Example_String_variable2
        Bracket,
        Label,
        Hat,
    }
}
