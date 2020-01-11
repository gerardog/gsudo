using Microsoft.Win32;
using System;

namespace gsudo
{
    abstract class RegistrySetting 
    { 
        protected const string REGKEY = "SOFTWARE\\gsudo";
        static RegistrySetting()
        {
            Registry.CurrentUser.CreateSubKey(REGKEY);
        }
        public string Name { get; set; }
        public abstract void Save(string newValue);
        public abstract void Reset();
        public abstract object GetStringValue();
    }

    class RegistrySetting<T> : RegistrySetting
    {
        private T runningValue;
        bool hasValue = false;
        private T defaultValue { get; }

        private Func<string, T> deserializer;

        public T Value 
        {
            get
            {
                if (hasValue) return runningValue;

                using (var subkey = Registry.CurrentUser.OpenSubKey(REGKEY, false))
                {
                    var currentValue = subkey.GetValue(Name, null) as string;
                    if (currentValue == null) return defaultValue;
                    try
                    {
                        return deserializer(currentValue);
                    }
                    catch
                    {
                        return defaultValue;
                    }
                }
            } 
            set
            {
                runningValue = value;
                hasValue = true;
            }
        }

        public override object GetStringValue() => Value.ToString();
       
        public RegistrySetting(string name, T defaultValue, Func<string,T> deserializer)
        {
            Name = name;
            this.defaultValue = defaultValue;
            this.deserializer = deserializer;
        }

        public override void Save(string newValue)
        {
            Value = deserializer(newValue);
            using (var subkey = Registry.CurrentUser.OpenSubKey(REGKEY, true))
            {
                subkey.SetValue(Name, GetStringValue());
            }
        }

        public override void Reset()
        {
            Value = defaultValue;
            using (var subkey = Registry.CurrentUser.OpenSubKey(REGKEY, true))
            {
                if (subkey.GetValue(Name) != null)
                    subkey.DeleteValue(Name);
            }
        }
        public override string ToString()
        {
            return Value.ToString();
        }

        public static implicit operator T(RegistrySetting<T> t) => t.Value;
    }
}
