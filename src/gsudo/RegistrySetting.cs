using Microsoft.Win32;
using Newtonsoft.Json;

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

        public abstract object GetStringValue();
    }

    class RegistrySetting<T> : RegistrySetting
    {
        private T runningValue;
        bool hasValue = false;
        private T defaultValue { get; }

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
                        return JsonConvert.DeserializeObject<T>(currentValue);
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

        public override object GetStringValue() => Value;
       
        public RegistrySetting(string name, T defaultValue)
        {
            Name = name;
            this.defaultValue = defaultValue;
        }

        public override void Save(string newValue)
        {
            Value = JsonConvert.DeserializeObject<T>(newValue);
            using (var subkey = Registry.CurrentUser.OpenSubKey(REGKEY, true))
            {
                subkey.SetValue(Name, JsonConvert.SerializeObject(Value));
            }
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static implicit operator T(RegistrySetting<T> t) => t.Value;
    }
}
