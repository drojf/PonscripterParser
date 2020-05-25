# Readme

This repository hosts my efforts to lex/parse the nscripter family of scripts. Previously this project was labelled as "ponscripter to renpy", that is, it was supposed to translate ponscripter scripts to renpy scripts. However, my attempt tried to do it at too low a level (emulating ponscripter variables in renpy), which was very difficult, and I didn't get very far with it.

I most likely won't work on this particular repository anymore, however I'm currently looking at some other similar projects I could do but I'll start a new repository for it. Therefore, this repository is more for reference than anything else.

I won't archive the repository in case there are any questions on the specifics of parsing nscripter.

Note that the code quality is not very good or well documented, and the choices with how lexing/parsing were implemented are probably very bad.

## Branches

- The branch `fix_umineko` was created by me when I was trying to fix some issues in the Umineko script (it takes in the umineko script, lexes, parses, then saves an edited version of the script).

## Useful stuff

- See the file ponscripter_grammar.txt . It doesn't actually contain the grammar, but some gotchas with the language.

## To fix

- Lots of bugs due to whitespace being passed through lexer to parser. I left it in because I woried it may be needed to resolve parsing ambiguities, but so far it was never used. If so, all the SkipWhitespace() could be removed
- Differences with nscripter interpreter
  - Ponscripter can parse builtin functions missing commas (instead of `numalias volY, 100` you write `numalias volY 100`) as it knows exactly how many arguments they take. This parser only knows whether builtins take an argument or not, and use the comma separator to determine whether the argument list is finished. You may encounter scripts which parse fine with Ponscripter, but you need to insert commas for this parser to parse it.
- If there is a parsing error, it can be difficult to figure out what went wrong
- Lack of documentation

## See also

There was another project to convert nscripter to renpy roughly 9 years ago called ["nscripter2renpy"](https://github.com/franckv/nscripter2renpy)
