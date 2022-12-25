namespace Kula.Core.Ast;

abstract class Stmt {
    public interface Visitor<R> {
        R VisitExpression(Expression stmt);
        R VisitReturn(Return stmt);
        R VisitBlock(Block stmt);
        R VisitWhile(While stmt);
        R VisitIf(If stmt);
    }
    public abstract R Accept<R>(Visitor<R> visitor);

    public class Expression : Stmt {
        public readonly Expr expression;

        public Expression(Expr expression) {
            this.expression = expression;
        }

        public override R Accept<R>(Visitor<R> visitor) {
            return visitor.VisitExpression(this);
        }
    }

    public class Return : Stmt {
        public readonly Expr? value;

        public Return(Expr? value) {
            this.value = value;
        }

        public override R Accept<R>(Visitor<R> visitor) {
            return visitor.VisitReturn(this);
        }
    }

    public class Block : Stmt {
        public readonly List<Stmt> statements;

        public Block(List<Stmt> statements) {
            this.statements = statements;
        }

        public override R Accept<R>(Visitor<R> visitor) {
            return visitor.VisitBlock(this);
        }
    }

    public class While : Stmt {
        public readonly Expr condition;
        public readonly Stmt branch;
        
        public While(Expr condition, Stmt branch) {
            this.condition = condition;
            this.branch = branch;
        }

        public override R Accept<R>(Visitor<R> visitor) {
            return visitor.VisitWhile(this);
        }
    }

    public class If : Stmt {
        public readonly Expr condition;
        public readonly Stmt thenBranch;
        public readonly Stmt? elseBranch;

        public If(Expr condition, Stmt thenBranch, Stmt? elseBranch) {
            this.condition = condition;
            this.thenBranch = thenBranch;
            this.elseBranch = elseBranch;
        }

        public override R Accept<R>(Visitor<R> visitor) {
            return visitor.VisitIf(this);
        }
    }
}