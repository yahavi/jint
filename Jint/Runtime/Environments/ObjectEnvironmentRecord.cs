using System;
using System.Diagnostics;
using System.Linq;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents an object environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.2
    /// </summary>
    public sealed class ObjectEnvironmentRecord : EnvironmentRecord
    {
        private readonly ObjectInstance _bindingObject;
        private readonly bool _provideThis;
        private readonly bool _withEnvironment;

        public ObjectEnvironmentRecord(Engine engine, ObjectInstance bindingObject, bool provideThis, bool withEnvironment) : base(engine)
        {
            _bindingObject = bindingObject;
            _provideThis = provideThis;
            _withEnvironment = withEnvironment;
        }

        /// <summary>
        /// The concrete Environment Record method HasBinding for object Environment Records determines if its associated binding object has a property whose name is the value of the argument N:
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public override bool HasBinding(in Key name)
        {
            if (!_bindingObject.HasProperty(name))
            {
                return false;
            }

            if (!_withEnvironment)
            {
                return true;
            }

            // TODO: Step 7 and following

            return false;
        }

        internal override bool TryGetBinding(
            in Key name,
            bool strict,
            out Binding binding,
            out JsValue value)
        {
            // we unwrap by name
            binding = default;

            var desc = _bindingObject.GetProperty(name);
            if (desc == PropertyDescriptor.Undefined)
            {
                value = default;
                return false;
            }

            value = ObjectInstance.UnwrapJsValue(desc, this);
            return true;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-createmutablebinding-n-d
        /// </summary>
        public override void CreateMutableBinding(in Key name, bool configurable = true)
        {
            var propertyDescriptor = configurable
                ? new PropertyDescriptor(Undefined, PropertyFlag.ConfigurableEnumerableWritable)
                : new PropertyDescriptor(Undefined, PropertyFlag.NonConfigurable);

            _bindingObject.SetOwnProperty(name, propertyDescriptor);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-object-environment-records-createimmutablebinding-n-s
        /// </summary>
        public override void CreateImmutableBinding(in Key name, bool configurable = true)
        {
            Debug.Assert(false);
        }

        public override void InitializeBinding(in Key name, JsValue value)
        {

        }

        public override void SetMutableBinding(in Key name, JsValue value, bool strict)
        {
            _bindingObject.Put(name, value, strict);
        }

        public override JsValue GetBindingValue(in Key name, bool strict)
        {
            var desc = _bindingObject.GetProperty(name);
            if (strict && desc == PropertyDescriptor.Undefined)
            {
                ExceptionHelper.ThrowReferenceError(_engine, name);
            }

            return ObjectInstance.UnwrapJsValue(desc, this);
        }

        public override bool DeleteBinding(in Key name)
        {
            return _bindingObject.Delete(name, false);
        }

        public override JsValue ImplicitThisValue()
        {
            if (_provideThis)
            {
                return _bindingObject;
            }

            return Undefined;
        }

        public override string[] GetAllBindingNames()
        {
            if (!ReferenceEquals(_bindingObject, null))
            {
                return _bindingObject.GetOwnProperties().Select( x=> x.Key).ToArray();
            }

            return ArrayExt.Empty<string>();
        }

        public override bool Equals(JsValue other)
        {
            return ReferenceEquals(_bindingObject, other);
        }

        internal override void FunctionWasCalled()
        {
        }
    }
}
