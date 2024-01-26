using Kula.ASTCompiler.Lexer;
using Kula.ASTCompiler.Parser;
using Kula.Runtime;
using Kula.ASTInterpreter.Runtime;
using Kula.Utilities;

namespace Kula.ASTInterpreter;

class Interpreter : Expr.IVisitor<object?>, Stmt.IVisitor<int>
{
    internal readonly Context globals;
    internal Context environment;
    private KulaEngine kulaEngine = null!;

    private int depth;
    private int maxDepth;

    private void CoreFunctions()
    {
        globals.Define("input", new NativeFunction(0, (_, args) => Console.ReadLine()));
        globals.Define("println", new NativeFunction(-1, (_, args) => {
            List<string> items = new();
            foreach (object? item in args) {
                items.Add(StandardLibrary.Stringify(item));
            }
            Console.WriteLine(string.Join(' ', items));
            return null;
        }));
        globals.Define("eval", new NativeFunction(1, (_, args) => {
            string source = StandardLibrary.Assert<string>(args[0]);
            kulaEngine.RunSource(source, "<eval>");
            return null;
        }));
        globals.Define("load", new NativeFunction(-1, (_, args) => {
            foreach (object? item in args) {
                string path = StandardLibrary.Assert<string>(item);
                if (File.Exists(path)) {
                    kulaEngine.Run(new FileInfo(path));
                }
                else {
                    throw new InterpreterInnerException($"File '{path}' does not exist.");
                }
            }
            return null;
        }));
    }

    public Interpreter() : this(200) { }

    public Interpreter(int maxDepth)
    {
        this.globals = new Runtime.Context();
        this.environment = this.globals;

        foreach (KeyValuePair<string, NativeFunction> kv in StandardLibrary.global_functions) {
            globals.Define(kv.Key, kv.Value);
        }

        CoreFunctions();

        globals.Define("__string_proto__", StandardLibrary.string_proto);
        globals.Define("__array_proto__", StandardLibrary.array_proto);
        globals.Define("__number_proto__", StandardLibrary.number_proto);
        globals.Define("__object_proto__", StandardLibrary.object_proto);
        globals.Define("__function_proto__", StandardLibrary.function_proto);

        this.maxDepth = maxDepth;
    }

    public void Interpret(KulaEngine kula, List<Stmt> stmts)
    {
        this.kulaEngine = kula;
        depth = maxDepth;
        try {
            ExecuteBlock(stmts, environment);
        }
        catch (InterpreterException runtimeError) {
            kula.RuntimeError(runtimeError);
        }
    }

    private object? Evaluate(Expr expr)
    {
        return expr.Accept(this);
    }

    private void Execute(Stmt stmt)
    {
        stmt.Accept(this);
    }

    object? Expr.IVisitor<object?>.VisitAssign(Expr.Assign expr)
    {
        object? value = Evaluate(expr.right);

        if (expr.left is Expr.Variable variable) {
            switch (expr.@operator.type) {
                case TokenType.COLON_EQUAL:
                    environment.Define(variable.name.lexeme, value);
                    break;
                case TokenType.EQUAL:
                    try {
                        environment.Assign(variable.name.lexeme, value);
                    }
                    catch (InterpreterInnerException rie) {
                        throw new InterpreterException(variable.name, rie.Message);
                    }
                    break;
            }
        }
        else if (expr.left is Expr.Get get) {
            object? container = Evaluate(get.container);
            object? key = Evaluate(get.key);

            if (container is KulaObject container_dict) {
                if (key is string key_string) {
                    container_dict.Set(key_string, value);
                }
                else {
                    throw new InterpreterException(get.@operator, "Index of 'Dict' can only be 'String'.");
                }
            }
            else if (container is KulaArray container_array) {
                if (key is double key_double) {
                    try {
                        container_array.Set(key_double, value);
                    }
                    catch (InterpreterInnerException rie) {
                        throw new InterpreterException(get.@operator, rie.Message);
                    }
                }
                else {
                    throw new InterpreterException(get.@operator, "Index of 'Array' can only be 'Number'.");
                }
            }
            else {
                throw new InterpreterException(get.@operator, "Only 'Object' have properties when set.");
            }
        }
        else {
            throw new InterpreterException(expr.@operator, "Illegal expression for assignment.");
        }

        return value;
    }

    private object EvalBinary(Token @operator, object? left, object? right)
    {
        if (@operator.type == TokenType.PLUS) {
            if (left is string left_string && right is string right_string) {
                return left_string + right_string;
            }
            if (left is double left_double && right is double right_double) {
                return left_double + right_double;
            }
            else {
                throw new InterpreterException(@operator, "Operands must be 2 numbers or 2 strings.");
            }
        }
        else if (@operator.type == TokenType.EQUAL_EQUAL) {
            return object.Equals(left, right);
        }
        else if (@operator.type == TokenType.BANG_EQUAL) {
            return !object.Equals(left, right);
        }
        else {
            if (left is double left_double && right is double right_double) {
                switch (@operator.type) {
                    case TokenType.MINUS:
                        return left_double - right_double;
                    case TokenType.STAR:
                        return left_double * right_double;
                    case TokenType.SLASH:
                        return left_double / right_double;
                    case TokenType.MODULUS:
                        return (double)((int)left_double % (int)right_double);
                    case TokenType.GREATER:
                        return left_double > right_double;
                    case TokenType.LESS:
                        return left_double < right_double;
                    case TokenType.GREATER_EQUAL:
                        return left_double >= right_double;
                    case TokenType.LESS_EQUAL:
                        return left_double <= right_double;
                    default:
                        throw new InterpreterException(@operator, $"Undefined Operator '{@operator.lexeme}'.");
                }
            }
            else {
                throw new InterpreterException(@operator, $"Operands must be numbers.");
            }
        }
    }

    object? Expr.IVisitor<object?>.VisitBinary(Expr.Binary expr)
    {
        return EvalBinary(expr.@operator, Evaluate(expr.left), Evaluate(expr.right));
    }

    int Stmt.IVisitor<int>.VisitBlock(Stmt.Block stmt)
    {
        ExecuteBlock(stmt.statements, new Runtime.Context(environment));
        return 0;
    }

    internal void ExecuteBlock(List<Stmt> statements, Runtime.Context environment)
    {
        Runtime.Context previous = this.environment;

        try {
            this.environment = environment;
            foreach (Stmt statement in statements) {
                Execute(statement);
            }
        }
        finally {
            this.environment = previous;
        }
    }

    object? Expr.IVisitor<object?>.VisitCall(Expr.Call expr)
    {
        object? callee;
        --depth;
        if (depth <= 0) {
            throw new InterpreterInnerException("Maximum recursion depth exceeded.");
        }

        // 'this' binding
        if (expr.callee is Expr.Get expr_get) {
            EvalGet(expr_get, out object? container, out object? key, out object? value);
            if (value is ICallable value_function) {
                value_function.Bind(container);
            }
            callee = value;
        }
        else {
            callee = Evaluate(expr.callee);
        }

        // __func__
        while (callee is KulaObject functor) {
            callee = functor.Get("__func__");
            if (callee is Function callee_function) {
                callee_function.Bind(functor);
            }
        }

        if (callee is ICallable function) {
            if (function.Arity >= 0 && function.Arity != expr.arguments.Count) {
                throw new InterpreterException(
                    expr.paren,
                    $"Need {function.Arity} argument(s) but {expr.arguments.Count} is given."
                );
            }

            List<object?> arguments = new();
            foreach (Expr argument in expr.arguments) {
                arguments.Add(Evaluate(argument));
            }

            try {
                return function.Call(arguments);
            }
            catch (InterpreterInnerException rie) {
                throw new InterpreterException(expr.paren, rie.Message);
            }
            finally {
                ++depth;
            }
        }
        else {
            throw new InterpreterException(expr.paren, "Can only call functions.");
        }

    }

    int Stmt.IVisitor<int>.VisitExpression(Stmt.Expression stmt)
    {
        Evaluate(stmt.expression);
        return 0;
    }

    object? Expr.IVisitor<object?>.VisitFunction(Expr.Function expr)
    {
        return new Function(expr, this, environment);
    }

    object? Expr.IVisitor<object?>.VisitGet(Expr.Get expr)
    {
        EvalGet(expr, out object? container, out object? key, out object? value);
        return value;
    }

    void EvalGet(Expr.Get expr, out object? container, out object? key, out object? value)
    {
        container = Evaluate(expr.container);
        key = Evaluate(expr.key);

        if (container is KulaObject dict) {
            if (key is string key_string) {
                value = dict.Get(key_string);
                return;
            }
            throw new InterpreterException(expr.@operator, "Index of 'Dict' can only be 'String'.");
        }
        else if (container is KulaArray array) {
            if (key is Double key_double) {
                try {
                    value = array.Get(key_double);
                }
                catch (InterpreterInnerException rie) {
                    throw new InterpreterException(expr.@operator, rie.Message);
                }
                return;
            }
            else if (key is string key_string) {
                value = StandardLibrary.array_proto.Get(key_string);
                return;
            }
            throw new InterpreterException(expr.@operator, "Index of 'Array' can only be 'Number'.");
        }
        else if (container is String string_proto) {
            if (key is string key_string) {
                value = StandardLibrary.string_proto.Get(key_string);
                return;
            }
        }
        else if (container is double number_proto) {
            if (key is string key_string) {
                value = StandardLibrary.number_proto.Get(key_string);
                return;
            }
        }
        else if (container is ICallable function_proto) {
            if (key is string key_string) {
                value = StandardLibrary.function_proto.Get(key_string);
                return;
            }
        }
        throw new InterpreterException(expr.@operator, "Only 'Object' and 'Array' have properties when get.");
    }

    int Stmt.IVisitor<int>.VisitIf(Stmt.If stmt)
    {
        if (StandardLibrary.Booleanify(Evaluate(stmt.condition))) {
            Execute(stmt.thenBranch);
        }
        else if (stmt.elseBranch is not null) {
            Execute(stmt.elseBranch);
        }
        return 0;
    }

    object? Expr.IVisitor<object?>.VisitLiteral(Expr.Literal expr)
    {
        return expr.value;
    }

    object? Expr.IVisitor<object?>.VisitLogical(Expr.Logical expr)
    {
        object? left = Evaluate(expr.left);

        if (expr.@operator.type == TokenType.OR == StandardLibrary.Booleanify(left)) {
            return left;
        }

        return Evaluate(expr.right);
    }

    int Stmt.IVisitor<int>.VisitPrint(Stmt.Print stmt)
    {
        List<string> items = new();

        foreach (Expr iexpr in stmt.items) {
            items.Add(StandardLibrary.Stringify(Evaluate(iexpr)));
        }

        Console.WriteLine(string.Join(' ', items));
        return 0;
    }

    int Stmt.IVisitor<int>.VisitReturn(Stmt.Return stmt)
    {
        throw new Return(stmt.value is null ? null : Evaluate(stmt.value));
    }

    private object? EvalUnary(Token @operator, Expr expr)
    {
        object? value = Evaluate(expr);

        switch (@operator.type) {
            case TokenType.MINUS:
                if (value is double value_double) {
                    return -value_double;
                }
                throw new InterpreterException(@operator, "Operand must be a number.");
            case TokenType.BANG:
                return !StandardLibrary.Booleanify(value);
        }

        throw new InterpreterException(@operator, "Undefined Operator.");
    }

    object? Expr.IVisitor<object?>.VisitUnary(Expr.Unary expr)
    {
        return EvalUnary(expr.@operator, expr.right);
    }

    object? Expr.IVisitor<object?>.VisitVariable(Expr.Variable expr)
    {
        try {
            return environment.Get(expr.name.lexeme);
        }
        catch (InterpreterInnerException rie) {
            throw new InterpreterException(expr.name, rie.Message);
        }
    }

    int Stmt.IVisitor<int>.VisitFor(Stmt.For stmt)
    {
        Runtime.Context previous = environment;
        environment = new Runtime.Context(previous);

        if (stmt.initializer is not null) {
            Execute(stmt.initializer);
        }
        while (stmt.condition is null ? true : StandardLibrary.Booleanify(Evaluate(stmt.condition))) {
            try {
                Execute(stmt.body);
            }
            catch (Break) {
                break;
            }
            catch (Continue) {
                continue;
            }
            finally {
                if (stmt.increment is not null) {
                    Evaluate(stmt.increment);
                }
            }
        }

        environment = previous;

        return 0;
    }

    int Stmt.IVisitor<int>.VisitBreak(Stmt.Break stmt)
    {
        throw new Break();
    }

    int Stmt.IVisitor<int>.VisitContinue(Stmt.Continue stmt)
    {
        throw new Continue();
    }

    int Stmt.IVisitor<int>.VisitImport(Stmt.Import stmt)
    {
        return 0;
    }


    internal class Return : Exception
    {
        public readonly object? value;
        public Return(object? value)
        {
            this.value = value;
        }
    }

    internal class Break : Exception
    {
        public Break() { }
    }

    internal class Continue : Exception
    {
        public Continue() { }
    }
}
