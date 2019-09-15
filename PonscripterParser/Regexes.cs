//Please see UncleMion's POnscripter Documentation here: https://www.drojf.com/nscripter/NScripter_API_Reference.html (mirror of website as original was down)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PonscripterParser
{
    class Regexes {
        public static Regex hexColor = RegexFromStart(@"#[0-9abcdef]{6}", RegexOptions.IgnoreCase);
        public static Regex word = RegexFromStart(@"[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.IgnoreCase);
        public static Regex numericLiteral = RegexFromStart(@"\d+", RegexOptions.IgnoreCase);

        //For loop regexes
        public static Regex ForTo = RegexFromStart(@"to", RegexOptions.IgnoreCase);
        public static Regex ForEquals = RegexFromStart(@"=", RegexOptions.IgnoreCase);
        public static Regex ForStep = RegexFromStart(@"step", RegexOptions.IgnoreCase);

        //Text line end check
        public static Regex TextLineEnd = RegexFromStart(@"(/|\\)\s*(;.*)?$", RegexOptions.IgnoreCase);

        //Special case for partial divide expression/page wait ambiguity like "langen:voicedelay 1240/"
        //TODO: decide if this is a bug or not
        public static Regex DividePageWaitAmbiguity = RegexFromStart(@"/\s*(;.*)?$", RegexOptions.IgnoreCase);

        //Jumpf target ~ regex
        public static Regex JumpfTarget = RegexFromStart(@"\s*~\s*$", RegexOptions.IgnoreCase);

        //Ponscripter Formatting tag region
        public static Regex ponscripterFormattingTagRegion = RegexFromStart(@"~[^~]*~", RegexOptions.IgnoreCase);

        public static Regex whitespace = RegexFromStart(@"\s+", RegexOptions.IgnoreCase);

        public static Regex comment = RegexFromStart(@";.*", RegexOptions.IgnoreCase);

        public static Regex colon = RegexFromStart(@":", RegexOptions.IgnoreCase);
        public static Regex lSquareBracket = RegexFromStart(@"\[", RegexOptions.IgnoreCase);
        public static Regex rSquareBracket = RegexFromStart(@"\]", RegexOptions.IgnoreCase);
        public static Regex lRoundBracket = RegexFromStart(@"\(", RegexOptions.IgnoreCase);
        public static Regex rRoundBracket = RegexFromStart(@"\)", RegexOptions.IgnoreCase);
        public static Regex backSlash = RegexFromStart(@"\\", RegexOptions.IgnoreCase);
        public static Regex forwardSlash = RegexFromStart(@"/", RegexOptions.IgnoreCase);
        public static Regex atSymbol = RegexFromStart(@"@", RegexOptions.IgnoreCase);

        public static Regex label = RegexFromStart(@"\*\w+", RegexOptions.IgnoreCase);

        public static Regex tilde = RegexFromStart(@"~", RegexOptions.IgnoreCase);

        public static Regex numericReferencePrefix = RegexFromStart(@"\%", RegexOptions.IgnoreCase);
        public static Regex stringReferencePrefix = RegexFromStart(@"\$", RegexOptions.IgnoreCase);
        public static Regex arrayReferencePrefix = RegexFromStart(@"\?", RegexOptions.IgnoreCase);

        public static Regex normalStringLiteral = RegexFromStart("\"[^\"]*\"", RegexOptions.IgnoreCase);
        public static Regex hatStringLiteral = RegexFromStart(@"\^[^\^]\^", RegexOptions.IgnoreCase);

        public static Regex comma = RegexFromStart(@",", RegexOptions.IgnoreCase);

        public static Regex RegexFromStart(string s, RegexOptions options)
        {
            return new Regex(@"\G" + s, options);
        }
    }
}
