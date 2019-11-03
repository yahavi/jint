using Jint.Native;

namespace Jint.Runtime.Environments
{
    public struct Binding
    {
        public Binding(bool canBeDeleted, bool mutable, bool strict)
        {
            Value = JsValue.Null;
            CanBeDeleted = canBeDeleted;
            Mutable = mutable;
            Strict = strict;
        }

        public JsValue Value;
        public readonly bool CanBeDeleted;
        public readonly bool Mutable;
        public readonly bool Strict;

        public bool IsInitialized => !ReferenceEquals(Value, JsValue.Null);
    }
}
