using Microsoft.Win32;
using System;

namespace gsudo
{
    public enum RegistrySettingScope { GlobalOnly, Any }

    abstract class RegistrySetting
    {
        protected const string REGKEY = "SOFTWARE\\gsudo";
        public RegistrySettingScope Scope { get; protected set; }

        public string Name { get; set; }
        public abstract void Save(string newValue, bool global);
        public abstract void Reset(bool global);
        public abstract object GetStringValue();
        public abstract bool HasGlobalValue();
        public abstract bool HasLocalValue();
        public abstract void ClearRunningValue();

    }

    class RegistrySetting<T> : RegistrySetting
    {
        private T defaultValue { get; }
        private T runningValue;
        bool hasValue = false;
        private Func<string, T> deserializer;

        public RegistrySetting(string name, T defaultValue, Func<string, T> deserializer, RegistrySettingScope scope = RegistrySettingScope.Any)
        {
            Name = name;
            this.defaultValue = defaultValue;
            this.deserializer = deserializer;
            this.Scope = scope;
        }

        public T Value
        {
            get
            {
                if (hasValue) return runningValue;

                using (var subkey = Registry.LocalMachine.OpenSubKey(REGKEY, false))
                {
                    if (subkey != null)
                    {
                        var currentValue = subkey.GetValue(Name, null) as string;
                        if (currentValue != null) return deserializer(currentValue);
                    }
                }

                if (Scope != RegistrySettingScope.GlobalOnly)
                {
                    using (var subkey = Registry.CurrentUser.OpenSubKey(REGKEY, false))
                    {
                        if (subkey != null)
                        {
                            var currentValue = subkey.GetValue(Name, null) as string;
                            if (currentValue == null) return defaultValue;
                            return deserializer(currentValue);
                        }
                    }
                }
                return defaultValue;
            }
            set
            {
                runningValue = value;
                hasValue = true;
            }
        }

        public override bool HasLocalValue()
        {
            using (var subkey = Registry.CurrentUser.OpenSubKey(REGKEY, false))
            {
                if (subkey != null)
                {
                    return subkey.GetValue(Name, null) != null;
                }
            }

            return false;
        }

        public override bool HasGlobalValue()
        {
            using (var subkey = Registry.LocalMachine.OpenSubKey(REGKEY, false))
            {
                if (subkey != null)
                {
                    return subkey.GetValue(Name, null) != null;
                }
            }

            return false;
        }
        public override object GetStringValue() => Value.ToString();

        public override void Save(string newValue, bool global)
        {
            if (!global && HasGlobalValue())
                Logger.Instance.Log($"A global value exists and it overrides the user value. \r\nUse 'gsudo config {Name} --global --reset' to clear it.", LogLevel.Warning);

            RegistryKey key;

            if (global)
                key = Registry.LocalMachine;
            else
                key = Registry.CurrentUser;

            var subkey = key.OpenSubKey(REGKEY, true);
            if (subkey == null)
                subkey = key.CreateSubKey(REGKEY, true);

            Value = deserializer(newValue);
            subkey.SetValue(Name, GetStringValue());
            subkey.Dispose();
        }

        public override void Reset(bool global)
        {
            Value = defaultValue;

            RegistryKey key;
            if (global)
                key = Registry.LocalMachine;
            else
                key = Registry.CurrentUser;

            using (var subkey = key.OpenSubKey(REGKEY, true))
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

        public override void ClearRunningValue() => hasValue = false;
    }
}
