using Esprima.Ast;
using Jint.Runtime.Interpreter.Declarations;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintFunctionDeclarationStatement : JintStatement<FunctionDeclaration>, IDeclaration
    {
        public JintFunctionDeclarationStatement(Engine engine, FunctionDeclaration statement) : base(engine, statement)
        {
        }

        public System.Collections.Generic.List<string> BoundNames => new System.Collections.Generic.List<string>( new[] { _statement.Id.Name } );

        public bool IsConstantDeclaration => true;

        protected override Completion ExecuteInternal()
        {
            return new Completion(CompletionType.Normal, null, null, Location);
        }
    }
}