using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser
{
    public enum LexingMode
    {
        Normal,
        Text,
        ExpressionExceptOperator,
        OperatorOrComma,
    }
}
