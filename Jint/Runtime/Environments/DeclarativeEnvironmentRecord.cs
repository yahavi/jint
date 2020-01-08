using Esprima.Ast;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Argument;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Runtime.Interpreter.Expressions;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a declarative environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.1
    /// </summary>
    public sealed class DeclarativeEnvironmentRecord : EnvironmentRecord
    {
        private StringDictionarySlim<Binding> _dictionary = new StringDictionarySlim<Binding>();

        public DeclarativeEnvironmentRecord(Engine engine) : base(engine)
        {
        }
        
        private bool ContainsKey(in Key key)
        {
            return _dictionary?.ContainsKey(key) == true;
        }

        private bool TryGetValue(in Key key, out Binding value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        public override bool HasBinding(in Key name)
        {
            return ContainsKey(name);
        }

        internal override bool TryGetBinding(
            in Key name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            if (_dictionary.TryGetValue(name, out binding))
            {

                if (!binding.Mutable && !binding.IsInitialized)
                {
                    if (strict)
                    {
                        ExceptionHelper.ThrowReferenceError(_engine, $"Uninitialized binding '{name}'.");
                    }

                    value = Undefined;
                }

                value = binding.Value;

                return true;
            }

            value = default;
            return false;
        }

        public override void CreateMutableBinding(in Key name, bool canBeDeleted = false)
        {
            _dictionary[name] = new Binding(canBeDeleted, mutable: true, strict: false);
        }

        public override void CreateImmutableBinding(in Key name, bool strict = false)
        {
            _dictionary[name] = new Binding(false, mutable: false, strict: strict);
        }

        public override void InitializeBinding(in Key name, JsValue value)
        {
            if (_dictionary.ContainsKey(name))
            {
                ref var binding = ref _dictionary.GetOrAddValueRef(name);

                binding.Value = value;
            }
            else
            {
                throw new JavaScriptException($"The binding for '{name}' could not be found.");
            }
        }

        public override void SetMutableBinding(in Key name, JsValue value, bool strict)
        {
            if (!_dictionary.ContainsKey(name))
            {
                if (strict)
                {
                    ExceptionHelper.ThrowReferenceError(_engine, "Can't update the value of an innexisting binding.");
                }

                // These are the spec steps
                //CreateMutableBinding(name, true);
                //InitializeBinding(name, value);

                // This is an optimization
                _dictionary[name] = new Binding(canBeDeleted: true, mutable: true, strict: false) { Value = value };

                return;
            }

            // it's important to get a reference to the binding as not to update a clone
            ref var binding = ref _dictionary.GetOrAddValueRef(name);

            if (!binding.IsInitialized)
            {
                ExceptionHelper.ThrowReferenceError(_engine, $"The binding '{name}' has not yet been initialized.");
            }
            else
            {
                if (binding.Mutable)
                {
                    binding.Value = value;
                }
                else
                {
                    if (binding.Strict)
                    {
                        ExceptionHelper.ThrowTypeError(_engine, "Can't update the value of an immutable binding.");
                    }
                }
            }            
        }

        public override JsValue GetBindingValue(in Key name, bool strict)
        {
            Debug.Assert(_dictionary.ContainsKey(name));

            ref var binding = ref _dictionary[name];

            if (!binding.IsInitialized)
            {
                ExceptionHelper.ThrowReferenceError(_engine, $"The binding '{name}' is uninitialized.");
            }

            return binding.Value;
        }

        public override bool DeleteBinding(in Key name)
        {
            if (!_dictionary.TryGetValue(name, out var binding))
            {
                return true;
            }

            if (!binding.CanBeDeleted)
            {
                return false;
            }

            _dictionary.Remove(name);

            return true;
        }

        public override JsValue ImplicitThisValue()
        {
            return Undefined;
        }

        /// <inheritdoc />
        public override string[] GetAllBindingNames()
        {
            var keys = new string[_dictionary.Count];

            var n = 0;
            foreach (var entry in _dictionary)
            {
                keys[n++] = entry.Key;
            }

            return keys;
        }

        internal void AddFunctionParameters(
            FunctionInstance functionInstance,
            JsValue[] arguments,
            ArgumentsInstance argumentsInstance,
            IFunction functionDeclaration)
        {
            var parameters = functionDeclaration.Params;

            bool empty = _dictionary.Count == 0;

            if (!(functionInstance is ArrowFunctionInstance))
            {
                _dictionary[KnownKeys.Arguments] = new Binding(canBeDeleted: false, mutable: true, false) { Value = argumentsInstance };
            }

            for (var i = 0; i < parameters.Count; i++)
            {
                SetFunctionParameter(parameters[i], arguments, i, empty);
            }
        }

        private void SetFunctionParameter(
            INode parameter,
            JsValue[] arguments,
            int index,
            bool initiallyEmpty)
        {
            var argument = arguments.Length > index ? arguments[index] : Undefined;

            if (parameter is Identifier identifier)
            {
                SetItemSafely(identifier.Name, argument, initiallyEmpty);
            }
            else if (parameter is RestElement restElement)
            {
                // index + 1 == parameters.count because rest is last
                int restCount = arguments.Length - (index + 1) + 1;
                uint count = restCount > 0 ? (uint) restCount : 0;

                var rest = _engine.Array.ConstructFast(count);

                uint targetIndex = 0;
                for (var argIndex = index; argIndex < arguments.Length; ++argIndex)
                {
                    rest.SetIndexValue(targetIndex++, arguments[argIndex], updateLength: false);
                }

                argument = rest;

                if (restElement.Argument is Identifier restIdentifier)
                {
                    SetItemSafely(restIdentifier.Name, argument, initiallyEmpty);
                }
                else if (restElement.Argument is BindingPattern bindingPattern)
                {
                    SetFunctionParameter(bindingPattern, new [] { argument }, index, initiallyEmpty);
                }
                else
                {
                    ExceptionHelper.ThrowSyntaxError(_engine, "Rest parameters can only be identifiers or arrays");
                }
            }
            else if (parameter is ArrayPattern arrayPattern)
            {
                if (argument.IsNull())
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Destructed parameter is null");
                }

                ArrayInstance array = null;
                var arrayContents = ArrayExt.Empty<JsValue>();
                if (argument.IsArray())
                {
                    array = argument.AsArray();
                }
                else if (argument.IsObject() && argument.TryGetIterator(_engine, out var iterator))
                {
                    array = _engine.Array.ConstructFast(0);
                    var protocol = new ArrayPatternProtocol(_engine, array, iterator, arrayPattern.Elements.Count);
                    protocol.Execute();
                }

                if (!ReferenceEquals(array, null))
                {
                    arrayContents = new JsValue[array.Length];

                    for (uint i = 0; i < (uint) arrayContents.Length; i++)
                    {
                        arrayContents[i] = array.Get(i);
                    }
                }

                for (uint arrayIndex = 0; arrayIndex < arrayPattern.Elements.Count; arrayIndex++)
                {
                    SetFunctionParameter(arrayPattern.Elements[(int) arrayIndex], arrayContents, (int) arrayIndex, initiallyEmpty);
                }
            }
            else if (parameter is ObjectPattern objectPattern)
            {
                if (argument.IsNullOrUndefined())
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Destructed parameter is null or undefined");
                }

                if (!argument.IsObject())
                {
                    return;
                }

                var argumentObject = argument.AsObject();

                var jsValues = _engine._jsValueArrayPool.RentArray(1);
                foreach (var property in objectPattern.Properties)
                {
                    if (property.Key is Identifier propertyIdentifier)
                    {
                        argument = argumentObject.Get(propertyIdentifier.Name);
                    }
                    else if (property.Key is Literal propertyLiteral)
                    {
                        argument = argumentObject.Get(propertyLiteral.Raw);
                    }
                    else if (property.Key is CallExpression callExpression)
                    {
                        var jintCallExpression = JintExpression.Build(_engine, callExpression);
                        argument = argumentObject.Get(jintCallExpression.GetValue().AsString());
                    }

                    jsValues[0] = argument;
                    SetFunctionParameter(property.Value, jsValues, 0, initiallyEmpty);
                }
                _engine._jsValueArrayPool.ReturnArray(jsValues);
            }
            else if (parameter is AssignmentPattern assignmentPattern)
            {
                var idLeft = assignmentPattern.Left as Identifier;
                if (idLeft != null
                    && assignmentPattern.Right is Identifier idRight
                    && idLeft.Name == idRight.Name)
                {
                    ExceptionHelper.ThrowReferenceError(_engine, idRight.Name);
                }

                if (argument.IsUndefined())
                {
                    JsValue RunInNewParameterEnvironment(JintExpression exp)
                    {
                        var oldEnv = _engine.ExecutionContext.LexicalEnvironment;
                        var paramVarEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, oldEnv);

                        _engine.EnterExecutionContext(paramVarEnv, paramVarEnv, _engine.ExecutionContext.ThisBinding);;
                        var result = exp.GetValue();
                        _engine.LeaveExecutionContext();

                        return result;
                    }

                    var expression = assignmentPattern.Right.As<Expression>();
                    var jintExpression = JintExpression.Build(_engine, expression);

                    argument = jintExpression is JintSequenceExpression
                        ? RunInNewParameterEnvironment(jintExpression)
                        : jintExpression.GetValue();

                    if (idLeft != null && assignmentPattern.Right.IsFunctionWithName())
                    {
                        ((FunctionInstance) argument).SetFunctionName(idLeft.Name);
                    }
                }

                SetFunctionParameter(assignmentPattern.Left, new []{ argument }, 0, initiallyEmpty);
            }
        }

        private void SetItemSafely(in Key name, JsValue argument, bool initiallyEmpty)
        {
            if (initiallyEmpty || !TryGetValue(name, out var existing))
            {
                _dictionary[name] = new Binding(false, true, false) { Value = argument };
            }
            else
            {
                if (existing.Mutable)
                {
                    ref var b = ref _dictionary.GetOrAddValueRef(name);
                    b.Value = argument;
                }
                else
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Can't update the value of an immutable binding.");
                }
            }
        }

        internal void AddVariableDeclarations(ref NodeList<VariableDeclaration> variableDeclarations)
        {
            var variableDeclarationsCount = variableDeclarations.Count;
            for (var i = 0; i < variableDeclarationsCount; i++)
            {
                var variableDeclaration = variableDeclarations[i];
                var declarationsCount = variableDeclaration.Declarations.Count;
                for (var j = 0; j < declarationsCount; j++)
                {
                    var d = variableDeclaration.Declarations[j];
                    if (d.Id is Identifier id)
                    {
                        Key dn = id.Name;
                        if (!ContainsKey(dn))
                        {
                            var binding = new Binding(canBeDeleted: false, mutable: true, false) { Value = Undefined };
                            _dictionary[dn] = binding;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static JsValue HandleAssignmentPatternIfNeeded(IFunction functionDeclaration, JsValue jsValue, int index)
        {
            // TODO remove this method, overwrite with above SetFunctionParameter logic
            if (jsValue.IsUndefined()
                && index < functionDeclaration?.Params.Count
                && functionDeclaration.Params[index] is AssignmentPattern ap
                && ap.Right is Literal l)
            {
                return JintLiteralExpression.ConvertToJsValue(l);
            }

            return jsValue;
        }
        
        internal override void FunctionWasCalled()
        {
            if (_dictionary.TryGetValue(KnownKeys.Arguments, out var arguments) && arguments.Value is ArgumentsInstance argumentsInstance)
            {
                argumentsInstance.PersistArguments();
            }

            // we can safely release arguments only if it doesn't have possibility to escape the scope
            // so check if someone ever accessed it
            //if (!(_argumentsBinding.Value is ArgumentsInstance argumentsInstance))
            //{
            //    return;
            //}

            //if (!argumentsInstance._initialized && _argumentsBindingWasAccessed == false)
            //{
            //    _engine._argumentsInstancePool.Return(argumentsInstance);
            //    _argumentsBinding = default;
            //}
            //else if (_argumentsBindingWasAccessed != null && argumentsInstance._args.Length > 0)
            //{
            //    // we need to ensure we hold on to arguments given
            //    argumentsInstance.PersistArguments();
            //    _argumentsBindingWasAccessed = null;
            //}
        }

        private sealed class ArrayPatternProtocol : IteratorProtocol
        {
            private readonly ArrayInstance _instance;
            private readonly int _max;
            private long _index = -1;

            public ArrayPatternProtocol(
                Engine engine,
                ArrayInstance instance,
                IIterator iterator,
                int max) : base(engine, iterator, 0)
            {
                _instance = instance;
                _max = max;
            }

            protected override void ProcessItem(JsValue[] args, JsValue currentValue)
            {
                _index++;
                var jsValue = ExtractValueFromIteratorInstance(currentValue);
                _instance.SetIndexValue((uint) _index, jsValue, updateLength: false);
            }

            protected override bool ShouldContinue => _index < _max;

            protected override void IterationEnd()
            {
                if (_index >= 0)
                {
                    _instance.SetLength((uint) _index);
                    ReturnIterator();
                }
            }
        }
    }
}
