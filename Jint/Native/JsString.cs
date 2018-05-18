using Jint.Native.String;
using Jint.Runtime;
using System;

namespace Jint.Native
{
    public class JsString : JsValue, IEquatable<JsString>
    {
        private const int AsciiMax = 126;
        private static readonly JsString[] _charToJsValue;
        private static readonly JsString[] _charToStringJsValue;

        private static readonly JsString Empty = new JsString("");
        private static readonly JsString NullString = new JsString("null");

        private string _value;
        internal JsString _next;

        static JsString()
        {
            _charToJsValue = new JsString[AsciiMax + 1];
            _charToStringJsValue = new JsString[AsciiMax + 1];

            for (int i = 0; i <= AsciiMax; i++)
            {
                _charToJsValue[i] = new JsString((char)i);
                _charToStringJsValue[i] = new JsString(((char)i).ToString());
            }
        }

        public JsString(string value) : base(Types.String)
        {
            _value = value;
            _next = null;
        }

        public override object ToObject()
        {
            return _value;
        }

        public JsString(char value) : this(value.ToString())
        {
        }

        public JsString(JsString other) : base(Types.String)
        {
            if (other != null)
            {
                _value = other._value;
                _next = new JsString(other._next);
            }
        }

        public virtual JsString Append(JsString jsString)
        {
            if (ReferenceEquals(this, Empty))
            {
                return new JsString(jsString);
            }

            Last._next = new JsString(jsString);

            return this;
        }

        public virtual JsString Append(string value)
        {
            if (ReferenceEquals(this, Empty))
            {
                return Create(value);
            }

            Last._next = Create(value);
            return this;
        }

        public virtual JsString Append(JsValue jsValue)
        {
            if (jsValue.IsString())
            {
                return Append((JsString)jsValue);
            }
            else
            {
                return Append(TypeConverter.ToString(jsValue));
            }
        }

        internal JsString Last
        {
            get
            {
                if (_next == null)
                {
                    return this;
                }
                else
                {
                    return _next.Last;
                }
            }
        }

        internal virtual bool IsNullOrEmpty()
        {
            return string.IsNullOrEmpty(_value) && (_next == null || _next.IsNullOrEmpty());
        }

        internal static JsString Create(string value)
        {
            if (value.Length <= 1)
            {
                if (value == "")
                {
                    return Empty;
                }

                if (value.Length == 1)
                {
                    if (value[0] >= 0 && value[0] <= AsciiMax)
                    {
                        return _charToStringJsValue[value[0]];
                    }
                }
            }
            else if (value == Native.Null.Text)
            {
                return NullString;
            }

            return new JsString(value);
        }

        internal static JsString Create(char value)
        {
            if (value >= 0 && value <= AsciiMax)
            {
                return _charToJsValue[value];
            }

            return new JsString(value);
        }

        public override string ToString()
        {
            if (_next == null)
            {
                return _value;
            }
            else
            {
                var builder = StringExecutionContext.Current.GetStringBuilder(0);
                builder.Clear();

                builder.Append(_value);

                var next = _next;

                while (next != null)
                {
                    if (next._value != null)
                    {
                        builder.Append(next._value);
                    }

                    next = next._next;
                }

                _value = builder.ToString();

                _next = null;
                return _value;
            }
        }

        public override bool Equals(JsValue obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (!(obj is JsString s))
            {
                return false;
            }

            return Equals(s);
        }

        public bool Equals(JsString other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (ReferenceEquals(_next, null) && ReferenceEquals(other._next, null))
            {
                return _value == other._value;
            }
            else
            {
                return this.ToString() == other.ToString();
            }
        }

    }
}