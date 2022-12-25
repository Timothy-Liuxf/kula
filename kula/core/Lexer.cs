namespace Kula.Core;

struct Token {
    public readonly TokenType type;
    public readonly string lexeme;
    public readonly object? literial;
    public readonly int line;

    public Token(TokenType type, string lexeme, object? literial, int line) {
        this.type = type;
        this.lexeme = lexeme;
        this.literial = literial;
        this.line = line;
    }

    public override string ToString() {
        return
            $"[line {line.ToString().PadRight(4)}: {type.ToString().PadRight(12)}]: {lexeme.PadRight(12)}"
            + (literial == null ? "" : $" => {literial}");
    }
}

enum TokenType {
    // Single-Character Tokens.
    LEFT_PAREN, RIGHT_PAREN, LEFT_BRACE, RIGHT_BRACE, LEFT_SQUARE, RIGHT_SQUARE,
    COMMA, DOT, MINUS, PLUS, SEMICOLON, SLASH, STAR,

    // One or Two Character Tokens.
    BANG, BANG_EQUAL, EQUAL, EQUAL_EQUAL,
    GREATER, LESS, GREATER_EQUAL, LESS_EQUAL,
    COLON_EQUAL, COLON,
    ARROW,

    // Literials.
    IDENTIFIER, STRING, NUMBER,

    // Keyword
    AND, CLASS, ELSE, FALSE, FUNC, FOR, IF, NULL,
    OR, PRINT, RETURN, THIS, TRUE, WHILE,

    // EOF
    EOF
}

class Lexer {
    private KulaEngine? kula;
    private string? source;
    private List<Token>? tokens;

    private int start;
    private int current;
    private int line;

    private Lexer() { }

    public static Lexer Instance = new Lexer();
    private static Dictionary<string, TokenType> keywordDict = new Dictionary<string, TokenType>() {
        {"and", TokenType.AND},
        {"class", TokenType.CLASS},
        {"else", TokenType.ELSE},
        {"false", TokenType.FALSE},
        {"func", TokenType.FUNC},
        {"for", TokenType.FOR},
        {"if", TokenType.IF},
        {"null", TokenType.NULL},
        {"or", TokenType.OR},
        {"print", TokenType.PRINT},
        {"return", TokenType.RETURN},
        {"this", TokenType.THIS},
        {"true", TokenType.TRUE},
        {"while", TokenType.WHILE},
    };

    public List<Token> ScanTokens(KulaEngine kula, string source) {
        this.kula = kula;
        this.source = source;
        this.tokens = new List<Token>();
        start = 0;
        current = start;
        line = 1;

        while (!IsEnd()) {
            start = current;
            ScanToken();
        }
        tokens.Add(new Token(TokenType.EOF, "", null, line));

        return tokens;
    }

    private void ScanToken() {
        char c = Advance();
        switch (c) {
            // Single Character Tokens
            case '(': AddToken(TokenType.LEFT_PAREN); break;
            case ')': AddToken(TokenType.RIGHT_PAREN); break;
            case '[': AddToken(TokenType.LEFT_SQUARE); break;
            case ']': AddToken(TokenType.RIGHT_SQUARE); break;
            case '{': AddToken(TokenType.LEFT_BRACE); break;
            case '}': AddToken(TokenType.RIGHT_BRACE); break;
            case ',': AddToken(TokenType.COMMA); break;
            case '.': AddToken(TokenType.DOT); break;
            case '-': AddToken(TokenType.MINUS); break;
            case '+': AddToken(TokenType.PLUS); break;
            case ';': AddToken(TokenType.SEMICOLON); break;
            case '*': AddToken(TokenType.STAR); break;
            case '/': AddToken(TokenType.SLASH); break;
            // Multi Character Tokens
            case ':':
                AddToken(Match('=') ? TokenType.COLON_EQUAL : TokenType.COLON);
                break;
            case '!':
                AddToken(Match('=') ? TokenType.BANG_EQUAL : TokenType.BANG);
                break;
            case '>':
                AddToken(Match('=') ? TokenType.GREATER_EQUAL : TokenType.GREATER);
                break;
            case '<':
                AddToken(Match('=') ? TokenType.LESS_EQUAL : TokenType.LESS);
                break;
            case '=':
                if (Match('>')) {
                    AddToken(TokenType.ARROW);
                    break;
                }
                if (Match('=')) {
                    AddToken(TokenType.EQUAL_EQUAL);
                    break;
                }
                AddToken(TokenType.EQUAL);
                break;
            // Comment
            case '#':
                while (Peek() != '\n' && !IsEnd()) {
                    Advance();
                }
                break;
            // Blank
            case '\n':
                ++line;
                break;
            case ' ':
            case '\t':
            case '\r':
                break;
            // Literial
            case '"':
                String();
                break;
            default:
                if (IsDigit(c)) {
                    Number();
                }
                else if (IsAlpha(c)) {
                    Identifier();
                }
                else {
                    kula!.Error(line, "Unexpected character.");
                }
                break;
        }
    }

    private void String() {
        while ((Peek() != '"') && !IsEnd()) {
            if (Peek() == '\n') ++line;
            Advance();
        }

        if (IsEnd()) {
            kula!.Error(line, "Unterminated string.");
            return;
        }

        Advance();

        string value = source!.Substring(start + 1, current - start - 2);
        AddToken(TokenType.STRING, value);
    }

    private void Number() {
        while (IsDigit(Peek())) {
            Advance();
        }
        if (Peek() == '.' && IsDigit(PeekNext())) {
            Advance();
            while (IsDigit(Peek())) {
                Advance();
            }
        }

        AddToken(TokenType.NUMBER, Double.Parse(source!.Substring(start, current - start)));
    }

    private void Identifier() {
        while (IsAlphaNumeric(Peek())) {
            Advance();
        }
        string text = source!.Substring(start, current - start);
        AddToken(keywordDict.GetValueOrDefault(text, TokenType.IDENTIFIER));
    }

    private bool IsDigit(char c) {
        return c >= '0' && c <= '9';
    }

    private bool IsAlpha(char c) {
        return c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c == '_';
    }

    private bool IsAlphaNumeric(char c) {
        return IsAlpha(c) || IsDigit(c);
    }

    private char Advance() {
        return source![current++];
    }

    private bool Match(char c) {
        if (IsEnd()) return false;
        if (source![current] != c) return false;
        ++current;
        return true;
    }

    private char Peek() {
        if (IsEnd()) return '\0';
        return source![current];
    }

    private char PeekNext() {
        if (current + 1 >= source!.Length) return '\0';
        return source![current + 1];
    }

    private bool IsEnd() {
        return current >= source!.Length;
    }

    private void AddToken(TokenType type) {
        AddToken(type, null);
    }

    private void AddToken(TokenType type, object? literial) {
        string text = source!.Substring(start, current - start);
        tokens!.Add(new Token(type, text, literial, line));
    }
}