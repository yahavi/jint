using System.Collections.Generic;

namespace Jint.Runtime.Interpreter.Declarations
{
    public interface IDeclaration
    {
        List<string> BoundNames { get; }
        bool IsConstantDeclaration { get; }
    }
}
