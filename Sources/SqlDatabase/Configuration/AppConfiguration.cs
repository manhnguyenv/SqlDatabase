﻿using System.Configuration;

namespace SqlDatabase.Configuration
{
    public sealed class AppConfiguration : ConfigurationSection
    {
        public const string SectionName = "sqlDatabase";

        private const string PropertyGetCurrentVersionScript = "getCurrentVersion";
        private const string PropertySetCurrentVersionScript = "setCurrentVersion";

        [ConfigurationProperty(PropertyGetCurrentVersionScript, DefaultValue = "SELECT value from sys.fn_listextendedproperty('version', default, default, default, default, default, default)")]
        public string GetCurrentVersionScript
        {
            get => (string)this[PropertyGetCurrentVersionScript];
            set => this[PropertyGetCurrentVersionScript] = value;
        }

        [ConfigurationProperty(PropertySetCurrentVersionScript, DefaultValue = "EXEC sys.sp_updateextendedproperty @name=N'version', @value=N'{{TargetVersion}}'")]
        public string SetCurrentVersionScript
        {
            get => (string)this[PropertySetCurrentVersionScript];
            set => this[PropertySetCurrentVersionScript] = value;
        }

        public static AppConfiguration GetCurrent()
        {
            return (AppConfiguration)ConfigurationManager.GetSection(SectionName) ?? new AppConfiguration();
        }
    }
}