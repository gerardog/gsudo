﻿using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace gsudo.AppSettings
{
    public enum RegistrySettingScope
    {
        /// <summary>
        /// System-wide setting. Affects all users. Admin Privilege required to write it.
        /// </summary>
        GlobalOnly,
        /// <summary>
        /// Can be set for the current user, or system-wide.
        /// </summary>
        Any
    }

    abstract class RegistrySetting
    {
        protected const string REGKEY = "SOFTWARE\\gsudo";
        public RegistrySettingScope Scope { get; protected set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public abstract void Save(string newValue, bool global);
        public abstract void Reset(bool global);
        public abstract object GetStringValue();
        public abstract bool HasGlobalValue();
        public abstract bool HasLocalValue();
        public abstract void ClearRunningValue();
        public abstract object Parse(string serialized);
    }

    class RegistrySetting<T> : RegistrySetting
    {
        private readonly Func<T> defaultValue;
        private T runningValue;
        private bool hasValue = false;
        private readonly Func<string, T> deserializer;
        private readonly Func<T, string> serializer;

        public RegistrySetting(string name, T defaultValue, Func<string, T> deserializer,
                               RegistrySettingScope scope = RegistrySettingScope.Any,
                               Func<T, string> serializer = null, string description = null)
            : this(name, () => defaultValue, deserializer, scope, serializer, description)
        { }

        public RegistrySetting(string name, Func<T> defaultValue, Func<string, T> deserializer,
                               RegistrySettingScope scope = RegistrySettingScope.Any,
                               Func<T, string> serializer = null, string description = null)
        {
            Name = name.Replace('_', '.');
            this.defaultValue = defaultValue;
            this.deserializer = deserializer;
            this.Scope = scope;
            this.serializer = serializer;
            this.Description = description;
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
                            if (currentValue == null) return defaultValue();
                            return deserializer(currentValue);
                        }
                    }
                }
                return defaultValue();
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
                return subkey?.GetValue(Name, null) != null;
            }
        }

        public override bool HasGlobalValue()
        {
            using (var subkey = Registry.LocalMachine.OpenSubKey(REGKEY, false))
            {
                return subkey?.GetValue(Name, null) != null;
            }
        }

        public override object GetStringValue() => serializer != null ? serializer(Value) : Value.ToString();

        public override void Save(string newValue, bool global)
        {
            Value = deserializer(newValue);
            var warning = GetAttributeOfType<DescriptionAttribute>(Value)?.Description;
            if (!string.IsNullOrEmpty(warning))
                Logger.Instance.Log(warning, LogLevel.Warning);

            if (!global && HasGlobalValue())
                Logger.Instance.Log($"A global value exists and it overrides the user value. \r\nUse 'gsudo config {Name} --global --reset' to clear it.", LogLevel.Warning);

            RegistryKey key = global ? Registry.LocalMachine : Registry.CurrentUser;
            var subkey = key.OpenSubKey(REGKEY, true) ?? key.CreateSubKey(REGKEY, true);
            subkey.SetValue(Name, GetStringValue());
            subkey.Dispose();
        }

        public override void Reset(bool global)
        {
            Value = defaultValue();

            RegistryKey key = global ? Registry.LocalMachine : Registry.CurrentUser;
            using (var subkey = key.OpenSubKey(REGKEY, true))
            {
                if (subkey.GetValue(Name) != null)
                    subkey.DeleteValue(Name);
            }
        }

        public override string ToString() => Value.ToString();

        public static implicit operator T(RegistrySetting<T> t) => t.Value;

        public override void ClearRunningValue() => hasValue = false;
        public override object Parse(string serialized) => deserializer(serialized);

#if NET5_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Enum type is assumed to have its public members preserved.")]
#endif
        public static TAttributte GetAttributeOfType<TAttributte>(T enumVal) where TAttributte : System.Attribute
        {
            var memberInfo = enumVal.GetType().GetMember(enumVal.ToString());
            if (memberInfo?.Length > 0)
            {
                return memberInfo[0]
                    .GetCustomAttributes(typeof(TAttributte), false)
                    .OfType<TAttributte>()
                    .FirstOrDefault();
            }
            return null;
        }
    }
}
