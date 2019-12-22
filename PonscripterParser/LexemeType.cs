//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

namespace PonscripterParser
{
    enum LexemeType
    {
        WHITESPACE,
        COMMA,
        COMMENT,
        COLON,
        LABEL,
        JUMPF_TARGET,
        FORMATTING_TAG,
        HEX_COLOR,
        UNHANDLED_CONTROL_CHAR,
        IF,
        NOT_IF,
        FOR,
        FUNCTION,
        NUMERIC_LITERAL,
        STRING_LITERAL,
        HAT_STRING_LITERAL,
        NUM_ALIAS,
        ALIAS,
        NUMERIC_REFERENCE,
        STRING_REFERENCE,
        ARRAY_REFERENCE,
        L_SQUARE_BRACKET,
        R_SQUARE_BRACKET,
        L_ROUND_BRACKET,
        R_ROUND_BRACKET,
        OPERATOR,//should do this properly later
        DIALOGUE,
        WORD,
        AT_SYMBOL,
        BACK_SLASH,
        //FORWARD_SLASH, //for now, forawrd slashes are emitted as OPERATOR - "\" (as a divide symbol)
    }
}
